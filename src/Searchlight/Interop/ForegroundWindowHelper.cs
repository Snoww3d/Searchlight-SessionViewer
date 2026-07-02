using System.Runtime.InteropServices;
using Microsoft.UI.Xaml;

namespace Searchlight.Interop;

/// <summary>
/// Forces a WinUI 3 window to the foreground on top of all other windows.
/// <see cref="Window.Activate"/> alone is unreliable at this — especially when the
/// process is launched externally (Start-Process, tray relaunch) — because the OS
/// foreground-lock keeps the newly created window behind the caller's window. The
/// robust workaround is to raise the window to the top of the normal (non-topmost)
/// z-order via <c>HWND_TOP</c> and then call SetForegroundWindow. We deliberately
/// avoid the <c>HWND_TOPMOST</c> band so the window comes to the front once without
/// staying pinned above every other window.
/// </summary>
internal static partial class ForegroundWindowHelper
{
    // ShowWindow command: restore a minimized/hidden window to its normal state.
    private const int SW_RESTORE = 9;

    // HWND_TOP places the window at the top of the normal z-order WITHOUT adding
    // the WS_EX_TOPMOST always-on-top flag. (HWND_TOPMOST(-1) would pin it.)
    private static readonly nint HWND_TOP = 0;

    // SetWindowPos flags: keep current position (NOMOVE) and size (NOSIZE),
    // and make the window visible (SHOWWINDOW) during the z-order change.
    private const uint SWP_NOSIZE = 0x0001;
    private const uint SWP_NOMOVE = 0x0002;
    private const uint SWP_SHOWWINDOW = 0x0040;

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool SetForegroundWindow(nint hWnd);

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool ShowWindow(nint hWnd, int nCmdShow);

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool SetWindowPos(
        nint hWnd, nint hWndInsertAfter, int x, int y, int cx, int cy, uint uFlags);

    [LibraryImport("user32.dll")]
    private static partial uint GetDpiForWindow(nint hWnd);

    /// <summary>
    /// Brings <paramref name="window"/> to the front and gives it foreground focus,
    /// WITHOUT leaving it always-on-top. Safe to call multiple times (launch + every
    /// tray "show").
    /// </summary>
    public static void BringToFront(Window window)
    {
        nint hwnd = WinRT.Interop.WindowNative.GetWindowHandle(window);
        if (hwnd == 0)
        {
            return;
        }

        // Restore in case the window was minimized.
        ShowWindow(hwnd, SW_RESTORE);

        // Raise to the top of the normal z-order (not the topmost band), so the
        // window surfaces above the launcher once but does not stay pinned.
        const uint flags = SWP_NOMOVE | SWP_NOSIZE | SWP_SHOWWINDOW;
        SetWindowPos(hwnd, HWND_TOP, 0, 0, 0, 0, flags);

        SetForegroundWindow(hwnd);
    }

    /// <summary>
    /// Resizes <paramref name="window"/> to the given <paramref name="logicalWidth"/>
    /// and <paramref name="logicalHeight"/> (in device-independent pixels), scaling to
    /// physical pixels for the window's current DPI. <see cref="AppWindow.Resize"/>
    /// expects physical pixels, so on a 150% display 1140 logical px becomes 1710.
    /// </summary>
    public static void ResizeLogical(Window window, int logicalWidth, int logicalHeight)
    {
        nint hwnd = WinRT.Interop.WindowNative.GetWindowHandle(window);
        if (hwnd == 0)
        {
            return;
        }

        uint dpi = GetDpiForWindow(hwnd);
        double scale = dpi == 0 ? 1.0 : dpi / 96.0;

        int physicalWidth = (int)System.Math.Round(logicalWidth * scale);
        int physicalHeight = (int)System.Math.Round(logicalHeight * scale);

        window.AppWindow.Resize(new Windows.Graphics.SizeInt32(physicalWidth, physicalHeight));
    }
}
