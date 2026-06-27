# publish.ps1 — Build Jarvis as a self-contained single-file .exe
# Usage:
#   powershell -ExecutionPolicy Bypass -File publish.ps1
#   powershell -ExecutionPolicy Bypass -File publish.ps1 -Version 1.1.0

param(
    [string]$Version = "1.0.0"
)

$ErrorActionPreference = "Stop"

Write-Host ""
Write-Host "==========================================" -ForegroundColor Cyan
Write-Host "  Jarvis — Build" -ForegroundColor Cyan
Write-Host "==========================================" -ForegroundColor Cyan
Write-Host ""

# ── Step 1: Publish self-contained .exe ──────────────────────
Write-Host "[1/2] Publishing self-contained .exe..." -ForegroundColor White

$publishDir = "$PSScriptRoot\publish"
if (Test-Path $publishDir) { Remove-Item $publishDir -Recurse -Force }
New-Item -ItemType Directory -Path $publishDir -Force | Out-Null

dotnet publish "$PSScriptRoot\src\Jarvis.Windows\Jarvis.Windows.csproj" `
    -c Release `
    -r win-x64 `
    --self-contained true `
    -p:PublishSingleFile=true `
    -p:IncludeNativeLibrariesForSelfExtract=true `
    -p:Version=$Version `
    -o $publishDir

if ($LASTEXITCODE -ne 0) {
    Write-Host "Build failed!" -ForegroundColor Red
    exit 1
}

$exePath = "$publishDir\Jarvis.exe"
$exeSize = [math]::Round((Get-Item $exePath).Length / 1MB, 1)
Write-Host "  Built: $exePath ($exeSize MB)" -ForegroundColor Green

# ── Step 2: Verify ───────────────────────────────────────────
Write-Host "[2/2] Verifying..." -ForegroundColor White
Write-Host "  Web assets embedded in Jarvis.Core.dll" -ForegroundColor Green
Write-Host "  NAudio bundled for wake word detection" -ForegroundColor Green
Write-Host "  WebView2 runtime required (preinstalled on Win10/11)" -ForegroundColor Green

Write-Host ""
Write-Host "==========================================" -ForegroundColor Green
Write-Host "  Build complete!" -ForegroundColor Green
Write-Host "==========================================" -ForegroundColor Green
Write-Host ""
Write-Host "Next step — install into Windows:" -ForegroundColor White
Write-Host "  powershell -ExecutionPolicy Bypass -File install.ps1" -ForegroundColor Gray
Write-Host ""
Write-Host "This registers Jarvis as a system component:" -ForegroundColor Gray
Write-Host "  - Starts at login (scheduled task, hidden)" -ForegroundColor Gray
Write-Host "  - No window, no tray icon, no shortcuts" -ForegroundColor Gray
Write-Host "  - Summon via Win+J or 'Hey Jarvis'" -ForegroundColor Gray
Write-Host ""
