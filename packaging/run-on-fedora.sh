#!/usr/bin/env bash
set -euo pipefail

# ==============================================================================
# Karobar POS - Linux Fedora Runner
# Runs the Karobar POS desktop application on Fedora via Wine / Proton / Bottles
# ==============================================================================

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
ROOT_DIR="$(cd "${SCRIPT_DIR}/.." && pwd)"

EXE_PATH="${ROOT_DIR}/dist/windows-standalone/POS.exe"
if [[ ! -f "${EXE_PATH}" ]]; then
    EXE_PATH="${ROOT_DIR}/dist/windows-framework/POS.exe"
fi

if [[ ! -f "${EXE_PATH}" ]]; then
    echo "==> Published executable not found. Building now..."
    "${SCRIPT_DIR}/publish-windows.sh"
    EXE_PATH="${ROOT_DIR}/dist/windows-standalone/POS.exe"
fi

# Check for Wine
if ! command -v wine &> /dev/null; then
    echo "================================================================================"
    echo " Wine is required to run Windows WPF applications on Linux Fedora."
    echo " To install Wine on Fedora, run:"
    echo "   sudo dnf install wine winetricks"
    echo " Alternatively, you can use Bottles (flatpak install flathub com.usebottles.bottles)"
    echo "================================================================================"
    exit 1
fi

echo "==> Launching Karobar POS via Wine..."
wine "${EXE_PATH}" "$@"
