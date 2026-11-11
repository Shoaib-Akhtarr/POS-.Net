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
    }
}
