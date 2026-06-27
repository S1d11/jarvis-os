using System;
using System.Windows;
using Jarvis.Core;
using Forms = System.Windows.Forms;

namespace Jarvis.Windows;

/// <summary>
/// System tray icon with context menu.
/// Uses WinForms NotifyIcon (WPF doesn't have a native tray icon).
/// </summary>
public sealed class NotifyIconHelper : IDisposable
{
    private readonly Forms.NotifyIcon _icon;
    private readonly MainWindow _window;

    public NotifyIconHelper(MainWindow window)
    {
        _window = window;

        _icon = new Forms.NotifyIcon
        {
            Text = "Jarvis",
            Visible = true,
        };

        // Use a simple icon — in production, use the app.ico
        try { _icon.Icon = System.Drawing.Icon.ExtractAssociatedIcon(
            Environment.ProcessPath ?? "jarvis.exe"); }
        catch { /* non-critical */ }

        _icon.DoubleClick += (_, _) => _window.BringToFront();

        _icon.ContextMenuStrip = BuildMenu();
    }

    private Forms.ContextMenuStrip BuildMenu()
    {
        var menu = new Forms.ContextMenuStrip();

        menu.Items.Add("Open Jarvis", null, (_, _) => _window.BringToFront());
        menu.Items.Add("-");

        // Quick settings submenu
        var settings = new Forms.ToolStripMenuItem("Settings");
        settings.DropDownItems.Add("Theme: Dark", null, (_, _) => { });
        settings.DropDownItems.Add("Theme: Light", null, (_, _) => { });
        settings.DropDownItems.Add("-");
        settings.DropDownItems.Add("Auto-start on login", null, (_, _) => ToggleAutoStart());
        menu.Items.Add(settings);

        // Shell mode
        var shellItem = new Forms.ToolStripMenuItem("Replace Explorer shell");
        shellItem.CheckOnClick = true;
        shellItem.Checked = Properties.Settings.Default.ShellMode;
        shellItem.Click += (_, _) => ToggleShellMode(shellItem.Checked);
        menu.Items.Add(shellItem);

        menu.Items.Add("-");

        // Power options
        var power = new Forms.ToolStripMenuItem("Power");
        power.DropDownItems.Add("Lock", null, (_, _) => new WindowsSystemAccess().LockScreen());
        power.DropDownItems.Add("Sleep", null, (_, _) => new WindowsSystemAccess().Sleep());
        power.DropDownItems.Add("Restart", null, (_, _) => new WindowsSystemAccess().Restart());
        power.DropDownItems.Add("Shut down", null, (_, _) => new WindowsSystemAccess().Shutdown());
        menu.Items.Add(power);

        menu.Items.Add("-");
        menu.Items.Add("Exit", null, (_, _) => _window.CloseApp());

        return menu;
    }

    private void ToggleAutoStart()
    {
        try
        {
            var key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(
                @"Software\Microsoft\Windows\CurrentVersion\Run", writable: true);
            if (key?.GetValue("Jarvis") != null)
                key.DeleteValue("Jarvis", false);
            else
                key?.SetValue("Jarvis", $"\"{Environment.ProcessPath}\" --tray");
            key?.Close();
        }
        catch { /* non-fatal */ }
    }

    private void ToggleShellMode(bool enable)
    {
        try
        {
            var key = Microsoft.Win32.Registry.LocalMachine.OpenSubKey(
                @"SOFTWARE\Microsoft\Windows NT\CurrentVersion\Winlogon", writable: true);
            if (enable)
            {
                // Backup current shell
                var current = key?.GetValue("Shell") as string ?? "explorer.exe";
                Microsoft.Win32.Registry.LocalMachine.CreateSubKey(@"SOFTWARE\Jarvis")?
                    .SetValue("ExplorerBackup", current);

                key?.SetValue("Shell", $"\"{Environment.ProcessPath}\" --shell");
            }
            else
            {
                key?.SetValue("Shell", "explorer.exe");
            }
            key?.Close();

            _icon.ShowBalloonTip(3000, "Jarvis", enable
                ? "Shell replacement enabled. Reboot to start Jarvis as the desktop shell."
                : "Shell restored to Explorer. Reboot to return to the normal Windows desktop.",
                Forms.ToolTipIcon.Info);
        }
        catch (Exception ex)
        {
            Forms.MessageBox.Show($"Failed to change shell mode: {ex.Message}",
                "Jarvis", Forms.MessageBoxButtons.OK, Forms.MessageBoxIcon.Warning);
        }
    }

    public void ShowBalloon(string title, string message)
    {
        _icon.ShowBalloonTip(3000, title, message, Forms.ToolTipIcon.Info);
    }

    public void Dispose()
    {
        _icon.Visible = false;
        _icon.Dispose();
    }
}
