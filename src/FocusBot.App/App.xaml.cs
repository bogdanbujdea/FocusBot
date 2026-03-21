using System.Runtime.InteropServices;
using FocusBot.App.ViewModels;
using FocusBot.App.Views;
using FocusBot.Core.Events;
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
        private FocusOverlayWindow? _overlayWindow;
        private readonly IServiceProvider? _services;
        private FocusPageViewModel? _viewModel;
        private IIntegrationService? _integrationService;
        private Timer? _heartbeatTimer;

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
            services.AddScoped<ISessionRepository, SessionRepository>();
            services.AddScoped<IAlignmentCacheRepository, AlignmentCacheRepository>();
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
            services.AddSingleton<ILocalSessionTracker, LocalSessionTracker>();
            services.AddSingleton<IDeviceService, DesktopDeviceService>();
            services.AddSingleton<IPlanService, PlanService>();
            services.AddSingleton<INavigationService, MainWindowNavigationService>();
            services.AddSingleton<IIntegrationService, WebSocketIntegrationService>();
            services.AddTransient<FocusPageViewModel>();
            services.AddTransient<ApiKeySettingsViewModel>();
            services.AddSingleton<OverlaySettingsViewModel>();
            services.AddTransient<PlanSelectionViewModel>();
            services.AddTransient<SettingsViewModel>();

            _services = services.BuildServiceProvider();

            using var scope = _services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            db.Database.Migrate();
        }

        protected override void OnLaunched(Microsoft.UI.Xaml.LaunchActivatedEventArgs args)
        {
            var activationArgs = AppInstance.GetCurrent().GetActivatedEventArgs();
            HandleActivation(activationArgs);

            _viewModel = _services!.GetRequiredService<FocusPageViewModel>();
            var navigationService = _services!.GetRequiredService<INavigationService>();
            _integrationService = _services!.GetRequiredService<IIntegrationService>();
            _window = new MainWindow(_viewModel);
            if (navigationService is MainWindowNavigationService mainNav)
                mainNav.SetWindow(_window);

            var uiDispatcher = _services!.GetRequiredService<AppUIThreadDispatcher>();
            uiDispatcher.DispatcherQueue = _window.DispatcherQueue;

            _ = _integrationService.StartAsync();

            var auth = _services!.GetRequiredService<IAuthService>();

            // Restore session from stored tokens on every launch (not just protocol activations).
            _ = auth.TryRestoreSessionAsync();

            auth.AuthStateChanged += () => _ = OnAuthStateChangedAsync();
            auth.ReAuthRequired += OnReAuthRequired;
            _ = OnAuthStateChangedAsync();

            _window.Activate();

            try
            {
                _overlayWindow = new FocusOverlayWindow(navigationService);

                // Check initial overlay visibility setting
                var overlaySettings = _services!.GetRequiredService<OverlaySettingsViewModel>();
                overlaySettings.OverlayVisibilityChanged += OnOverlayVisibilityChanged;

                if (overlaySettings.IsOverlayEnabled)
                    _overlayWindow.Show();

                // Subscribe to ViewModel state changes
                _viewModel.FocusOverlayStateChanged += OnFocusOverlayStateChanged;
            }
            catch (Exception ex)
            {
                ShowExceptionMessage("Focus overlay failed", ex);
            }
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
            StopHeartbeat();

            if (_services is null)
                return;

            var navigationService = _services.GetRequiredService<INavigationService>();
            var dispatcher = _services.GetRequiredService<AppUIThreadDispatcher>();

            // Navigate on the UI thread so WinUI controls are not touched off-thread.
            dispatcher.DispatcherQueue?.TryEnqueue(() =>
            {
                _window?.Activate();
                navigationService.NavigateToSettings();
            });
        }

        private async Task OnAuthStateChangedAsync()
        {
            if (_services is null)
                return;

            var auth = _services.GetRequiredService<IAuthService>();
            if (!auth.IsAuthenticated)
            {
                StopHeartbeat();
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
            var plan = await planService.GetCurrentPlanAsync();

            if (!planService.IsCloudPlan(plan))
            {
                StopHeartbeat();
                return;
            }

            var deviceService = _services.GetRequiredService<IDeviceService>();
            if (deviceService.GetDeviceId() is null)
                await deviceService.RegisterAsync();

            StartHeartbeat(deviceService);
        }

        private void StartHeartbeat(IDeviceService deviceService)
        {
            if (_heartbeatTimer is not null)
                return;

            var logger = _services!.GetRequiredService<ILogger<App>>();
            var semaphore = new SemaphoreSlim(1, 1);

            _heartbeatTimer = new Timer(
                callback: _ =>
                {
                    // Skip this tick if the previous heartbeat is still in flight.
                    if (!semaphore.Wait(0))
                        return;

                    _ = SendHeartbeatSafeAsync(deviceService, semaphore, logger);
                },
                state: null,
                dueTime: TimeSpan.FromSeconds(60),
                period: TimeSpan.FromSeconds(60)
            );
        }

        private static async Task SendHeartbeatSafeAsync(
            IDeviceService deviceService,
            SemaphoreSlim semaphore,
            ILogger<App> logger
        )
        {
            try
            {
                await deviceService.SendHeartbeatAsync().ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "An error occurred while sending the device heartbeat.");
            }
            finally
            {
                semaphore.Release();
            }
        }

        private void StopHeartbeat()
        {
            _heartbeatTimer?.Dispose();
            _heartbeatTimer = null;
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

        private void OnFocusOverlayStateChanged(object? sender, FocusOverlayStateChangedEventArgs e)
        {
            _overlayWindow?.UpdateState(
                e.HasActiveSession,
                e.FocusScorePercent,
                e.Status,
                e.IsSessionPaused,
                e.IsLoading,
                e.HasError,
                e.TooltipText
            );
        }

        private void OnOverlayVisibilityChanged(object? sender, bool isVisible)
        {
            if (isVisible)
                _overlayWindow?.Show();
            else
                _overlayWindow?.Hide();
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
