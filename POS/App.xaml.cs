using System.Windows;
using System.IO;
using System.Linq;
using System;

namespace POS
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);
            
            // 1. Ensure Desktop Shortcut
            POS.Services.ShortcutService.EnsureShortcut();

            // 2. Database Sync
            using (var context = new POS.Models.AppDbContext())
            {
                context.Database.EnsureCreated();
            }
        }
    }
}
