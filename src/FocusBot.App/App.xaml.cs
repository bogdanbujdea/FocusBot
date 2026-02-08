using FocusBot.App.ViewModels;
using FocusBot.Core.Interfaces;
using FocusBot.Infrastructure.Data;
using FocusBot.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;

namespace FocusBot.App
{
    public partial class App
    {
        private Window? _window;
        private IServiceProvider? _services;

        public App()
        {
            InitializeComponent();
            var dataPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "FocusBot",
                "focusbot.db"
            );
            var dir = Path.GetDirectoryName(dataPath);
            if (!string.IsNullOrEmpty(dir))
                Directory.CreateDirectory(dir);

            _services = new ServiceCollection()
                .AddDbContext<AppDbContext>(o => o.UseSqlite($"Data Source={dataPath}"))
                .AddScoped<ITaskRepository, TaskRepository>()
                .AddSingleton<IWindowMonitorService, WindowMonitorService>()
                .AddTransient<KanbanBoardViewModel>()
                .BuildServiceProvider();

            using var scope = _services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            db.Database.Migrate();
        }

        protected override void OnLaunched(LaunchActivatedEventArgs args)
        {
            var viewModel = _services!.GetRequiredService<KanbanBoardViewModel>();
            _window = new MainWindow(viewModel);
            _window.Activate();
        }
    }
}
