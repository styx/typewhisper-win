using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;

namespace TypeWhisper.Windows.Native;

internal static partial class WindowStyling
{
    private const int GWL_EXSTYLE      = -20;
    private const int WS_EX_NOACTIVATE = 0x08000000;
    private const int WS_EX_TOOLWINDOW = 0x00000080;

    private const uint MONITOR_DEFAULTTONEAREST = 2;

    [LibraryImport("user32.dll")]
    private static partial int GetWindowLongW(IntPtr hWnd, int nIndex);

    [LibraryImport("user32.dll")]
    private static partial int SetWindowLongW(IntPtr hWnd, int nIndex, int dwNewLong);

    [LibraryImport("user32.dll")]
    private static partial IntPtr MonitorFromPoint(POINT pt, uint dwFlags);

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool GetMonitorInfoW(IntPtr hMonitor, ref MonitorInfo lpmi);

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool GetCursorPos(out POINT lpPoint);

    [StructLayout(LayoutKind.Sequential)]
    private struct POINT { public int X, Y; }

    [StructLayout(LayoutKind.Sequential)]
    private struct RECT { public int Left, Top, Right, Bottom; }

    [StructLayout(LayoutKind.Sequential)]
    private struct MonitorInfo
    {
        public int cbSize;
        public RECT rcMonitor;
        public RECT rcWork;
        public uint dwFlags;
    }

    /// <summary>
    /// Applies WS_EX_NOACTIVATE | WS_EX_TOOLWINDOW to the window so it never
    /// steals focus and stays off the taskbar / Alt+Tab list.
    /// Must be called from <see cref="Window.OnSourceInitialized"/>.
    /// </summary>
    internal static void ApplyOverlayStyle(Window window)
    {
        var hwnd    = new WindowInteropHelper(window).Handle;
        var exStyle = GetWindowLongW(hwnd, GWL_EXSTYLE);
        SetWindowLongW(hwnd, GWL_EXSTYLE, exStyle | WS_EX_NOACTIVATE | WS_EX_TOOLWINDOW);
    }

    /// <summary>
    /// Returns the work-area of the monitor that currently contains the cursor,
    /// expressed in WPF device-independent pixels relative to <paramref name="visual"/>.
    /// Falls back to <see cref="SystemParameters.WorkArea"/> when monitor info is unavailable.
    /// </summary>
    internal static Rect GetMonitorWorkAreaForCursor(Visual visual)
    {
        GetCursorPos(out var cursor);
        var hMonitor = MonitorFromPoint(cursor, MONITOR_DEFAULTTONEAREST);

        var mi = new MonitorInfo { cbSize = Marshal.SizeOf<MonitorInfo>() };
        if (!GetMonitorInfoW(hMonitor, ref mi))
            return SystemParameters.WorkArea;

        var source    = PresentationSource.FromVisual(visual);
        var dpiToWpfX = source?.CompositionTarget?.TransformFromDevice.M11 ?? 1.0;
        var dpiToWpfY = source?.CompositionTarget?.TransformFromDevice.M22 ?? 1.0;

        return new Rect(
            mi.rcWork.Left   * dpiToWpfX,
            mi.rcWork.Top    * dpiToWpfY,
            (mi.rcWork.Right  - mi.rcWork.Left) * dpiToWpfX,
            (mi.rcWork.Bottom - mi.rcWork.Top)  * dpiToWpfY);
    }
}
