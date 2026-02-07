using System;
using System.Runtime.InteropServices;
using System.Text;
using Microsoft.UI.Xaml;

namespace FocusBot.App
{
    public sealed partial class MainWindow : Window
    {
        [DllImport("user32.dll")]
        private static extern IntPtr GetForegroundWindow();

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        private static extern int GetWindowText(IntPtr hWnd, StringBuilder text, int count);

        private readonly Microsoft.UI.Dispatching.DispatcherQueueTimer _titleTimer;

        public MainWindow()
        {
            InitializeComponent();
            _titleTimer = DispatcherQueue.CreateTimer();
            _titleTimer.Interval = TimeSpan.FromSeconds(0.5);
            _titleTimer.Tick += OnTitleTimerTick;
            _titleTimer.Start();
            UpdateForegroundWindowTitle();
        }

        private void OnTitleTimerTick(
            Microsoft.UI.Dispatching.DispatcherQueueTimer sender,
            object args
        )
        {
            UpdateForegroundWindowTitle();
        }

        private void UpdateForegroundWindowTitle()
        {
            const int maxChars = 512;
            var sb = new StringBuilder(maxChars);
            IntPtr hWnd = GetForegroundWindow();
            int len = GetWindowText(hWnd, sb, maxChars);
            ForegroundWindowTitleBox.Text = len > 0 ? sb.ToString() : string.Empty;
        }
    }
}
