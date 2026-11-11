using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using Microsoft.Win32;
using POS.Models;
using POS.Services;

namespace POS.ViewModels
{
    public partial class ProfileViewModel : ObservableObject, IRecipient<DataChangedMessage>
    {
        [ObservableProperty] private string _currentLanguage = "English";
        [ObservableProperty] private string _currentTheme = "Light";

        // Store Branding & Printer Settings
        [ObservableProperty] private string _shopName = string.Empty;
        [ObservableProperty] private string _subtitle = string.Empty;
        [ObservableProperty] private string _address = string.Empty;
        [ObservableProperty] private string _phone1 = string.Empty;
        [ObservableProperty] private string _phone2 = string.Empty;
        [ObservableProperty] private string _receiptFooter = string.Empty;
        [ObservableProperty] private string _printerName = "XP-80";
        [ObservableProperty] private string _preferredPrintFormat = "Thermal80mm";
        [ObservableProperty] private bool _useBitmapThermalForUrdu;
        [ObservableProperty] private ObservableCollection<string> _installedPrinters = new();

        public List<string> PrintFormats { get; } = new() { "Thermal80mm", "A4" };

        // Staff PIN Management (Owner only)
        [ObservableProperty] private ObservableCollection<StaffUser> _staffUsers = new();
        [ObservableProperty] private StaffUser? _selectedStaffUser;
        [ObservableProperty] private string _resetPinValue = string.Empty;
        [ObservableProperty] private string _newStaffName = string.Empty;
        [ObservableProperty] private string _newStaffRole = "Cashier";
        [ObservableProperty] private string _newStaffPin = string.Empty;
        public List<string> AvailableRoles { get; } = new() { "Cashier", "Owner" };

        [ObservableProperty] private string _statusMessage = string.Empty;
        [ObservableProperty] private bool _isErrorStatus;

        public bool IsOwner => SessionService.IsOwner;
        public string CurrentUserName => SessionService.CurrentUserName;
        public string CurrentUserRole => SessionService.CurrentUser?.Role ?? "Cashier";
        public string DatabasePath => AppDbContext.GetDatabasePath();

        private readonly PrintService _printService;
        private readonly BackupService _backupService;

        public ProfileViewModel()
        {
            _printService = new PrintService();
            _backupService = new BackupService();
            RefreshData();
            WeakReferenceMessenger.Default.Register(this);
        }

        public void Receive(DataChangedMessage message)
        {
            if (message.Value == "Session")
            {
                App.Current?.Dispatcher.Invoke(() =>
                {
                    OnPropertyChanged(nameof(IsOwner));
                    OnPropertyChanged(nameof(CurrentUserName));
                    OnPropertyChanged(nameof(CurrentUserRole));
                });
            }
        }

        public void RefreshData()
        {
            var settings = ThemeService.GetSettings();
            ShopName = settings.ShopName;
            Subtitle = settings.Subtitle;
            Address = settings.Address;
            Phone1 = settings.Phone1;
            Phone2 = settings.Phone2;
            ReceiptFooter = settings.ReceiptFooter;
            PrinterName = settings.PrinterName;
            PreferredPrintFormat = settings.PreferredPrintFormat;
            UseBitmapThermalForUrdu = settings.UseBitmapThermalForUrdu;
            CurrentLanguage = settings.Language;
            CurrentTheme = settings.Theme;

            var printers = PrintService.GetInstalledPrinters();
            if (!string.IsNullOrWhiteSpace(PrinterName) && !printers.Contains(PrinterName))
            {
                printers.Insert(0, PrinterName);
            }
            InstalledPrinters = new ObservableCollection<string>(printers);

            LoadStaffUsers();
        }

        private void LoadStaffUsers()
        {
            var users = SessionService.GetActiveUsers();
            StaffUsers = new ObservableCollection<StaffUser>(users);
            SelectedStaffUser ??= StaffUsers.FirstOrDefault();
        }

        [RelayCommand]
        private void ChangeLanguage(string lang)
        {
            using var context = new AppDbContext();
            var settings = context.StoreSettings.FirstOrDefault();
            if (settings != null)
            {
                settings.Language = lang;
                settings.UpdatedAt = DateTime.Now;
                context.SaveChanges();
            }

            CurrentLanguage = lang;
            ThemeService.ApplyLanguage(lang);
            SetStatus($"Language switched to {lang}.", isError: false);
        }

        [RelayCommand]
        private void ChangeTheme(string theme)
        {
            using var context = new AppDbContext();
            var settings = context.StoreSettings.FirstOrDefault();
            if (settings != null)
            {
                settings.Theme = theme;
                settings.UpdatedAt = DateTime.Now;
                context.SaveChanges();
            }

            CurrentTheme = theme;
            ThemeService.ApplyTheme(theme);
            SetStatus($"{theme} theme activated.", isError: false);
        }

        [RelayCommand]
        private void SaveStoreSettings()
        {
            if (string.IsNullOrWhiteSpace(ShopName))
            {
                SetStatus("Shop name cannot be empty.", isError: true);
                return;
            }

            using var context = new AppDbContext();
            var settings = context.StoreSettings.FirstOrDefault();
            if (settings == null)
            {
                settings = new StoreSettings { Id = 1 };
                context.StoreSettings.Add(settings);
            }

            settings.ShopName = ShopName.Trim();
            settings.Subtitle = Subtitle.Trim();
            settings.Address = Address.Trim();
            settings.Phone1 = Phone1.Trim();
            settings.Phone2 = Phone2.Trim();
            settings.ReceiptFooter = ReceiptFooter.Trim();
            settings.PrinterName = string.IsNullOrWhiteSpace(PrinterName) ? "XP-80" : PrinterName.Trim();
            settings.PreferredPrintFormat = PreferredPrintFormat;
            settings.UseBitmapThermalForUrdu = UseBitmapThermalForUrdu;
            settings.Language = CurrentLanguage;
            settings.Theme = CurrentTheme;
            settings.UpdatedAt = DateTime.Now;

            context.SaveChanges();
            WeakReferenceMessenger.Default.Send(DataChangedMessage.SettingsUpdated);
            SetStatus("Store branding and printer settings saved.", isError: false);
        }

        [RelayCommand]
        private void TestPrint()
        {
            SaveStoreSettings();
            var result = _printService.TestPrint(PrinterName);
            SetStatus(result.message, isError: !result.success);
        }

        [RelayCommand]
        private void UpdateSelectedUserPin()
        {
            if (!SessionService.IsOwner || SelectedStaffUser == null) return;
            if (string.IsNullOrWhiteSpace(ResetPinValue) || ResetPinValue.Trim().Length < 4)
            {
                SetStatus("PIN must be at least 4 characters/digits.", isError: true);
                return;
            }

            using var context = new AppDbContext();
            var dbUser = context.StaffUsers.Find(SelectedStaffUser.Id);
            if (dbUser != null)
            {
                dbUser.PinHash = SessionService.HashPin(ResetPinValue.Trim());
                context.SaveChanges();
                ResetPinValue = string.Empty;
                LoadStaffUsers();
                SetStatus($"PIN updated for {dbUser.DisplayName}.", isError: false);
            }
        }

        [RelayCommand]
        private void AddStaffUser()
        {
            if (!SessionService.IsOwner) return;
            if (string.IsNullOrWhiteSpace(NewStaffName) || string.IsNullOrWhiteSpace(NewStaffPin) || NewStaffPin.Trim().Length < 4)
            {
                SetStatus("Enter staff name and a 4+ digit PIN.", isError: true);
                return;
            }

            using var context = new AppDbContext();
            var user = new StaffUser
            {
                DisplayName = NewStaffName.Trim(),
                Role = NewStaffRole,
                PinHash = SessionService.HashPin(NewStaffPin.Trim()),
                IsActive = true,
                CreatedAt = DateTime.Now
            };
            context.StaffUsers.Add(user);
            context.SaveChanges();

            NewStaffName = string.Empty;
            NewStaffPin = string.Empty;
            LoadStaffUsers();
            SetStatus($"Added {user.DisplayName} ({user.Role}).", isError: false);
        }

        [RelayCommand]
        private async Task BackupDatabase()
        {
            try
            {
                var dlg = new SaveFileDialog
                {
                    FileName = $"KarobarPOS_Backup_{DateTime.Now:yyyyMMdd_HHmm}.db",
                    Filter = "SQLite Database (*.db)|*.db"
                };

                string? chosenPath = dlg.ShowDialog() == true ? dlg.FileName : null;
                string savedPath = await _backupService.ExportDatabaseBackupAsync(chosenPath);
                SetStatus($"Database backed up to {savedPath}", isError: false);
            }
            catch (Exception ex)
            {
                SetStatus($"Backup failed: {ex.Message}", isError: true);
            }
        }

        [RelayCommand]
        private async Task RestoreDatabase()
        {
            if (!SessionService.IsOwner) return;
            try
            {
                var dlg = new OpenFileDialog
                {
                    Filter = "SQLite Database (*.db)|*.db"
                };
                if (dlg.ShowDialog() != true) return;

                await _backupService.RestoreDatabaseBackupAsync(dlg.FileName);
                RefreshData();
                WeakReferenceMessenger.Default.Send(DataChangedMessage.ProductUpdated);
                WeakReferenceMessenger.Default.Send(DataChangedMessage.CustomerUpdated);
                WeakReferenceMessenger.Default.Send(DataChangedMessage.TransactionUpdated);
                WeakReferenceMessenger.Default.Send(DataChangedMessage.SettingsUpdated);
                SetStatus("Database restored successfully.", isError: false);
            }
            catch (Exception ex)
            {
                SetStatus($"Restore failed: {ex.Message}", isError: true);
            }
        }

        private void SetStatus(string message, bool isError)
        {
            StatusMessage = message;
            IsErrorStatus = isError;
        }
    }
}
