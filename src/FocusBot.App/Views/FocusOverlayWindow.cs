using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Drawing.Text;
using System.Runtime.InteropServices;
using FocusBot.App.ViewModels;
using FocusBot.Core.Events;

namespace FocusBot.App.Views;

/// <summary>
/// Pure Win32 layered window that displays a circular focus status indicator with anti-aliased edges.
/// Uses UpdateLayeredWindow with per-pixel alpha for smooth rendering.
/// Shows focus score percentage when a task is active, empty circle otherwise.
/// Hovering reveals a pause/play button to control AI classification.
/// Topmost, no title bar, draggable. Glows briefly when status changes.
/// </summary>
public sealed class FocusOverlayWindow : IDisposable
{
    private const int SizePx = 96;
    private const int GlowPadding = 8; // Extra pixels for glow effect
    private const int TotalSize = SizePx + GlowPadding * 2; // 112px total with glow
    private const byte OpacityNormal = 179; // 0.7 * 255
    private const byte OpacityHighlight = 255; // Full opacity when highlighted
    private const int HighlightDurationMs = 3000; // 3 seconds

    // Theme colors (RGB format for GDI+)
    private static readonly Color ColorFocused = Color.FromArgb(255, 0x22, 0xC5, 0x5E);    // Green #22C55E
    private static readonly Color ColorNeutral = Color.FromArgb(255, 0x8B, 0x5C, 0xF6);    // Purple #8B5CF6
    private static readonly Color ColorDistracted = Color.FromArgb(255, 0xF9, 0x73, 0x16); // Orange #F97316

    private static readonly uint WndClassAtom;
    private static readonly IntPtr HInstance;

    private IntPtr _hwnd;
    private bool _dragging;
    private int _dragStartX, _dragStartY;
    private int _windowX, _windowY;
    private GCHandle _gcHandle;

    private bool _hasActiveTask;
    private int _focusScorePercent;
    private FocusStatus _focusStatus = FocusStatus.Neutral;
    private FocusStatus? _previousStatus; // Track previous status to detect changes
    private Color _currentColor = ColorNeutral;
    private bool _isHighlighted; // True when showing glow effect
    private byte _currentOpacity = OpacityNormal;
    private System.Threading.Timer? _highlightTimer;

    private bool _isHovering; // True when mouse is over the overlay
    private bool _isTrackingMouse; // True when TrackMouseEvent is active
    private bool _isTaskPaused; // Pause state from ViewModel

    private readonly INavigationService? _navigationService;
    private readonly Action? _onPausePlayClicked;

    // Win32 constants
    private const uint WS_POPUP = 0x80000000;
    private const uint WS_VISIBLE = 0x10000000;
    private const uint WS_EX_LAYERED = 0x00080000;
    private const uint WS_EX_TOPMOST = 0x00000008;
    private const uint WS_EX_TOOLWINDOW = 0x00000080;
    private const uint WS_EX_NOACTIVATE = 0x08000000;

    private const int GWLP_USERDATA = -21;
    private const int ULW_ALPHA = 0x02;
    private const byte AC_SRC_OVER = 0x00;
    private const byte AC_SRC_ALPHA = 0x01;

    private const int WM_CREATE = 0x0001;
    private const int WM_DESTROY = 0x0002;
    private const int WM_LBUTTONDOWN = 0x0201;
    private const int WM_LBUTTONUP = 0x0202;
    private const int WM_MOUSEMOVE = 0x0200;
    private const int WM_MOUSELEAVE = 0x02A3;

    private const int TME_LEAVE = 0x00000002;

    private static readonly IntPtr HWND_TOPMOST = new(-1);
    private const uint SWP_NOACTIVATE = 0x0010;
    private const uint SWP_NOSIZE = 0x0001;
    private const uint SWP_NOMOVE = 0x0002;

    private static readonly IntPtr s_nullBrush;

    static FocusOverlayWindow()
    {
        HInstance = GetModuleHandleW(null);
        s_nullBrush = GetStockObject(5); // NULL_BRUSH

        var wc = new WNDCLASSEXW
        {
            cbSize = (uint)Marshal.SizeOf<WNDCLASSEXW>(),
            style = 0,
            lpfnWndProc = Marshal.GetFunctionPointerForDelegate(s_wndProcDelegate),
            hInstance = HInstance,
            hCursor = LoadCursor(IntPtr.Zero, 0x7F00),
            hbrBackground = s_nullBrush,
            lpszClassName = "FocusOverlayWindow"
        };
        WndClassAtom = RegisterClassExW(ref wc);
        if (WndClassAtom == 0)
            throw new InvalidOperationException("RegisterClassEx failed: " + Marshal.GetLastWin32Error());
    }

    public FocusOverlayWindow(INavigationService? navigationService = null, Action? onPausePlayClicked = null)
    {
        _navigationService = navigationService;
        _onPausePlayClicked = onPausePlayClicked;
        GetInitialPosition(out _windowX, out _windowY);
        _gcHandle = GCHandle.Alloc(this);
        _hwnd = CreateWindowExW(
            WS_EX_LAYERED | WS_EX_TOPMOST | WS_EX_TOOLWINDOW | WS_EX_NOACTIVATE,
            "FocusOverlayWindow",
            "",
            WS_POPUP,
            _windowX, _windowY,
            TotalSize, TotalSize,
            IntPtr.Zero,
            IntPtr.Zero,
            HInstance,
            GCHandle.ToIntPtr(_gcHandle));
        if (_hwnd == IntPtr.Zero)
        {
            _gcHandle.Free();
            throw new InvalidOperationException("CreateWindowEx failed: " + Marshal.GetLastWin32Error());
        }

        SetWindowPos(_hwnd, HWND_TOPMOST, 0, 0, 0, 0, SWP_NOMOVE | SWP_NOSIZE | SWP_NOACTIVATE);

        // Initial render
        UpdateLayeredBitmap();
    }

    public void Show()
    {
        ShowWindow(_hwnd, 5); // SW_SHOW
        UpdateLayeredBitmap();
    }

    public void Hide()
    {
        ShowWindow(_hwnd, 0); // SW_HIDE
    }

    /// <summary>
    /// Updates the overlay state and triggers a repaint.
    /// When status changes, shows a highlight effect for 3 seconds.
    /// </summary>
    public void UpdateState(bool hasActiveTask, int focusScorePercent, FocusStatus status, bool isTaskPaused = false)
    {
        _hasActiveTask = hasActiveTask;
        _focusScorePercent = focusScorePercent;
        _isTaskPaused = isTaskPaused;

        // Check if status actually changed (and we have a previous status to compare)
        var statusChanged = _previousStatus.HasValue && _previousStatus.Value != status && hasActiveTask;
        _previousStatus = status;

        _focusStatus = status;
        _currentColor = status switch
        {
            FocusStatus.Focused => ColorFocused,
            FocusStatus.Distracted => ColorDistracted,
            _ => ColorNeutral
        };

        // If status changed, start highlight effect
        if (statusChanged)
        {
            StartHighlight();
        }

        UpdateLayeredBitmap();
    }

    private void StartHighlight()
    {
        _isHighlighted = true;
        _currentOpacity = OpacityHighlight;

        // Cancel any existing timer and start a new one
        _highlightTimer?.Dispose();
        _highlightTimer = new System.Threading.Timer(OnHighlightTimerElapsed, null, HighlightDurationMs, Timeout.Infinite);
    }

    private void OnHighlightTimerElapsed(object? state)
    {
        _isHighlighted = false;
        _currentOpacity = OpacityNormal;
        UpdateLayeredBitmap();
    }

    /// <summary>
    /// Renders the overlay to a 32-bit ARGB bitmap and updates the layered window.
    /// This provides smooth anti-aliased edges via per-pixel alpha.
    /// </summary>
    private void UpdateLayeredBitmap()
    {
        if (_hwnd == IntPtr.Zero)
            return;

        using var bitmap = new Bitmap(TotalSize, TotalSize, PixelFormat.Format32bppPArgb);
        using (var graphics = Graphics.FromImage(bitmap))
        {
            graphics.SmoothingMode = SmoothingMode.AntiAlias;
            graphics.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;
            graphics.Clear(Color.Transparent);

            // Draw glow effect when highlighted
            if (_isHighlighted)
            {
                // Draw multiple concentric rings with decreasing opacity for glow effect
                for (int i = GlowPadding; i > 0; i -= 2)
                {
                    var glowAlpha = (int)(120 * (1.0 - (float)i / GlowPadding)); // Fade out toward edges
                    using var glowBrush = new SolidBrush(Color.FromArgb(glowAlpha, _currentColor));
                    var offset = GlowPadding - i;
                    var glowSize = SizePx + i * 2;
                    graphics.FillEllipse(glowBrush, offset, offset, glowSize, glowSize);
                }
            }

            // Fill main circle with current status color (centered with glow padding)
            using var brush = new SolidBrush(_currentColor);
            graphics.FillEllipse(brush, GlowPadding, GlowPadding, SizePx, SizePx);

            // Draw content based on state:
            // - Paused + not hovering: show pause icon (current state)
            // - Paused + hovering: show play icon (action to take)
            // - Running + not hovering: show score
            // - Running + hovering: show pause icon (action to take)
            if (_hasActiveTask)
            {
                if (_isTaskPaused)
                {
                    // Task is paused - show pause icon normally, play icon on hover
                    if (_isHovering)
                        DrawPlayButton(graphics);
                    else
                        DrawPauseButton(graphics);
                }
                else if (_isHovering)
                {
                    // Running + hovering: show pause button
                    DrawPauseButton(graphics);
                }
                else
                {
                    // Running + not hovering: show score
                    var scoreText = _focusScorePercent.ToString();
                    using var font = new Font("Segoe UI", 26, FontStyle.Bold, GraphicsUnit.Pixel);
                    using var textBrush = new SolidBrush(Color.White);

                    var textSize = graphics.MeasureString(scoreText, font);
                    var textX = GlowPadding + (SizePx - textSize.Width) / 2;
                    var textY = GlowPadding + (SizePx - textSize.Height) / 2;
                    graphics.DrawString(scoreText, font, textBrush, textX, textY);
                }
            }
        }

        // Update the layered window with the bitmap
        var hdcScreen = GetDC(IntPtr.Zero);
        var hdcMem = CreateCompatibleDC(hdcScreen);
        var hBitmap = bitmap.GetHbitmap(Color.FromArgb(0));
        var hOldBitmap = SelectObject(hdcMem, hBitmap);

        var blend = new BLENDFUNCTION
        {
            BlendOp = AC_SRC_OVER,
            BlendFlags = 0,
            SourceConstantAlpha = _currentOpacity,
            AlphaFormat = AC_SRC_ALPHA
        };

        var ptSrc = new POINT { x = 0, y = 0 };
        var size = new SIZE { cx = TotalSize, cy = TotalSize };
        var ptDst = new POINT { x = _windowX, y = _windowY };

        UpdateLayeredWindow(_hwnd, hdcScreen, ref ptDst, ref size, hdcMem, ref ptSrc, 0, ref blend, ULW_ALPHA);

        SelectObject(hdcMem, hOldBitmap);
        DeleteObject(hBitmap);
        DeleteDC(hdcMem);
        ReleaseDC(IntPtr.Zero, hdcScreen);
    }

    /// <summary>
    /// Draws a pause button (two vertical bars) in the center of the overlay.
    /// </summary>
    private void DrawPauseButton(Graphics graphics)
    {
        var centerX = GlowPadding + SizePx / 2;
        var centerY = GlowPadding + SizePx / 2;
        const int iconSize = 32;
        const int barWidth = 8;
        const int barGap = 6;

        using var iconBrush = new SolidBrush(Color.White);

        var barHeight = iconSize;
        var leftBarX = centerX - barGap / 2 - barWidth;
        var rightBarX = centerX + barGap / 2;
        var barY = centerY - barHeight / 2;

        graphics.FillRectangle(iconBrush, leftBarX, barY, barWidth, barHeight);
        graphics.FillRectangle(iconBrush, rightBarX, barY, barWidth, barHeight);
    }

    /// <summary>
    /// Draws a play button (triangle pointing right) in the center of the overlay.
    /// </summary>
    private void DrawPlayButton(Graphics graphics)
    {
        var centerX = GlowPadding + SizePx / 2;
        var centerY = GlowPadding + SizePx / 2;
        const int iconSize = 32;

        using var iconBrush = new SolidBrush(Color.White);

        var trianglePoints = new PointF[]
        {
            new(centerX - iconSize / 3, centerY - iconSize / 2),
            new(centerX - iconSize / 3, centerY + iconSize / 2),
            new(centerX + iconSize / 2, centerY)
        };
        graphics.FillPolygon(iconBrush, trianglePoints);
    }

    /// <summary>
    /// Positions the overlay in the bottom-right of the primary screen work area
    /// (above the taskbar), with a small margin. Accounts for glow padding.
    /// </summary>
    private static void GetInitialPosition(out int x, out int y)
    {
        const int marginPx = 16;
        if (SystemParametersInfoW(0x0030, 0, out var workArea, 0)) // SPI_GETWORKAREA
        {
            // Position so the visible circle (not glow area) is at the margin
            x = workArea.Right - SizePx - marginPx - GlowPadding;
            y = workArea.Bottom - SizePx - marginPx - GlowPadding;
        }
        else
        {
            x = 100;
            y = 100;
        }
    }

    private static nint WndProc(IntPtr hwnd, uint msg, nuint wParam, nint lParam)
    {
        FocusOverlayWindow? w = null;
        if (msg == WM_CREATE)
        {
            var cs = Marshal.PtrToStructure<CREATESTRUCT>(lParam);
            try
            {
                w = (FocusOverlayWindow?)GCHandle.FromIntPtr(cs.lpCreateParams).Target;
                if (w != null)
                    SetWindowLongPtr(hwnd, GWLP_USERDATA, cs.lpCreateParams);
            }
            catch
            {
                // ignore invalid handle during create
            }
        }
        else
        {
            var userData = GetWindowLongPtr(hwnd, GWLP_USERDATA);
            if (userData != IntPtr.Zero)
            {
                try
                {
                    w = (FocusOverlayWindow?)GCHandle.FromIntPtr(userData).Target;
                }
                catch
                {
                    // ignore invalid handle
                }
            }
        }
        if (w != null)
            return w.HandleMessage(hwnd, msg, wParam, lParam);
        return DefWindowProcW(hwnd, msg, wParam, lParam);
    }

    private nint HandleMessage(IntPtr hwnd, uint msg, nuint wParam, nint lParam)
    {
        switch (msg)
        {
            case WM_LBUTTONDOWN:
                _dragging = true;
                _dragStartX = (short)(lParam & 0xFFFF);
                _dragStartY = (short)((lParam >> 16) & 0xFFFF);
                SetCapture(hwnd);
                return 0;

            case WM_LBUTTONUP:
                if (_dragging)
                {
                    _dragging = false;
                    ReleaseCapture();

                    // Check if it was a click (not a drag)
                    var x = (short)(lParam & 0xFFFF);
                    var y = (short)((lParam >> 16) & 0xFFFF);
                    var dx = Math.Abs(x - _dragStartX);
                    var dy = Math.Abs(y - _dragStartY);
                    if (dx < 5 && dy < 5)
                    {
                        // If hovering with active task, toggle pause/play
                        if (_isHovering && _hasActiveTask)
                        {
                            _onPausePlayClicked?.Invoke();
                        }
                        else
                        {
                            // Clicked without dragging - activate main window
                            _navigationService?.ActivateMainWindow();
                        }
                    }
                }
                return 0;

            case WM_MOUSEMOVE:
                if (_dragging)
                {
                    var x = (short)(lParam & 0xFFFF);
                    var y = (short)((lParam >> 16) & 0xFFFF);
                    var dx = x - _dragStartX;
                    var dy = y - _dragStartY;
                    _windowX += dx;
                    _windowY += dy;
                    UpdateLayeredBitmap(); // Re-render at new position
                }
                else
                {
                    // Track mouse for hover detection
                    if (!_isTrackingMouse)
                    {
                        var tme = new TRACKMOUSEEVENT
                        {
                            cbSize = (uint)Marshal.SizeOf<TRACKMOUSEEVENT>(),
                            dwFlags = TME_LEAVE,
                            hwndTrack = hwnd,
                            dwHoverTime = 0
                        };
                        TrackMouseEvent(ref tme);
                        _isTrackingMouse = true;
                    }

                    if (!_isHovering)
                    {
                        _isHovering = true;
                        UpdateLayeredBitmap(); // Show pause/play button
                    }
                }
                return 0;

            case WM_MOUSELEAVE:
                _isTrackingMouse = false;
                if (_isHovering)
                {
                    _isHovering = false;
                    UpdateLayeredBitmap(); // Hide pause/play button, show score
                }
                return 0;

            case WM_DESTROY:
                _gcHandle.Free();
                return 0;
        }
        return DefWindowProcW(hwnd, msg, wParam, lParam);
    }

    public void Dispose()
    {
        _highlightTimer?.Dispose();
        _highlightTimer = null;

        if (_hwnd != IntPtr.Zero)
        {
            DestroyWindow(_hwnd);
            _hwnd = IntPtr.Zero;
        }
    }

    #region Win32 Interop

    [StructLayout(LayoutKind.Sequential)]
    private struct POINT
    {
        public int x, y;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct SIZE
    {
        public int cx, cy;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct BLENDFUNCTION
    {
        public byte BlendOp;
        public byte BlendFlags;
        public byte SourceConstantAlpha;
        public byte AlphaFormat;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct RECT
    {
        public int Left, Top, Right, Bottom;
    }

    private delegate nint WndProcDelegate(IntPtr hWnd, uint msg, nuint wParam, nint lParam);

    private static readonly WndProcDelegate s_wndProcDelegate = WndProc;

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct WNDCLASSEXW
    {
        public uint cbSize;
        public uint style;
        public IntPtr lpfnWndProc;
        public int cbClsExtra;
        public int cbWndExtra;
        public IntPtr hInstance;
        public IntPtr hIcon;
        public IntPtr hCursor;
        public IntPtr hbrBackground;
        public string? lpszMenuName;
        public string lpszClassName;
        public IntPtr hIconSm;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct CREATESTRUCT
    {
        public IntPtr lpCreateParams;
        public IntPtr hInstance;
        public IntPtr hMenu;
        public IntPtr hwndParent;
        public int cy, cx, y, x;
        public int style;
        public IntPtr lpszName;
        public IntPtr lpszClass;
        public uint dwExStyle;
    }

    [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern IntPtr CreateWindowExW(uint dwExStyle, string lpClassName, string lpWindowName,
        uint dwStyle, int x, int y, int nWidth, int nHeight,
        IntPtr hWndParent, IntPtr hMenu, IntPtr hInstance, IntPtr lpParam);

    [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern uint RegisterClassExW(ref WNDCLASSEXW lpwcx);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern nint DefWindowProcW(IntPtr hWnd, uint Msg, nuint wParam, nint lParam);

    [DllImport("user32.dll")]
    private static extern bool DestroyWindow(IntPtr hWnd);

    [DllImport("user32.dll")]
    private static extern bool UpdateLayeredWindow(IntPtr hwnd, IntPtr hdcDst, ref POINT pptDst, ref SIZE psize,
        IntPtr hdcSrc, ref POINT pptSrc, uint crKey, ref BLENDFUNCTION pblend, int dwFlags);

    [DllImport("user32.dll")]
    private static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int X, int Y, int cx, int cy, uint uFlags);

    [DllImport("user32.dll")]
    private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

    [DllImport("user32.dll")]
    private static extern IntPtr LoadCursor(IntPtr hInstance, int lpCursorName);

    [DllImport("user32.dll")]
    private static extern IntPtr GetWindowLongPtr(IntPtr hWnd, int nIndex);

    [DllImport("user32.dll")]
    private static extern IntPtr SetWindowLongPtr(IntPtr hWnd, int nIndex, IntPtr dwNewLong);

    [DllImport("user32.dll")]
    private static extern IntPtr SetCapture(IntPtr hWnd);

    [DllImport("user32.dll")]
    private static extern bool ReleaseCapture();

    [DllImport("user32.dll")]
    private static extern bool TrackMouseEvent(ref TRACKMOUSEEVENT lpEventTrack);

    [StructLayout(LayoutKind.Sequential)]
    private struct TRACKMOUSEEVENT
    {
        public uint cbSize;
        public uint dwFlags;
        public IntPtr hwndTrack;
        public uint dwHoverTime;
    }

    [DllImport("user32.dll")]
    private static extern IntPtr GetDC(IntPtr hWnd);

    [DllImport("user32.dll")]
    private static extern int ReleaseDC(IntPtr hWnd, IntPtr hDC);

    [DllImport("gdi32.dll")]
    private static extern IntPtr CreateCompatibleDC(IntPtr hdc);

    [DllImport("gdi32.dll")]
    private static extern IntPtr SelectObject(IntPtr hdc, IntPtr hgdiobj);

    [DllImport("gdi32.dll")]
    private static extern bool DeleteObject(IntPtr hObject);

    [DllImport("gdi32.dll")]
    private static extern bool DeleteDC(IntPtr hdc);

    [DllImport("gdi32.dll")]
    private static extern IntPtr GetStockObject(int fnObject);

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
    private static extern IntPtr GetModuleHandleW(string? lpModuleName);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool SystemParametersInfoW(uint uiAction, uint uiParam, out RECT pvParam, uint fWinIni);

    #endregion
}
