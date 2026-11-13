using System.Windows;
using POS.Services;

namespace POS
{
    public partial class App : Application
    {
        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            ShortcutService.EnsureShortcut();
            DatabaseInitializer.Initialize();
            ThemeService.ApplySavedPreferences();
        }

        protected override void OnExit(ExitEventArgs e)
        {
            // Silent auto-backup to the owner-configured folder (Google Drive / OneDrive / USB)
            new BackupService().RunAutoBackupOnExit();
            base.OnExit(e);
        }
    }
}
