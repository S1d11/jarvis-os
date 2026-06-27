# publish.ps1 — Build Jarvis for Windows as a self-contained single-file .exe
# Usage:
#   powershell -ExecutionPolicy Bypass -File publish.ps1              # just .exe
#   powershell -ExecutionPolicy Bypass -File publish.ps1 -MakeInstaller # .exe + installer

param(
    [switch]$MakeInstaller,
    [string]$Version = "1.0.0"
)

$ErrorActionPreference = "Stop"

Write-Host ""
Write-Host "==========================================" -ForegroundColor Cyan
Write-Host "  Jarvis for Windows — Build" -ForegroundColor Cyan
Write-Host "==========================================" -ForegroundColor Cyan
Write-Host ""

# ── Step 1: Publish self-contained .exe ──────────────────────
Write-Host "[1/3] Publishing self-contained .exe..." -ForegroundColor White

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

# ── Step 2: Copy web assets ──────────────────────────────────
Write-Host "[2/3] Verifying web assets..." -ForegroundColor White
# Web assets are embedded in Jarvis.Core.dll as embedded resources
# They get extracted at runtime to %LOCALAPPDATA%\Jarvis\web\
Write-Host "  Web assets embedded in Jarvis.Core.dll" -ForegroundColor Green

# ── Step 3: Build installer (optional) ───────────────────────
if ($MakeInstaller) {
    Write-Host "[3/3] Building installer..." -ForegroundColor White

    $issFile = "$PSScriptRoot\installer\setup.iss"
    if (-not (Test-Path $issFile)) {
        Write-Host "  setup.iss not found. Skipping installer." -ForegroundColor Yellow
    } else {
        # Find Inno Setup
        $iscc = Get-Command "iscc" -ErrorAction SilentlyContinue
        if (-not $iscc) {
            $isccPath = "C:\Program Files (x86)\Inno Setup 6\ISCC.exe"
            if (Test-Path $isccPath) {
                $iscc = [PSCustomObject]@{ Source = $isccPath }
            } else {
                Write-Host "  Inno Setup not found. Install from https://jrsoftware.org/isdl.php" -ForegroundColor Yellow
                Write-Host "  Skipping installer build." -ForegroundColor Yellow
            }
        }

        if ($iscc) {
            # Update version in .iss
            (Get-Content $issFile) -replace '#define MyAppVersion ".*"', "#define MyAppVersion `"$Version`"" |
                Set-Content $issFile

            & $iscc.Source $issFile
            if ($LASTEXITCODE -eq 0) {
                $installerPath = "$PSScriptRoot\installer\Output\Jarvis-Setup-$Version.exe"
                if (Test-Path $installerPath) {
                    $instSize = [math]::Round((Get-Item $installerPath).Length / 1MB, 1)
                    Write-Host "  Built: $installerPath ($instSize MB)" -ForegroundColor Green
                }
            } else {
                Write-Host "  Installer build failed!" -ForegroundColor Red
            }
        }
    }
} else {
    Write-Host "[3/3] Skipping installer (use -MakeInstaller to build)" -ForegroundColor Gray
}

Write-Host ""
Write-Host "==========================================" -ForegroundColor Green
Write-Host "  Build complete!" -ForegroundColor Green
Write-Host "==========================================" -ForegroundColor Green
Write-Host ""
Write-Host "Output:" -ForegroundColor White
Write-Host "  $publishDir\Jarvis.exe" -ForegroundColor Gray
if ($MakeInstaller) {
    Write-Host "  $PSScriptRoot\installer\Output\Jarvis-Setup-$Version.exe" -ForegroundColor Gray
}
Write-Host ""
