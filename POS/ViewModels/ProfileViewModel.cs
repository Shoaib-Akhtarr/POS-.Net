using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System;
using System.IO;
using System.Linq;
using System.Windows;

namespace POS.ViewModels
{
    public partial class ProfileViewModel : ObservableObject
    {
        [ObservableProperty]
        private string _currentLanguage;

        public ProfileViewModel()
        {
            // Detect current language from App resources
            var currentDict = Application.Current.Resources.MergedDictionaries
                .FirstOrDefault(d => d.Source != null && d.Source.OriginalString.Contains("Langs/"));

            if (currentDict != null)
            {
                CurrentLanguage = currentDict.Source.OriginalString.Contains("ur.xaml") ? "Urdu" : "English";
            }
            else
            {
                CurrentLanguage = "English";
            }
        }

        [RelayCommand]
        private void ChangeLanguage(string lang)
        {
            string dictPath = lang == "Urdu" ? "Resources/Langs/ur.xaml" : "Resources/Langs/en.xaml";
            
            try
            {
                var newDict = new ResourceDictionary { Source = new Uri(dictPath, UriKind.Relative) };

                // Find and replace the existing language dictionary
                var existingDict = Application.Current.Resources.MergedDictionaries
                    .FirstOrDefault(d => d.Source != null && d.Source.OriginalString.Contains("Langs/"));

                if (existingDict != null)
                {
                    Application.Current.Resources.MergedDictionaries.Remove(existingDict);
                }

                Application.Current.Resources.MergedDictionaries.Add(newDict);
                CurrentLanguage = lang;
                
                // Save preference for persistence
                SaveLanguagePreference(lang);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error switching language: {ex.Message}");
            }
        }

        private void SaveLanguagePreference(string lang)
        {
            try
            {
                string appDir = Path.GetDirectoryName(Environment.ProcessPath) ?? AppDomain.CurrentDomain.BaseDirectory;
                string configPath = Path.Combine(appDir, "settings.txt");
                File.WriteAllText(configPath, lang);
            }
            catch { /* Ignore saving errors */ }
        }
    }
}
