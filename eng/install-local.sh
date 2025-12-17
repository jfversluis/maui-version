#!/usr/bin/env bash
set -euo pipefail

# Installs MauiVersion CLI tool locally for testing

REPO_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"

if [[ "${1:-}" == "--uninstall" ]]; then
    echo "Uninstalling maui-version..."
    dotnet tool uninstall -g maui-version 2>&1 || true
    echo "✓ Uninstalled maui-version"
    exit 0
fi

echo "Building MauiVersion CLI..."
cd "$REPO_ROOT/src/MauiVersion"

# Clean previous builds
rm -rf bin obj

# Build and pack
dotnet pack -c Release -o "$REPO_ROOT/nupkg"

echo "✓ Build successful"

# Uninstall existing version
echo "Uninstalling existing version..."
dotnet tool uninstall -g maui-version 2>&1 || true

# Install from local nupkg
echo "Installing from local package..."
dotnet tool install -g maui-version --add-source "$REPO_ROOT/nupkg" --prerelease

echo ""
echo "✓ MauiVersion CLI installed successfully!"
echo ""
echo "Usage:"
echo "  maui-version apply              # Interactive mode"
echo "  maui-version apply --stable     # Apply stable release"
echo "  maui-version apply --nightly    # Apply nightly build"
echo "  maui-version apply --pr 12345   # Apply PR build"
echo ""
