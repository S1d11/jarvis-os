using System;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace Jarvis.Windows;

/// <summary>
/// Low-level keyboard hook (WH_KEYBOARD_LL) that intercepts keys system-wide
/// BEFORE any other application sees them — including fullscreen games,
/// the lock screen, and UAC prompts.
///
/// This is the same mechanism used by:
///   - Windows built-in shortcuts (Win+L, Win+D, etc.)
///   - Discord push-to-talk
///   - OBS Studio hotkeys
///   - Steam overlay
///
/// Unlike RegisterHotKey (which fails when another app has registered the
/// same combo or when a fullscreen game is eating input), WH_KEYBOARD_LL
/// sits at the very top of the input chain and always receives events.
/// </summary>
public sealed class LowLevelKeyboardHook : IDisposable
{
    [DllImport("user32.dll", SetLastError = true)]
    private static extern IntPtr SetWindowsHookEx(int idHook, LowLevelKeyboardProc lpfn, IntPtr hMod, uint dwThreadId);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool UnhookWindowsHookEx(IntPtr hhk);

    [DllImport("user32.dll")]
    private static extern IntPtr CallNextHookEx(IntPtr hhk, int nCode, IntPtr wParam, IntPtr lParam);

    [DllImport("kernel32.dll")]
    private static extern IntPtr GetModuleHandle(string lpModuleName);

    private delegate IntPtr LowLevelKeyboardProc(int nCode, IntPtr wParam, IntPtr lParam);

    private const int WH_KEYBOARD_LL = 13;
    private const int WM_KEYDOWN = 0x0100;
    private const int WM_SYSKEYDOWN = 0x0104;

    // Virtual key codes
    private const int VK_LWIN = 0x5B;
    private const int VK_RWIN = 0x5C;
    private const int VK_J = 0x4A;
    private const int VK_ESCAPE = 0x1B;

    private IntPtr _hookId = IntPtr.Zero;
    private LowLevelKeyboardProc? _proc;
    private bool _disposed;

    // Track modifier state
    private bool _winDown;

    /// <summary>Fired when Win+J is pressed (summon Jarvis).</summary>
    public event Action? SummonPressed;

    /// <summary>Fired when Escape is pressed (dismiss orb).</summary>
    public event Action? EscapePressed;

    /// <summary>
    /// Install the hook. Call once at startup. The hook remains active
    /// until Dispose() is called.
    /// </summary>
    public void Install()
    {
        if (_hookId != IntPtr.Zero || _disposed) return;

        _proc = HookCallback;

        using var curProcess = Process.GetCurrentProcess();
        using var curModule = curProcess.MainModule!;
        _hookId = SetWindowsHookEx(WH_KEYBOARD_LL, _proc, GetModuleHandle(curModule.ModuleName!), 0);

        if (_hookId == IntPtr.Zero)
        {
            Debug.WriteLine("[KeyboardHook] Failed to install hook");
        }
        else
        {
            Debug.WriteLine("[KeyboardHook] WH_KEYBOARD_LL installed");
        }
    }

    private IntPtr HookCallback(int nCode, IntPtr wParam, IntPtr lParam)
    {
        if (nCode >= 0 && (wParam.ToInt64() == WM_KEYDOWN || wParam.ToInt64() == WM_SYSKEYDOWN))
        {
            int vk = Marshal.ReadInt32(lParam);

            // Track Win key state
            if (vk == VK_LWIN || vk == VK_RWIN)
            {
                _winDown = true;
            }
            else if (_winDown && vk == VK_J)
            {
                // Win+J pressed — summon Jarvis
                SummonPressed?.Invoke();
                // Swallow the key so Windows doesn't also process it
                return new IntPtr(1);
            }
            else if (vk == VK_ESCAPE)
            {
                EscapePressed?.Invoke();
            }
        }

        // Key up — reset win state
        if (nCode >= 0 && wParam.ToInt64() == 0x0101 /* WM_KEYUP */)
        {
            int vk = Marshal.ReadInt32(lParam);
            if (vk == VK_LWIN || vk == VK_RWIN)
                _winDown = false;
        }

        return CallNextHookEx(_hookId, nCode, wParam, lParam);
    }

    public void Dispose()
    {
        if (_hookId != IntPtr.Zero)
        {
            UnhookWindowsHookEx(_hookId);
            _hookId = IntPtr.Zero;
        }
        _disposed = true;
    }
}
