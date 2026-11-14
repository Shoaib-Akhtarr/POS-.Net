#!/usr/bin/env bash
set -euo pipefail

# ==============================================================================
# Karobar POS - Windows Desktop Packaging Script
# Compiles and publishes self-contained & framework-dependent Windows releases.
# ==============================================================================

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
ROOT_DIR="$(cd "${SCRIPT_DIR}/.." && pwd)"

echo "==> Building Karobar POS for Windows (win-x64)..."

cd "${ROOT_DIR}"

# 1. Clean previous dist
rm -rf dist/windows-standalone dist/windows-framework

# 2. Publish Self-Contained Single-File Executable
echo "==> Publishing Self-Contained Single-File (dist/windows-standalone/POS.exe)..."
dotnet publish POS/POS.csproj \
    -c Release \
    -r win-x64 \
    --self-contained true \
    -p:PublishSingleFile=true \
    -p:IncludeNativeLibrariesForSelfExtract=true \
    -p:EnableWindowsTargeting=true \
    -o dist/windows-standalone

# 3. Publish Framework-Dependent Lightweight Distribution
echo "==> Publishing Framework-Dependent (dist/windows-framework/)..."
dotnet publish POS/POS.csproj \
    -c Release \
    -r win-x64 \
    --self-contained false \
    -p:EnableWindowsTargeting=true \
    -o dist/windows-framework

# 4. Copy Icon into dist
cp POS/Resources/app.ico dist/windows-standalone/app.ico 2>/dev/null || true
cp POS/Resources/icon.png dist/windows-standalone/icon.png 2>/dev/null || true

echo ""
echo "================================================================================"
echo " Packaging completed successfully!"
echo " 1. Portable Single-File: dist/windows-standalone/POS.exe"
echo "    (Runs on any Windows 10/11 x64 PC without installing .NET runtime)"
echo " 2. Framework-Dependent:  dist/windows-framework/POS.exe"
echo "================================================================================"
