#!/usr/bin/env pwsh
<#
.SYNOPSIS
    Installs MauiVersion CLI tool locally for testing
.DESCRIPTION
    Builds and installs the MauiVersion CLI tool as a global .NET tool from the local source
#>

param(
    [switch]$Uninstall
)

$ErrorActionPreference = "Stop"
$repoRoot = Split-Path $PSScriptRoot -Parent

if ($Uninstall) {
    Write-Host "Uninstalling maui-version..." -ForegroundColor Yellow
    dotnet tool uninstall -g version-maui 2>&1 | Out-Null
    Write-Host "✓ Uninstalled maui-version" -ForegroundColor Green
    exit 0
}

Write-Host "Building MauiVersion CLI..." -ForegroundColor Cyan
Push-Location (Join-Path $repoRoot "src" "MauiVersion")
try {
    # Clean previous builds
    if (Test-Path "bin") {
        Remove-Item -Recurse -Force "bin"
    }
    if (Test-Path "obj") {
        Remove-Item -Recurse -Force "obj"
    }

    # Build and pack
    dotnet pack -c Release -o (Join-Path $repoRoot "nupkg")
    if ($LASTEXITCODE -ne 0) {
        throw "Build failed"
    }

    Write-Host "✓ Build successful" -ForegroundColor Green

    # Uninstall existing version
    Write-Host "Uninstalling existing version..." -ForegroundColor Yellow
    dotnet tool uninstall -g version-maui 2>&1 | Out-Null

    # Install from local nupkg
    Write-Host "Installing from local package..." -ForegroundColor Cyan
    $nupkgPath = Join-Path $repoRoot "nupkg"
    dotnet tool install -g version-maui --add-source $nupkgPath --prerelease
    if ($LASTEXITCODE -ne 0) {
        throw "Installation failed"
    }

    Write-Host ""
    Write-Host "✓ MauiVersion CLI installed successfully!" -ForegroundColor Green
    Write-Host ""
    Write-Host "Usage:" -ForegroundColor Cyan
    Write-Host "  maui-version apply              # Interactive mode"
    Write-Host "  maui-version apply --stable     # Apply stable release"
    Write-Host "  maui-version apply --nightly    # Apply nightly build"
    Write-Host "  maui-version apply --pr 12345   # Apply PR build"
    Write-Host ""
}
finally {
    Pop-Location
}
