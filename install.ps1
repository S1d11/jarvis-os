<#
.SYNOPSIS
    Install Jarvis as a Windows system component.

.DESCRIPTION
    This is NOT a standard application installer. Jarvis is installed as
    a system-level background process that starts at boot — not a desktop
    app with shortcuts and a Start Menu entry.

    What this script does:
      1. Copies Jarvis to C:\Program Files\Jarvis
      2. Registers a scheduled task that starts Jarvis at user login
         (hidden, no window, no tray icon)
      3. Optionally replaces Explorer with Jarvis as the shell

    After installation, Jarvis is invisible:
      - No desktop shortcut
      - No Start Menu entry
      - No system tray icon
      - No visible window

    The only way to interact with Jarvis is:
      - Press Win+J (the orb appears)
      - Say "Hey Jarvis" (the orb appears)

.PARAMETER Uninstall
    Remove Jarvis from the system (restores Explorer if needed).

.PARAMETER ShellMode
    Replace Windows Explorer with Jarvis as the desktop shell.

.PARAMETER Source
    Path to the published Jarvis.exe (default: .\publish\Jarvis.exe)

.EXAMPLE
    .\install.ps1
    Installs Jarvis as a background system component.

.EXAMPLE
    .\install.ps1 -ShellMode
    Installs Jarvis and replaces Explorer as the Windows shell.

.EXAMPLE
    .\install.ps1 -Uninstall
    Removes Jarvis and restores Explorer.
#>

param(
    [switch]$Uninstall,
    [switch]$ShellMode,
    [string]$Source = "$PSScriptRoot\publish\Jarvis.exe"
)

$ErrorActionPreference = "Stop"
$InstallDir = "$env:ProgramFiles\Jarvis"
$TaskName = "Jarvis"

# ── Admin check ───────────────────────────────────────────────
$isAdmin = ([Security.Principal.WindowsPrincipal] [Security.Principal.WindowsIdentity]::GetCurrent()).IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)
if (-not $isAdmin) {
    Write-Host "Administrator privileges required. Relaunching elevated..." -ForegroundColor Yellow
    Start-Process powershell.exe -ArgumentList "-ExecutionPolicy Bypass -File `"$PSCommandPath`" $(if ($Uninstall) {'-Uninstall'}) $(if ($ShellMode) {'-ShellMode'}) -Source `"$Source`"" -Verb RunAs
    exit
}

# ── Uninstall ─────────────────────────────────────────────────
if ($Uninstall) {
    Write-Host ""
    Write-Host "Removing Jarvis from Windows…" -ForegroundColor Cyan

    # Kill running Jarvis process
    Get-Process -Name "Jarvis" -ErrorAction SilentlyContinue | Stop-Process -Force

    # Remove scheduled task
    Unregister-ScheduledTask -TaskName $TaskName -Confirm:$false -ErrorAction SilentlyContinue

    # Restore Explorer shell if it was replaced
    $winlogon = "HKLM:\SOFTWARE\Microsoft\Windows NT\CurrentVersion\Winlogon"
    $currentShell = (Get-ItemProperty -Path $winlogon -Name "Shell" -ErrorAction SilentlyContinue).Shell
    if ($currentShell -and $currentShell -like "*Jarvis*") {
        Set-ItemProperty -Path $winlogon -Name "Shell" -Value "explorer.exe"
        Write-Host "  Restored Explorer as shell." -ForegroundColor Green
    }

    # Remove install directory
    if (Test-Path $InstallDir) {
        Remove-Item $InstallDir -Recurse -Force
        Write-Host "  Removed $InstallDir" -ForegroundColor Green
    }

    # Remove user data
    $userData = "$env:LOCALAPPDATA\Jarvis"
    if (Test-Path $userData) {
        Remove-Item $userData -Recurse -Force
        Write-Host "  Removed user data" -ForegroundColor Green
    }

    Write-Host ""
    Write-Host "Jarvis has been removed from Windows." -ForegroundColor Green
    Write-Host "Reboot to complete the uninstall." -ForegroundColor Gray
    exit 0
}

# ── Install ───────────────────────────────────────────────────
Write-Host ""
Write-Host "==========================================" -ForegroundColor Cyan
Write-Host "  Installing Jarvis into Windows" -ForegroundColor Cyan
Write-Host "==========================================" -ForegroundColor Cyan
Write-Host ""

# Step 1: Copy files
Write-Host "[1/4] Copying files to $InstallDir …" -ForegroundColor White
if (-not (Test-Path $Source)) {
    Write-Host "  ERROR: Jarvis.exe not found at $Source" -ForegroundColor Red
    Write-Host "  Run publish.ps1 first to build the executable." -ForegroundColor Yellow
    exit 1
}

if (Test-Path $InstallDir) { Remove-Item $InstallDir -Recurse -Force }
New-Item -ItemType Directory -Path $InstallDir -Force | Out-Null
Copy-Item $Source -Destination "$InstallDir\Jarvis.exe" -Force

# Copy any runtime dependencies (DLLs, etc.)
$sourceDir = Split-Path $Source -Parent
Get-ChildItem $sourceDir -Filter "*.dll" | Copy-Item -Destination $InstallDir -Force
Get-ChildItem $sourceDir -Filter "*.json" -ErrorAction SilentlyContinue | Copy-Item -Destination $InstallDir -Force

Write-Host "  Files installed." -ForegroundColor Green

# Step 2: Create scheduled task (starts at user login, hidden)
Write-Host "[2/4] Registering Jarvis as a system component …" -ForegroundColor White

$action = New-ScheduledTaskAction -Execute "$InstallDir\Jarvis.exe" -Argument "--orb"
$trigger = New-ScheduledTaskTrigger -AtLogOn
$settings = New-ScheduledTaskSettingsSet -AllowStartIfOnBatteries -DontStopIfGoingOnBatteries -StartWhenAvailable -ExecutionTimeLimit ([TimeSpan]::Zero)
$principal = New-ScheduledTaskPrincipal -UserId $env:USERNAME -LogonType Interactive -RunLevel Limited

# Remove existing task if present
Unregister-ScheduledTask -TaskName $TaskName -Confirm:$false -ErrorAction SilentlyContinue

Register-ScheduledTask -TaskName $TaskName -Action $action -Trigger $trigger -Settings $settings -Principal $principal -Description "Jarvis AI assistant — runs invisibly in the background, summoned via Win+J or 'Hey Jarvis'" | Out-Null

Write-Host "  Jarvis will start automatically at login (invisible)." -ForegroundColor Green

# Step 3: Shell replacement (optional)
if ($ShellMode) {
    Write-Host "[3/4] Replacing Windows Explorer with Jarvis shell …" -ForegroundColor White
    $winlogon = "HKLM:\SOFTWARE\Microsoft\Windows NT\CurrentVersion\Winlogon"
    $current = (Get-ItemProperty -Path $winlogon -Name "Shell").Shell
    New-Item -Path "HKLM:\SOFTWARE\Jarvis" -Force | Out-Null
    Set-ItemProperty -Path "HKLM:\SOFTWARE\Jarvis" -Name "ExplorerBackup" -Value $current
    Set-ItemProperty -Path $winlogon -Name "Shell" -Value "`"$InstallDir\Jarvis.exe`" --shell"
    Write-Host "  Shell replaced. Reboot to boot into Jarvis." -ForegroundColor Green
} else {
    Write-Host "[3/4] Shell mode skipped (Explorer stays as desktop)." -ForegroundColor Gray
}

# Step 4: Start Jarvis now
Write-Host "[4/4] Starting Jarvis …" -ForegroundColor White
Start-ScheduledTask -TaskName $TaskName
Write-Host "  Jarvis is now running in the background." -ForegroundColor Green

Write-Host ""
Write-Host "==========================================" -ForegroundColor Green
Write-Host "  Installation complete!" -ForegroundColor Green
Write-Host "==========================================" -ForegroundColor Green
Write-Host ""
Write-Host "Jarvis is now part of Windows." -ForegroundColor White
Write-Host ""
Write-Host "  Press Win+J to summon the orb." -ForegroundColor Cyan
Write-Host '  Say "Hey Jarvis" to summon via voice.' -ForegroundColor Cyan
Write-Host ""
Write-Host "There is no desktop shortcut, no Start Menu entry, no tray icon." -ForegroundColor Gray
Write-Host "Jarvis is invisible until you call it." -ForegroundColor Gray
Write-Host ""
Write-Host "To uninstall: .\install.ps1 -Uninstall" -ForegroundColor Gray
Write-Host ""
