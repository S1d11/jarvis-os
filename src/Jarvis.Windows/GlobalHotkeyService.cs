using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;

namespace Jarvis.Windows;

/// <summary>
/// Registers a system-wide hotkey (Win+J by default) that summons the Jarvis orb
/// from anywhere — even when another app is fullscreen.
///
/// Uses Win32 RegisterHotKey/UnregisterHotKey via the hidden message window.
/// </summary>
public sealed class GlobalHotkeyService : IDisposable
{
    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool UnregisterHotKey(IntPtr hWnd, int id);

    // Modifiers
    private const uint MOD_ALT = 0x0001;
    private const uint MOD_CONTROL = 0x0002;
    private const uint SHIFT = 0x0004;
    private const uint MOD_WIN = 0x0008;
    private const uint MOD_NOREPEAT = 0x4000;

    // Virtual key codes
    private const int VK_J = 0x4A;

    private const int HOTKEY_ID = 9000;

    private HwndSource? _source;
    private IntPtr _hwnd;
    private bool _registered;

    /// <summary>Fired when the hotkey is pressed.</summary>
    public event Action? HotkeyPressed;

    /// <summary>
    /// Register the global hotkey. Must be called after a window handle is available.
    /// </summary>
    public void Register(IntPtr hwnd)
    {
        _hwnd = hwnd;

        // Use a hidden message-only window to receive WM_HOTKEY
        _source = HwndSource.FromHwnd(hwnd);
        if (_source != null)
        {
            _source.AddHook(HwndHook);
        }

        // Register Win+J (MOD_WIN | MOD_NOREPEAT to prevent auto-repeat)
        _registered = RegisterHotKey(hwnd, HOTKEY_ID, MOD_WIN | MOD_NOREPEAT, VK_J);

        if (!_registered)
        {
            // Fallback: Ctrl+Alt+J if Win+J is taken (Windows sometimes reserves Win+key combos)
            _registered = RegisterHotKey(hwnd, HOTKEY_ID, MOD_CONTROL | MOD_ALT | MOD_NOREPEAT, VK_J);
        }
    }

    private IntPtr HwndHook(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        const int WM_HOTKEY = 0x0312;

        if (msg == WM_HOTKEY && wParam.ToInt32() == HOTKEY_ID)
        {
            HotkeyPressed?.Invoke();
            handled = true;
        }

        return IntPtr.Zero;
    }

    public void Dispose()
    {
        if (_registered && _hwnd != IntPtr.Zero)
        {
            UnregisterHotKey(_hwnd, HOTKEY_ID);
            _registered = false;
        }
        _source?.RemoveHook(HwndHook);
        _source = null;
    }
}
