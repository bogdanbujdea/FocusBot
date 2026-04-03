using FocusBot.Core.Interfaces;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace FocusBot.App
{
    public sealed partial class MainWindow : Window
    {
        private readonly IWindowMonitorService _windowMonitor;
        private readonly TextBlock _captionText;

        public MainWindow(IWindowMonitorService windowMonitor)
        {
            InitializeComponent();

            _windowMonitor = windowMonitor;
            _captionText = new TextBlock
            {
                Text = string.Empty,
                FontSize = 16,
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(24),
            };

            Content = new Grid { Children = { _captionText } };

            _windowMonitor.ForegroundWindowChanged += OnForegroundWindowChanged;
            _windowMonitor.Start();
            Closed += OnClosed;

            // Set default window size
            AppWindow.Resize(new Windows.Graphics.SizeInt32(1500, 1500));
        }

        private void OnForegroundWindowChanged(
            object? sender,
            FocusBot.Core.Events.ForegroundWindowChangedEventArgs e
        )
        {
            _captionText.Text = string.IsNullOrWhiteSpace(e.ProcessName)
                && string.IsNullOrWhiteSpace(e.WindowTitle)
                ? string.Empty
                : $"App: {e.ProcessName} | Window Title: {e.WindowTitle}";
        }

        private void OnClosed(object sender, WindowEventArgs args)
        {
            _windowMonitor.ForegroundWindowChanged -= OnForegroundWindowChanged;
            _windowMonitor.Stop();
            Closed -= OnClosed;
        }
    }
}
