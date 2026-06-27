using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using Jarvis.Core.Services;

namespace Jarvis.Windows;

/// <summary>
/// Win32 window management — enumerate, focus, close, minimize, maximize, snap.
/// </summary>
[SupportedOSPlatform("windows")]
public sealed class WindowsWindowAccess : IWindowAccess
{
    // ── Win32 constants ─────────────────────────────────────
    private const int GW_OWNER = 4;
    private const uint GWL_STYLE = 0xFFFFFFF0;
    private const uint WS_VISIBLE = 0x10000000;
    private const uint WS_EX_TOOLWINDOW = 0x00000080;
    private const int SW_RESTORE = 9;
    private const int SW_MINIMIZE = 6;
    private const int SW_MAXIMIZE = 3;
    private const uint SWP_NOZORDER = 0x0004;
    private const uint SWP_NOACTIVATE = 0x0010;
    private const uint SWP_SHOWWINDOW = 0x0040;

    [DllImport("user32.dll")]
    private static extern bool EnumWindows(EnumWindowsProc lpEnumFunc, IntPtr lParam);
    private delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);

    [DllImport("user32.dll")]
    private static extern bool IsWindowVisible(IntPtr hWnd);

    [DllImport("user32.dll")]
    private static extern int GetWindowText(IntPtr hWnd, System.Text.StringBuilder lpString, int nMaxCount);

    [DllImport("user32.dll")]
    private static extern int GetWindowTextLength(IntPtr hWnd);

    [DllImport("user32.dll")]
    private static extern int GetWindowThreadProcessId(IntPtr hWnd, out int lpdwProcessId);

    [DllImport("user32.dll")]
    private static extern bool SetForegroundWindow(IntPtr hWnd);

    [DllImport("user32.dll")]
    private static extern bool DestroyWindow(IntPtr hWnd);

    [DllImport("user32.dll")]
    private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool PostMessage(IntPtr hWnd, int Msg, IntPtr wParam, IntPtr lParam);

    [DllImport("user32.dll")]
    private static extern bool GetWindowRect(IntPtr hWnd, out RECT lpRect);

    [DllImport("user32.dll")]
    private static extern bool MoveWindow(IntPtr hWnd, int x, int y, int nWidth, int nHeight, bool bRepaint);

    [DllImport("user32.dll")]
    private static extern IntPtr GetShellWindow();

    [DllImport("user32.dll")]
    private static extern bool IsIconic(IntPtr hWnd);

    [DllImport("user32.dll")]
    private static extern bool IsZoomed(IntPtr hWnd);

    [StructLayout(LayoutKind.Sequential)]
    private struct RECT { public int Left, Top, Right, Bottom; }

    public IReadOnlyList<WinInfo> EnumerateWindows()
    {
        var result = new List<WinInfo>();
        var shellWindow = GetShellWindow();

        EnumWindows((hWnd, _) =>
        {
            if (hWnd == shellWindow) return true;
            if (!IsWindowVisible(hWnd)) return true;

            var sb = new System.Text.StringBuilder(256);
            GetWindowText(hWnd, sb, sb.Capacity);
            var title = sb.ToString();
            if (string.IsNullOrEmpty(title)) return true;

            GetWindowThreadProcessId(hWnd, out var pid);
            var procName = "";
            try { procName = System.Diagnostics.Process.GetProcessById(pid).ProcessName; }
            catch { }

            result.Add(new WinInfo
            {
                Hwnd = hWnd.ToInt64(),
                Title = title,
                ProcessName = procName,
                IsMaximized = IsZoomed(hWnd),
                IsMinimized = IsIconic(hWnd),
            });
            return true;
        }, IntPtr.Zero);

        return result;
    }

    public bool FocusWindow(long hwnd)
    {
        try
        {
            var h = new IntPtr(hwnd);
            if (IsIconic(h)) ShowWindow(h, SW_RESTORE);
            return SetForegroundWindow(h);
        }
        catch { return false; }
    }

    public bool CloseWindow(long hwnd)
    {
        try { return PostMessage(new IntPtr(hwnd), 0x0010, IntPtr.Zero, IntPtr.Zero); } // WM_CLOSE
        catch { return false; }
    }

    public bool MinimizeWindow(long hwnd)
    {
        try { return ShowWindow(new IntPtr(hwnd), SW_MINIMIZE); }
        catch { return false; }
    }

    public bool MaximizeWindow(long hwnd)
    {
        try { return ShowWindow(new IntPtr(hwnd), SW_MAXIMIZE); }
        catch { return false; }
    }

    public bool RestoreWindow(long hwnd)
    {
        try { return ShowWindow(new IntPtr(hwnd), SW_RESTORE); }
        catch { return false; }
    }

    public bool SnapWindow(long hwnd, SnapDirection direction)
    {
        try
        {
            var h = new IntPtr(hwnd);
            var screen = System.Windows.SystemParameters.WorkArea;
            int x, y, w, h2;

            (x, y, w, h2) = direction switch
            {
                SnapDirection.Left => (0, 0, (int)(screen.Width / 2), (int)screen.Height),
                SnapDirection.Right => ((int)(screen.Width / 2), 0, (int)(screen.Width / 2), (int)screen.Height),
                SnapDirection.Top => (0, 0, (int)screen.Width, (int)(screen.Height / 2)),
                SnapDirection.Bottom => (0, (int)(screen.Height / 2), (int)screen.Width, (int)(screen.Height / 2)),
                SnapDirection.TopLeft => (0, 0, (int)(screen.Width / 2), (int)(screen.Height / 2)),
                SnapDirection.TopRight => ((int)(screen.Width / 2), 0, (int)(screen.Width / 2), (int)(screen.Height / 2)),
                SnapDirection.BottomLeft => (0, (int)(screen.Height / 2), (int)(screen.Width / 2), (int)(screen.Height / 2)),
                SnapDirection.BottomRight => ((int)(screen.Width / 2), (int)(screen.Height / 2), (int)(screen.Width / 2), (int)(screen.Height / 2)),
                SnapDirection.Maximize => (0, 0, (int)screen.Width, (int)screen.Height),
                _ => (0, 0, (int)screen.Width, (int)screen.Height),
            };

            if (IsIconic(h)) ShowWindow(h, SW_RESTORE);
            return MoveWindow(h, x, y, w, h2, true);
        }
        catch { return false; }
    }
}
