using System;
using System.Diagnostics;
using System.IO;

namespace POS.Services
{
    public static class ShortcutService
    {
        public static void EnsureShortcut()
        {
            if (!OperatingSystem.IsWindows()) return;

            try
            {
                string desktopPath = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
                if (string.IsNullOrWhiteSpace(desktopPath)) return;

                string shortcutPath = Path.Combine(desktopPath, "Karobar POS.lnk");
                if (File.Exists(shortcutPath)) return;

                string? currentExePath = Environment.ProcessPath;
                if (string.IsNullOrEmpty(currentExePath)) return;

                string? currentDir = Path.GetDirectoryName(currentExePath) ?? AppDomain.CurrentDomain.BaseDirectory;

                string script = $@"
                    $WshShell = New-Object -ComObject WScript.Shell
                    $Shortcut = $WshShell.CreateShortcut('{shortcutPath}')
                    $Shortcut.TargetPath = '{currentExePath}'
                    $Shortcut.WorkingDirectory = '{currentDir}'
                    $Shortcut.Description = 'Aizaz Fareed Center POS System'
                    $Shortcut.Save()
                ";

                var psi = new ProcessStartInfo
                {
                    FileName = "powershell",
                    Arguments = $"-NoProfile -WindowStyle Hidden -Command \"{script.Replace("\"", "\\\"")}\"",
                    UseShellExecute = false,
                    CreateNoWindow = true
                };

                Process.Start(psi);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Failed to create shortcut: {ex.Message}");
            }
        }
    }
}
