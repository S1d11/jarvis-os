using System.Collections.Generic;

namespace Jarvis.Core.Services;

/// <summary>
/// Window management — list, focus, close, minimize, maximize, snap windows.
/// Uses platform-specific window enumeration (Win32 on Windows).
/// </summary>
public sealed class WindowService
{
    private readonly IWindowAccess _access;

    public WindowService(IWindowAccess access) => _access = access;

    public IReadOnlyList<WinInfo> ListWindows() => _access.EnumerateWindows();

    public bool FocusWindow(long hwnd) => _access.FocusWindow(hwnd);
    public bool CloseWindow(long hwnd) => _access.CloseWindow(hwnd);
    public bool MinimizeWindow(long hwnd) => _access.MinimizeWindow(hwnd);
    public bool MaximizeWindow(long hwnd) => _access.MaximizeWindow(hwnd);
    public bool RestoreWindow(long hwnd) => _access.RestoreWindow(hwnd);

    public bool SnapWindow(long hwnd, string direction)
    {
        var snap = direction.ToLowerInvariant() switch
        {
            "left" => SnapDirection.Left,
            "right" => SnapDirection.Right,
            "top" => SnapDirection.Top,
            "bottom" => SnapDirection.Bottom,
            "topleft" => SnapDirection.TopLeft,
            "topright" => SnapDirection.TopRight,
            "bottomleft" => SnapDirection.BottomLeft,
            "bottomright" => SnapDirection.BottomRight,
            "maximize" => SnapDirection.Maximize,
            _ => SnapDirection.Left,
        };
        return _access.SnapWindow(hwnd, snap);
    }
}

public interface IWindowAccess
{
    IReadOnlyList<WinInfo> EnumerateWindows();
    bool FocusWindow(long hwnd);
    bool CloseWindow(long hwnd);
    bool MinimizeWindow(long hwnd);
    bool MaximizeWindow(long hwnd);
    bool RestoreWindow(long hwnd);
    bool SnapWindow(long hwnd, SnapDirection direction);
}

public enum SnapDirection
{
    Left, Right, Top, Bottom,
    TopLeft, TopRight, BottomLeft, BottomRight,
    Maximize,
}

public sealed class WinInfo
{
    public long Hwnd { get; set; }
    public string Title { get; set; } = "";
    public string ProcessName { get; set; } = "";
    public bool IsMaximized { get; set; }
    public bool IsMinimized { get; set; }
}
