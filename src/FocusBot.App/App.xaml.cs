using FocusBot.App.ViewModels;
using FocusBot.Core.Interfaces;
using FocusBot.Infrastructure.Data;
using FocusBot.Infrastructure.Services;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.UI.Xaml;
using Windows.Storage;

namespace FocusBot.App
{
    public partial class App
    {
        private Window? _window;
        private IServiceProvider? _services;

        public App()
        {
            InitializeComponent();

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
            services.AddSingleton<LlmService>();
            services.AddSingleton<ILlmService>(sp => new AlignmentClassificationCacheDecorator(
                sp.GetRequiredService<LlmService>(),
                sp.GetRequiredService<IServiceScopeFactory>()));
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
            var viewModel = _services!.GetRequiredService<KanbanBoardViewModel>();
            var navigationService = _services!.GetRequiredService<INavigationService>();
            _window = new MainWindow(viewModel, navigationService);
            if (navigationService is MainWindowNavigationService mainNav)
                mainNav.SetWindow(_window);
            _window.Activate();
        }
    }
}
