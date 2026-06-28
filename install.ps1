<#
.SYNOPSIS
    Install Jarvis as a Windows background component.

.DESCRIPTION
    Jarvis coexists with Windows — it does NOT replace Explorer.
    It installs as a background process that starts at user login:
      - Floating glass orb (always on top, draggable)
      - Siri-style overlay (summoned via Win+J or voice)

    No desktop shortcut, no Start Menu, no tray icon.
    Jarvis is invisible until you press Win+J or say "Hey Jarvis".

.PARAMETER Uninstall
    Remove Jarvis from the system.

.PARAMETER Source
    Path to the published Jarvis.exe (default: .\publish\Jarvis.exe)

.EXAMPLE
    .\install.ps1
    Installs Jarvis as a background system component.

.EXAMPLE
    .\install.ps1 -Uninstall
    Removes Jarvis from the system.
#>

param(
    [switch]$Uninstall,
    [string]$Source = "$PSScriptRoot\publish\Jarvis.exe"
)

$ErrorActionPreference = "Stop"
$InstallDir = "$env:ProgramFiles\Jarvis"
$TaskName = "Jarvis"

# ── Admin check ───────────────────────────────────────────────
$isAdmin = ([Security.Principal.WindowsPrincipal] [Security.Principal.WindowsIdentity]::GetCurrent()).IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)
if (-not $isAdmin) {
    Write-Host "Administrator privileges required. Relaunching elevated..." -ForegroundColor Yellow
    Start-Process powershell.exe -ArgumentList "-ExecutionPolicy Bypass -File `"$PSCommandPath`" $(if ($Uninstall) {'-Uninstall'}) -Source `"$Source`"" -Verb RunAs
    exit
}

# ── Uninstall ─────────────────────────────────────────────────
if ($Uninstall) {
    Write-Host ""
    Write-Host "Removing Jarvis from Windows..." -ForegroundColor Cyan

    # Kill running Jarvis process
    Get-Process -Name "Jarvis" -ErrorAction SilentlyContinue | Stop-Process -Force

    # Remove scheduled task
    Unregister-ScheduledTask -TaskName $TaskName -Confirm:$false -ErrorAction SilentlyContinue

    # Remove Start Menu shortcut
    $shortcutPath = "$env:APPDATA\Microsoft\Windows\Start Menu\Programs\Jarvis.lnk"
    if (Test-Path $shortcutPath) {
        Remove-Item $shortcutPath -Force
        Write-Host "  Removed Start Menu shortcut" -ForegroundColor Green
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
    exit 0
}

# ── Install ───────────────────────────────────────────────────
Write-Host ""
Write-Host "==========================================" -ForegroundColor Cyan
Write-Host "  Installing Jarvis into Windows" -ForegroundColor Cyan
Write-Host "==========================================" -ForegroundColor Cyan
Write-Host ""

# Step 1: Copy files
Write-Host "[1/3] Copying files to $InstallDir..." -ForegroundColor White
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
Write-Host "[2/4] Registering Jarvis to start at login..." -ForegroundColor White

$action = New-ScheduledTaskAction -Execute "$InstallDir\Jarvis.exe"
$trigger = New-ScheduledTaskTrigger -AtLogOn
$settings = New-ScheduledTaskSettingsSet -AllowStartIfOnBatteries -DontStopIfGoingOnBatteries -StartWhenAvailable -ExecutionTimeLimit ([TimeSpan]::Zero)
$principal = New-ScheduledTaskPrincipal -UserId $env:USERNAME -LogonType Interactive -RunLevel Limited

# Remove existing task if present
Unregister-ScheduledTask -TaskName $TaskName -Confirm:$false -ErrorAction SilentlyContinue

Register-ScheduledTask -TaskName $TaskName -Action $action -Trigger $trigger -Settings $settings -Principal $principal -Description "Jarvis AI assistant — runs alongside Windows, summoned via Win+J or 'Hey Jarvis'" | Out-Null

Write-Host "  Jarvis will start automatically at login." -ForegroundColor Green

# Step 3: Create Start Menu shortcut
Write-Host "[3/4] Creating Start Menu shortcut..." -ForegroundColor White

$startMenuPath = "$env:APPDATA\Microsoft\Windows\Start Menu\Programs"
$shortcutPath = "$startMenuPath\Jarvis.lnk"

if (Test-Path $shortcutPath) { Remove-Item $shortcutPath -Force }

$shell = New-Object -ComObject WScript.Shell
$shortcut = $shell.CreateShortcut($shortcutPath)
$shortcut.TargetPath = "$InstallDir\Jarvis.exe"
$shortcut.IconLocation = "$InstallDir\Jarvis.exe,0"
$shortcut.Description = "Jarvis AI assistant — Press Win+J or say 'Hey Jarvis'"
$shortcut.Save()

Write-Host "  Start Menu shortcut created." -ForegroundColor Green

# Step 4: Start Jarvis now
Write-Host "[4/4] Starting Jarvis..." -ForegroundColor White
Start-ScheduledTask -TaskName $TaskName
Write-Host "  Jarvis is now running in the background." -ForegroundColor Green

Write-Host ""
Write-Host "==========================================" -ForegroundColor Green
Write-Host "  Installation complete!" -ForegroundColor Green
Write-Host "==========================================" -ForegroundColor Green
Write-Host ""
Write-Host "Jarvis is now part of Windows." -ForegroundColor White
Write-Host ""
Write-Host "  Press Win+J to summon the assistant." -ForegroundColor Cyan
Write-Host '  Say "Hey Jarvis" to summon via voice.' -ForegroundColor Cyan
Write-Host "  Click the floating orb to open the assistant." -ForegroundColor Cyan
Write-Host "  Find Jarvis in the Start Menu to relaunch." -ForegroundColor Cyan
Write-Host "  Tray icon in hidden icons menu for quick access." -ForegroundColor Cyan
Write-Host ""
Write-Host "On startup, nothing is visible — not even the orb." -ForegroundColor Gray
Write-Host "The orb only appears after you press Win+J or say Hey Jarvis." -ForegroundColor Gray
Write-Host ""
Write-Host "To uninstall: .\install.ps1 -Uninstall" -ForegroundColor Gray
Write-Host ""
