# Desktop Deployment & Packaging Guide

Karobar POS is a modern WPF desktop application targeting `.NET 10.0-windows` with hardware acceleration, SQLite embedded storage, and MaterialDesignThemes.

---

## 1. Building for Windows (x64)

### Option A: Portable Standalone Single-File (Recommended)
This produces a self-contained executable that bundles the .NET 10 Windows Desktop Runtime, SQLite native libraries, and embedded application icon (`app.ico`). It runs directly on any Windows 10/11 x64 PC without requiring .NET to be pre-installed.

* **On Linux (Cross-Compilation):**
  ```bash
  ./packaging/publish-windows.sh
  ```
  Output: `dist/windows-standalone/POS.exe`

* **On Windows:**
  ```cmd
  packaging\publish-windows.bat
  ```
  or via PowerShell:
  ```powershell
  dotnet publish POS/POS.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -o dist/windows-standalone
  ```

### Option B: Windows Installer Setup (`.exe`)
You can compile the Inno Setup script to generate a standard Windows installer (`KarobarPOS-Setup-v2.0.0.exe`) with desktop shortcuts, Start Menu entry, and uninstaller.

1. Install [Inno Setup 6](https://jrsoftware.org/isdl.php) on Windows.
2. Open `packaging/installer.iss`.
3. Click **Compile** (or run `iscc packaging/installer.iss`).
4. Output installer is created at `dist/installer/KarobarPOS-Setup-v2.0.0.exe`.

---

## 2. Running on Linux Fedora

WPF is built on the Windows Graphics Foundation (DirectX / Win32). To run the application on Fedora Linux, use **Wine** or **Bottles**:

### Method 1: Using Wine (Native Fedora RPM)
1. Install Wine:
   ```bash
   sudo dnf install wine winetricks
   ```
2. Build and launch:
   ```bash
   ./packaging/run-on-fedora.sh
   ```

### Method 2: Using Bottles (Flatpak)
1. Install Bottles from Flathub:
   ```bash
   flatpak install flathub com.usebottles.bottles
   ```
2. Create an **Application Bottle** (64-bit).
3. Under **Dependencies**, install `dotnetcoredesktop10` or run the standalone executable directly:
   `dist/windows-standalone/POS.exe`.

### Method 3: Desktop Launcher on Fedora GNOME
Install the application shortcut to your user menu:
```bash
cp POS/Resources/icon.png ~/.local/share/icons/hicolor/256x256/apps/karobar-pos.png
cp packaging/karobar-pos.desktop ~/.local/share/applications/
update-desktop-database ~/.local/share/applications/
```
