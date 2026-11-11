using System;
using System.Linq;
using System.Windows;
using POS.Models;

namespace POS.Services
{
    public static class ThemeService
    {
        public static StoreSettings GetSettings()
        {
            try
            {
                using var context = new AppDbContext();
                return context.StoreSettings.FirstOrDefault() ?? new StoreSettings();
            }
            catch
            {
                return new StoreSettings();
            }
        }

        public static void ApplySavedPreferences()
        {
            var settings = GetSettings();
            ApplyTheme(settings.Theme);
            ApplyLanguage(settings.Language);
        }

        public static void ApplyTheme(string theme)
        {
            if (Application.Current == null) return;

            string dictPath = string.Equals(theme, "Dark", StringComparison.OrdinalIgnoreCase)
                ? "Themes/DarkTheme.xaml"
                : "Themes/LightTheme.xaml";

            SwapDictionary("Themes/", dictPath);
        }

        public static void ApplyLanguage(string language)
        {
            if (Application.Current == null) return;

            bool isUrdu = string.Equals(language, "Urdu", StringComparison.OrdinalIgnoreCase);
            string dictPath = isUrdu ? "Resources/Langs/ur.xaml" : "Resources/Langs/en.xaml";

            SwapDictionary("Langs/", dictPath);

            if (Application.Current.MainWindow != null)
            {
                Application.Current.MainWindow.FlowDirection = isUrdu ? FlowDirection.RightToLeft : FlowDirection.LeftToRight;
            }
        }

        private static void SwapDictionary(string matchSegment, string newRelativeUri)
        {
            var dictionaries = Application.Current.Resources.MergedDictionaries;
            var existing = dictionaries.FirstOrDefault(d =>
                d.Source != null &&
                d.Source.OriginalString.Contains(matchSegment, StringComparison.OrdinalIgnoreCase) &&
                !d.Source.OriginalString.Contains("Tokens.xaml", StringComparison.OrdinalIgnoreCase) &&
                !d.Source.OriginalString.Contains("Controls.xaml", StringComparison.OrdinalIgnoreCase));

            if (existing != null && string.Equals(existing.Source?.OriginalString, newRelativeUri, StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            var replacement = new ResourceDictionary { Source = new Uri(newRelativeUri, UriKind.Relative) };

            if (existing != null)
            {
                int index = dictionaries.IndexOf(existing);
                dictionaries[index] = replacement;
            }
            else
            {
                dictionaries.Insert(0, replacement);
            }
        }
    }
}

