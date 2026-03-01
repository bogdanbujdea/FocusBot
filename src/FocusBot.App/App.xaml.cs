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
using Windows.Services.Store;
using Windows.Storage;

namespace FocusBot.App
{
    public partial class App
    {
        private Window? _window;
        private FocusOverlayWindow? _overlayWindow;
        private IServiceProvider? _services;
        private KanbanBoardViewModel? _viewModel;

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
            services.AddScoped<ITaskRepository, TaskRepository>();
            services.AddScoped<IAlignmentCacheRepository, AlignmentCacheRepository>();
            services.AddSingleton<ISettingsService>(sp => new SettingsService(
                sp.GetRequiredService<IDataProtectionProvider>(),
                sp.GetRequiredService<ILogger<SettingsService>>(),
                appDataRoot
            ));
            services.AddSingleton<IWindowMonitorService, WindowMonitorService>();
            services.AddSingleton<ITimeTrackingService, TimeTrackingService>();
            services.AddSingleton<IIdleDetectionService, IdleDetectionService>();
            services.AddSingleton<StoreContextHolder>();
            services.AddSingleton<AppUIThreadDispatcher>();
            services.AddSingleton<IUIThreadDispatcher>(sp =>
                sp.GetRequiredService<AppUIThreadDispatcher>()
            );

#if DEBUG
            services.AddSingleton<ISubscriptionService, MockSubscriptionService>();
#else
            services.AddSingleton<ISubscriptionService, SubscriptionService>();
#endif
            services.AddSingleton<IManagedKeyProvider, EmbeddedManagedKeyProvider>();
            services.AddSingleton<LlmService>();
            services.AddSingleton<ILlmService>(sp => new AlignmentClassificationCacheDecorator(
                sp.GetRequiredService<LlmService>(),
                sp.GetRequiredService<IServiceScopeFactory>()
            ));
            services.AddSingleton<INavigationService, MainWindowNavigationService>();
            services.AddSingleton<IFocusScoreService, FocusScoreService>();
            services.AddTransient<KanbanBoardViewModel>();
            services.AddTransient<ApiKeySettingsViewModel>();
            services.AddTransient<SettingsViewModel>();

            _services = services.BuildServiceProvider();

            using var scope = _services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            db.Database.Migrate();
        }

        protected override void OnLaunched(LaunchActivatedEventArgs args)
        {
            _viewModel = _services!.GetRequiredService<KanbanBoardViewModel>();
            var navigationService = _services!.GetRequiredService<INavigationService>();
            _window = new MainWindow(_viewModel, navigationService);
            if (navigationService is MainWindowNavigationService mainNav)
                mainNav.SetWindow(_window);

            var contextHolder = _services!.GetRequiredService<StoreContextHolder>();
            var uiDispatcher = _services!.GetRequiredService<AppUIThreadDispatcher>();
            uiDispatcher.DispatcherQueue = _window.DispatcherQueue;
            var storeContext = StoreContext.GetDefault();
            var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(_window);
            WinRT.Interop.InitializeWithWindow.Initialize(storeContext, hwnd);
            contextHolder.Context = storeContext;

            _window.Activate();

            try
            {
                _overlayWindow = new FocusOverlayWindow(navigationService);
                _overlayWindow.Show();

                // Subscribe to ViewModel state changes
                _viewModel.FocusOverlayStateChanged += OnFocusOverlayStateChanged;
            }
            catch (Exception ex)
            {
                ShowExceptionMessage("Focus overlay failed", ex);
            }
        }

        private void OnFocusOverlayStateChanged(object? sender, FocusOverlayStateChangedEventArgs e)
        {
            _overlayWindow?.UpdateState(e.HasActiveTask, e.FocusScorePercent, e.Status);
        }

        private static void OnUnhandledException(object? sender, System.UnhandledExceptionEventArgs e)
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
    }
}
