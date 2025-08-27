# .vscode/build.ps1
Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
$PSNativeCommandUseErrorActionPreference = $true

Write-Host 'Building D2RLAN...' -ForegroundColor Green

# Paths
$projDir = Join-Path $PSScriptRoot '..\D2RLAN-WPF\src\D2RLAN\D2RLAN'
$source  = Join-Path $projDir 'bin\Debug\net7.0-windows'
$target  = 'D:\D2RLaunch\D2RLAN\net7.0-windows'

Push-Location $projDir
try {
    # Build
    dotnet build -c Debug -v minimal
    if ($LASTEXITCODE -ne 0) {
        throw "Build failed with exit code $LASTEXITCODE"
    }

    Write-Host 'Build successful!' -ForegroundColor Green
    Write-Host 'Cleaning target directory...' -ForegroundColor Yellow

    if (-not (Test-Path -LiteralPath $source)) {
        throw "Build output not found at '$source'"
    }

    # Clean target directory completely
    if (Test-Path -LiteralPath $target) {
        Remove-Item -Path $target -Recurse -Force
        Write-Host "   Removed existing $target" -ForegroundColor Gray
    }

    # Create fresh target directory
    New-Item -ItemType Directory -Path $target -Force | Out-Null
    Write-Host 'Copying fresh files to deployment directory...' -ForegroundColor Yellow

    # Copy all files and subfolders (including locale directories)
    Copy-Item -Path (Join-Path $source '*') -Destination $target -Recurse -Force

    Write-Host 'Deploy completed!' -ForegroundColor Green
    Write-Host 'Starting D2RLAN...' -ForegroundColor Cyan

    $exePath = Join-Path $target 'D2RLAN.exe'
    if (-not (Test-Path -LiteralPath $exePath)) {
        throw "Could not find executable at '$exePath'"
    }
    Start-Process -FilePath $exePath -WorkingDirectory $target
}
catch {
    Write-Host "Error: $($_.Exception.Message)" -ForegroundColor Red
    exit 1
}
finally {
    Pop-Location
}