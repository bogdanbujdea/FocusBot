using System.Runtime.InteropServices;
using FocusBot.App.ViewModels;
using FocusBot.App.Views;
using FocusBot.Core.Interfaces;
using FocusBot.Infrastructure.Data;
using FocusBot.Infrastructure.Services;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.UI.Xaml;
using Microsoft.Windows.AppLifecycle;
using Windows.ApplicationModel.Activation;
using Windows.Storage;

namespace FocusBot.App
{
    public partial class App
    {
        private Window? _window;
        private readonly IServiceProvider? _services;

        public App()
        {
            InitializeComponent();

            AppDomain.CurrentDomain.UnhandledException += OnUnhandledException;

            var appDataRoot = ApplicationData.Current.LocalFolder.Path;
            Directory.CreateDirectory(appDataRoot);

            var dataPath = Path.Combine(appDataRoot, "focusbot.db");
            var keysPath = Path.Combine(appDataRoot, "keys");
            Directory.CreateDirectory(keysPath);

            var services = new ServiceCollection();
            services.AddLogging(builder => builder.AddDebug());
            services
                .AddDataProtection()
                .SetApplicationName("FocusBot")
                .PersistKeysToFileSystem(new DirectoryInfo(keysPath));
            services.AddDbContext<AppDbContext>(o => o.UseSqlite($"Data Source={dataPath}"));
            services.AddSingleton<ISettingsService>(sp => new SettingsService(
                sp.GetRequiredService<IDataProtectionProvider>(),
                sp.GetRequiredService<ILogger<SettingsService>>(),
                appDataRoot
            ));
            services.AddTransient<AccountSettingsViewModel>();
            services.AddSingleton<IAuthService>(sp => new SupabaseAuthService(
                new HttpClient(),
                sp.GetRequiredService<ISettingsService>(),
                sp.GetRequiredService<ILogger<SupabaseAuthService>>()
            ));
            services.AddSingleton<IWindowMonitorService, WindowMonitorService>();
            services.AddSingleton<AppUIThreadDispatcher>();
            services.AddSingleton<IUIThreadDispatcher>(sp =>
                sp.GetRequiredService<AppUIThreadDispatcher>()
            );
            services.AddSingleton<IFocusBotApiClient>(sp =>
            {
                var baseUrl = GetFocusBotApiBaseUrl();
                return new FocusBotApiClient(
                    new HttpClient { BaseAddress = new Uri(baseUrl) },
                    sp.GetRequiredService<IAuthService>(),
                    sp.GetRequiredService<ILogger<FocusBotApiClient>>()
                );
            });
            services.AddScoped<IClassificationService, AlignmentClassificationService>();
            services.AddSingleton<IClientService, DesktopClientService>();
            services.AddSingleton<IPlanService, PlanService>();
            services.AddSingleton<INavigationService, MainWindowNavigationService>();
            services.AddSingleton<IFocusHubClient>(sp =>
            {
                var baseUrl = GetFocusBotApiBaseUrl();
                return new FocusHubClientService(
                    sp.GetRequiredService<IAuthService>(),
                    sp.GetRequiredService<ILogger<FocusHubClientService>>(),
                    baseUrl
                );
            });
            services.AddTransient<ApiKeySettingsViewModel>();
            services.AddSingleton<OverlaySettingsViewModel>();
            services.AddTransient<PlanSelectionViewModel>();
            services.AddTransient<SettingsViewModel>();
            services.AddTransient<NewSessionViewModel>();
            services.AddTransient<SessionPageViewModel>();

            _services = services.BuildServiceProvider();

            using var scope = _services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            db.Database.Migrate();
        }

        protected override void OnLaunched(Microsoft.UI.Xaml.LaunchActivatedEventArgs args)
        {
            _ = RunStartupAsync();
        }

        private async Task RunStartupAsync()
        {
            try
            {
                await StartApplicationAsync();
            }
            catch (Exception ex)
            {
                ShowExceptionMessage("Startup failed", ex);
            }
        }

        /// <summary>
        /// Restores auth before creating the main window so background services can initialize.
        /// </summary>
        private async Task StartApplicationAsync()
        {
            var auth = _services!.GetRequiredService<IAuthService>();
            auth.ReAuthRequired += OnReAuthRequired;

            // Protocol launch (magic link): complete the callback before session restore so tokens
            // exist on disk before TryRestoreSessionAsync, and avoid racing InitializeAuthAsync.
            var launchActivation = AppInstance.GetCurrent().GetActivatedEventArgs();
            var launchRequest = ActivationRequest.From(launchActivation);
            if (launchRequest.IsSupported && !string.IsNullOrWhiteSpace(launchRequest.ProtocolUri))
            {
                await HandleActivationAsync(launchRequest);
            }

            await InitializeAuthAsync(auth);

            auth.AuthStateChanged += () => _ = OnAuthStateChangedAsync();

            await OnAuthStateChangedAsync();

            var windowMonitor = _services!.GetRequiredService<IWindowMonitorService>();

            _window = new MainWindow(windowMonitor);

            var navigationService = _services!.GetRequiredService<INavigationService>();
            if (navigationService is MainWindowNavigationService mainNav)
                mainNav.SetWindow(_window);

            var sessionPageViewModel = _services!.GetRequiredService<SessionPageViewModel>();
            _window.Content = new SessionPage { DataContext = sessionPageViewModel };

            var uiDispatcher = _services!.GetRequiredService<AppUIThreadDispatcher>();
            uiDispatcher.DispatcherQueue = _window.DispatcherQueue;

            // Connect SignalR only after DispatcherQueue exists. Hub handlers marshal to the UI thread;
            // if DispatcherQueue was still null, RunOnUIThreadAsync would run work on the SignalR thread
            // and WinRT/XAML updates throw COMException.
            var authAfterVm = _services!.GetRequiredService<IAuthService>();
            if (authAfterVm.IsAuthenticated)
            {
                // Do not use ConfigureAwait(false) here: WinUI requires Activate(), overlay, and further
                // XAML work on the window's UI thread. Continuing on the thread pool leaves no window
                // in the taskbar.
                await ConnectFocusHubAsync();
            }

            await ActivateAndShowChromeAsync();
        }

        public void HandleActivation(AppActivationArguments activationArgs)
        {
            if (_services is null)
                return;

            var request = ActivationRequest.From(activationArgs);
            if (!request.IsSupported)
                return;

            var dispatcher = _services.GetRequiredService<AppUIThreadDispatcher>();
            if (dispatcher.DispatcherQueue is not null)
            {
                _ = dispatcher.DispatcherQueue.TryEnqueue(async () =>
                {
                    await HandleActivationAsync(request);
                });
            }
            else
            {
                _ = HandleActivationAsync(request);
            }
        }

        public async Task HandleActivationAsync(ActivationRequest request)
        {
            if (_services is null)
                return;

            var auth = _services.GetRequiredService<IAuthService>();

            // Restore any existing session as early as possible.
            await auth.TryRestoreSessionAsync();
            if (
                request.Kind != ExtendedActivationKind.Protocol
                || string.IsNullOrWhiteSpace(request.ProtocolUri)
            )
                return;

            var handled = await auth.HandleCallbackAsync(request.ProtocolUri);
            if (!handled)
            {
                ShowExceptionMessage("Sign-in failed", new Exception("Auth callback failed."));
            }
        }

        /// <summary>
        /// Called when the auth service determines the refresh token is exhausted and the user
        /// must authenticate again. Stops background services and navigates to Settings.
        /// </summary>
        private void OnReAuthRequired()
        {
            _ = DisconnectFocusHubAsync();

            if (_services is null)
                return;

            var dispatcher = _services.GetRequiredService<AppUIThreadDispatcher>();

            // Navigate on the UI thread so WinUI controls are not touched off-thread.
            dispatcher.DispatcherQueue?.TryEnqueue(() =>
            {
                _window?.Activate();
            });
        }

        /// <summary>
        /// Initializes auth state at startup. Awaits session restore (including token refresh)
        /// before triggering auth-dependent initialization like client registration.
        /// </summary>
        private async Task InitializeAuthAsync(IAuthService auth)
        {
            try
            {
                await auth.TryRestoreSessionAsync();
            }
            catch (Exception ex)
            {
                var logger = _services?.GetRequiredService<ILogger<App>>();
                logger?.LogError(ex, "Failed to restore auth session at startup");
            }
        }

        private async Task OnAuthStateChangedAsync()
        {
            if (_services is null)
                return;

            var auth = _services.GetRequiredService<IAuthService>();
            if (!auth.IsAuthenticated)
            {
                await DisconnectFocusHubAsync().ConfigureAwait(false);
                return;
            }

            // Ensure the backend user row exists before any feature calls.
            // /auth/me uses get-or-create, so this is safe on every sign-in and session restore.
            var apiClient = _services.GetRequiredService<IFocusBotApiClient>();
            var provisioned = await apiClient.ProvisionUserAsync();
            if (!provisioned)
            {
                var logger = _services.GetRequiredService<ILogger<App>>();
                logger.LogWarning(
                    "Backend user provisioning failed; cloud features may be unavailable"
                );
            }

            var planService = _services.GetRequiredService<IPlanService>();
            await planService.RefreshAsync();

            var clientService = _services.GetRequiredService<IClientService>();
            await clientService.EnsureClientIdLoadedAsync();
            if (clientService.GetClientId() is null)
                await clientService.RegisterAsync();

            await ConnectFocusHubAsync().ConfigureAwait(false);
        }

        private async Task ConnectFocusHubAsync()
        {
            if (_services is null)
                return;

            try
            {
                var hub = _services.GetRequiredService<IFocusHubClient>();
                // Preserve UI sync context when called from startup so Activate() stays on the UI thread.
                await hub.ConnectAsync();
            }
            catch (Exception ex)
            {
                var logger = _services.GetRequiredService<ILogger<App>>();
                logger.LogWarning(ex, "Focus hub connect failed");
            }
        }

        /// <summary>
        /// Activates the main window on its UI thread (required for WinUI; worker-thread Activate is unreliable).
        /// </summary>
        private Task ActivateAndShowChromeAsync()
        {
            var window = _window;
            if (window is null)
                return Task.CompletedTask;

            var dq = window.DispatcherQueue;
            if (dq.HasThreadAccess)
            {
                window.Activate();
                return Task.CompletedTask;
            }

            var tcs = new TaskCompletionSource();
            if (
                !dq.TryEnqueue(() =>
                {
                    try
                    {
                        window.Activate();
                        tcs.TrySetResult();
                    }
                    catch (Exception ex)
                    {
                        tcs.TrySetException(ex);
                    }
                })
            )
            {
                tcs.TrySetException(
                    new InvalidOperationException("Could not enqueue main window Activate.")
                );
            }

            return tcs.Task;
        }

        private async Task DisconnectFocusHubAsync()
        {
            if (_services is null)
                return;

            try
            {
                var hub = _services.GetRequiredService<IFocusHubClient>();
                await hub.DisconnectAsync().ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                var logger = _services.GetRequiredService<ILogger<App>>();
                logger.LogWarning(ex, "Focus hub disconnect failed");
            }
        }

        public readonly record struct ActivationRequest(
            ExtendedActivationKind Kind,
            string? ProtocolUri
        )
        {
            public bool IsSupported => Kind == ExtendedActivationKind.Protocol;

            public static ActivationRequest From(AppActivationArguments activationArgs)
            {
                try
                {
                    var kind = activationArgs.Kind;
                    if (kind != ExtendedActivationKind.Protocol)
                        return new ActivationRequest(kind, null);

                    if (activationArgs.Data is not ProtocolActivatedEventArgs protocolArgs)
                        return new ActivationRequest(kind, null);

                    return new ActivationRequest(kind, protocolArgs.Uri?.ToString());
                }
                catch (COMException)
                {
                    // AppActivationArguments is a WinRT object and may not be agile across threads.
                    // If we can't safely read Data here, treat activation as unsupported.
                    return new ActivationRequest(default, null);
                }
            }
        }

        private static void OnUnhandledException(
            object? sender,
            System.UnhandledExceptionEventArgs e
        )
        {
            if (e.ExceptionObject is Exception ex)
                ShowExceptionMessage("Unhandled exception", ex);
        }

        private static void ShowExceptionMessage(string title, Exception ex)
        {
            var message = ex.ToString();
            MessageBoxW(IntPtr.Zero, message, title, 0x10); // MB_ICONERROR
        }

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        private static extern int MessageBoxW(IntPtr hWnd, string text, string caption, uint type);

        private static string GetFocusBotApiBaseUrl()
        {
#if DEBUG
            return "http://localhost:5251";
#else
            return "https://api.foqus.me";
#endif
        }
    }
}
