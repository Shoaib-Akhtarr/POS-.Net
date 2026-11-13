using System;
using System.Windows;
using System.Windows.Threading;
using POS.Services;

namespace POS
{
    public partial class App : Application
    {
        private DispatcherTimer? _autoBackupTimer;

        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            ShortcutService.EnsureShortcut();
            DatabaseInitializer.Initialize();
            ThemeService.ApplySavedPreferences();

            StartPeriodicBackupTimer();
        }

        private void StartPeriodicBackupTimer()
        {
            // Initial backup & spreadsheet generation on launch in background thread
            Task.Run(() => new BackupService().RunPeriodicBackup());

            // Repeat every 2 hours while app is open
            _autoBackupTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromHours(2)
            };
            _autoBackupTimer.Tick += (s, e) =>
            {
                Task.Run(() => new BackupService().RunPeriodicBackup());
            };
            _autoBackupTimer.Start();
        }

        protected override void OnExit(ExitEventArgs e)
        {
            _autoBackupTimer?.Stop();
            // Silent auto-backup to the owner-configured folder (Google Drive / OneDrive / USB)
            new BackupService().RunAutoBackupOnExit();
            base.OnExit(e);
        }
    }
}
