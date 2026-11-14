@echo off
echo ================================================================================
echo  Karobar POS - Windows Desktop Packaging
echo ================================================================================

cd /d "%~dp0\.."

echo ==> Publishing Self-Contained Single-File (dist\windows-standalone\POS.exe)...
dotnet publish POS\POS.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -o dist\windows-standalone

echo ==> Publishing Framework-Dependent (dist\windows-framework\)...
dotnet publish POS\POS.csproj -c Release -r win-x64 --self-contained false -o dist\windows-framework

copy POS\Resources\app.ico dist\windows-standalone\app.ico >nul 2>&1
copy POS\Resources\icon.png dist\windows-standalone\icon.png >nul 2>&1

echo.
echo Packaging completed!
echo Standalone Portable Exe: dist\windows-standalone\POS.exe
echo.
pause
