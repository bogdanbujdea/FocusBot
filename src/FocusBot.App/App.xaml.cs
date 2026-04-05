using System.Runtime.InteropServices;
using FocusBot.App.ViewModels;
using FocusBot.App.Views;
using FocusBot.Core.Interfaces;
using FocusBot.Infrastructure.Services;
using Microsoft.AspNetCore.DataProtection;
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

            // Catches exceptions that escape XAML/WinRT event handlers (async void,
            // binding updates, converters) before they cross the WinRT ABI and become 0xC000027B.
            this.UnhandledException += OnXamlUnhandledException;

            var appDataRoot = ApplicationData.Current.LocalFolder.Path;
            Directory.CreateDirectory(appDataRoot);

            var keysPath = Path.Combine(appDataRoot, "keys");
            Directory.CreateDirectory(keysPath);

            var services = new ServiceCollection();
            services.AddLogging(builder => builder.AddDebug());
            services
                .AddDataProtection()
                .SetApplicationName("FocusBot")
                .PersistKeysToFileSystem(new DirectoryInfo(keysPath));
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
                    sp.GetRequiredService<ISettingsService>(),
                    sp.GetRequiredService<ILogger<FocusBotApiClient>>()
                );
            });
            services.AddSingleton<ISessionCoordinator, SessionCoordinator>();
            services.AddSingleton<ISessionRealtimeAdapter>(sp =>
            {
                var hubUrl = $"{GetFocusBotApiBaseUrl()}/hubs/focus";
                return new SignalRSessionRealtimeAdapter(
                    sp.GetRequiredService<ISessionCoordinator>(),
                    sp.GetRequiredService<IAuthService>(),
                    sp.GetRequiredService<ISettingsService>(),
                    sp.GetRequiredService<ILogger<SignalRSessionRealtimeAdapter>>(),
                    hubUrl
                );
            });
            services.AddScoped<IClassificationService, AlignmentClassificationService>();
            services.AddSingleton<INavigationService, MainWindowNavigationService>();

            services.AddTransient<ApiKeySettingsViewModel>();
            services.AddSingleton<OverlaySettingsViewModel>();
            services.AddTransient<PlanSelectionViewModel>();
            services.AddTransient<SettingsViewModel>();
            services.AddTransient<NewSessionViewModel>();
            services.AddTransient<SessionPageViewModel>();

            _services = services.BuildServiceProvider();
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

            await ActivateAndShowChromeAsync();

            await sessionPageViewModel.InitializeAsync();
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

            var authService = _services.GetRequiredService<IAuthService>();
            // Ensure the backend user row exists before any feature calls.
            // /auth/me uses get-or-create, so this is safe on every sign-in and session restore.
            var apiClient = _services.GetRequiredService<IFocusBotApiClient>();
            var realtimeAdapter = _services.GetRequiredService<ISessionRealtimeAdapter>();
            var coordinator = _services.GetRequiredService<ISessionCoordinator>();

            if (!authService.IsAuthenticated)
            {
                await realtimeAdapter.DisconnectAsync();
                coordinator.Reset();
                return;
            }

            var me = await apiClient.GetUserInfoAsync();
            if (me is null)
            {
                var logger = _services.GetRequiredService<ILogger<App>>();
                logger.LogWarning(
                    "Backend user provisioning failed; cloud features may be unavailable"
                );
            }

            await realtimeAdapter.ConnectAsync();
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

        private void OnXamlUnhandledException(
            object sender,
            Microsoft.UI.Xaml.UnhandledExceptionEventArgs e
        )
        {
            // Setting Handled = true prevents the WinRT runtime from escalating this
            // to a native 0xC000027B fatal crash, giving us a chance to log the real cause.
            e.Handled = true;
            ShowExceptionMessage("XAML unhandled exception", e.Exception);
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
