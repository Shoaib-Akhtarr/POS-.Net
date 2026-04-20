using System;
using System.Diagnostics;
using System.IO;

namespace POS.Services
{
    public static class ShortcutService
    {
        public static void EnsureShortcut()
        {
            try
            {
                string desktopPath = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
                string shortcutPath = Path.Combine(desktopPath, "Karobar POS.lnk");

                // If shortcut already exists, don't recreate it every time 
                // unless the user wants it forced. For now, just ensure it exists.
                if (File.Exists(shortcutPath)) return;

                string currentExePath = Environment.ProcessPath;
                if (string.IsNullOrEmpty(currentExePath)) return;

                string currentDir = Path.GetDirectoryName(currentExePath);

                // Use PowerShell to create the shortcut (WshShell alternative)
                string script = $@"
                    $WshShell = New-Object -ComObject WScript.Shell
                    $Shortcut = $WshShell.CreateShortcut('{shortcutPath}')
                    $Shortcut.TargetPath = '{currentExePath}'
                    $Shortcut.WorkingDirectory = '{currentDir}'
                    $Shortcut.Description = 'Karobar POS Ledger System'
                    $Shortcut.Save()
                ";

                ProcessStartInfo psi = new ProcessStartInfo
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
                // Fail silently or log
                Debug.WriteLine($"Failed to create shortcut: {ex.Message}");
            }
        }
    }
}
