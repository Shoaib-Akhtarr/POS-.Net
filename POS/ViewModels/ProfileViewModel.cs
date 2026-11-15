using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
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
        [ObservableProperty] private string _printerName = string.Empty;
        [ObservableProperty] private string _preferredPrintFormat = "A4";
        [ObservableProperty] private bool _useBitmapThermalForUrdu;
        [ObservableProperty] private ObservableCollection<string> _installedPrinters = new();

        public List<string> PrintFormats { get; } = new() { "A4" };

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

        // Auto-backup folder (Google Drive / OneDrive / USB)
        [ObservableProperty] private string _autoBackupFolder = string.Empty;

        // FBR / PRA Fiscal Tax Integration Settings
        [ObservableProperty] private bool _isFbrTaxEnabled;
        [ObservableProperty] private string _fbrEnvironment = "Sandbox"; // Sandbox or Live
        [ObservableProperty] private string _fbrPosId = "102345";
        [ObservableProperty] private string _fbrBearerToken = string.Empty;
        [ObservableProperty] private string _fbrNtn = string.Empty;
        [ObservableProperty] private string _fbrStrn = string.Empty;
        [ObservableProperty] private decimal _fbrDefaultTaxRate = 18.0m;
        [ObservableProperty] private bool _fbrAutoSync = true;
        [ObservableProperty] private bool _fbrOfflineModeAllowed = true;
        [ObservableProperty] private int _pendingOfflineFbrCount;
        [ObservableProperty] private string _fbrTestResult = string.Empty;
        [ObservableProperty] private bool _isFbrTesting;
        [ObservableProperty] private bool _isFbrSyncing;

        // Shift Summary & Role Metadata
        [ObservableProperty] private int _todayShiftSalesCount;
        [ObservableProperty] private decimal _todayShiftSalesVolume;
        [ObservableProperty] private string _roleDescription = string.Empty;
        [ObservableProperty] private string _userInitials = "U";
        [ObservableProperty] private bool _isShiftOpen;
        [ObservableProperty] private decimal _shiftOpeningCash;
        [ObservableProperty] private string _shiftStatusMessage = string.Empty;

        public bool IsOwner => SessionService.IsOwner;
        public string CurrentUserName => SessionService.CurrentUserName;
        public string CurrentUserRole => SessionService.CurrentUser?.Role ?? "Cashier";
        public string DatabasePath => AppDbContext.GetDatabasePath();

        private readonly PrintService _printService;
        private readonly BackupService _backupService;
        private readonly FbrTaxService _fbrTaxService;

        public ProfileViewModel()
        {
            _printService = new PrintService();
            _backupService = new BackupService();
            _fbrTaxService = new FbrTaxService();
            RefreshData();
            WeakReferenceMessenger.Default.Register(this);
        }

        public void Receive(DataChangedMessage message)
        {
            if (message.Value is "Session" or "Transaction")
            {
                App.Current?.Dispatcher.Invoke(() =>
                {
                    OnPropertyChanged(nameof(IsOwner));
                    OnPropertyChanged(nameof(CurrentUserName));
                    OnPropertyChanged(nameof(CurrentUserRole));
                    RefreshData();
                });
            }
        }

        [RelayCommand]
        private void OpenShift()
        {
            if (_isShiftOpen)
            {
                SetShiftStatus("Shift is already open.");
                return;
            }

            if (_shiftOpeningCash <= 0m)
            {
                SetShiftStatus("Please enter opening cash amount.");
                return;
            }

            try
            {
                using var context = new AppDbContext();
                var newShift = new CashShift
                {
                    CashierId = SessionService.CurrentUser?.Id ?? 0,
                    CashierName = SessionService.CurrentUserName,
                    OpenedAt = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
                    OpeningCash = _shiftOpeningCash,
                    Status = "Open",
                    CreatedAt = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")
                };
                context.CashShifts.Add(newShift);
                context.SaveChanges();

                _isShiftOpen = true;
                _shiftOpeningCash = 0m;
                SetShiftStatus($"Shift opened successfully for {SessionService.CurrentUserName}.");
                NotificationService.ShowInfo($"Shift opened. Opening cash: Rs {_shiftOpeningCash:N0}");
            }
            catch (Exception ex)
            {
                SetShiftStatus($"Failed to open shift: {ex.Message}");
                NotificationService.ShowError($"Shift open failed: {ex.Message}");
            }
        }

        [RelayCommand]
        private void CloseShift()
        {
            if (!_isShiftOpen)
            {
                SetShiftStatus("No shift is currently open.");
                return;
            }

            // Calculate variance
            decimal countedCash = _shiftOpeningCash; // In real app, this would be physical count
            decimal expectedCash = _shiftOpeningCash; // Revenue + starting cash
            decimal variance = countedCash - expectedCash;

            try
            {
                using var context = new AppDbContext();
                var shift = context.CashShifts.OrderByDescending(s => s.OpenedAt).FirstOrDefault(s => s.Status == "Open");
                if (shift != null)
                {
                    shift.ClosedAt = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
                    shift.ClosingCash = countedCash;
                    shift.ExpectedCash = _shiftOpeningCash;
                    shift.Variance = variance;
                    shift.Status = (Math.Abs(variance) < 0.01m) ? "Closed" : "Z-Reported";

                    context.SaveChanges();
                }

                _isShiftOpen = false;
                _shiftOpeningCash = 0m;
                SetShiftStatus($"Shift closed. Variance: Rs {variance:N0} {(variance >= 0 ? " (Surplus)" : " (Shortage)")}");
                NotificationService.ShowInfo($"Shift closed. Variance: Rs {variance:N0}");
            }
            catch (Exception ex)
            {
                SetShiftStatus($"Failed to close shift: {ex.Message}");
                NotificationService.ShowError($"Shift close failed: {ex.Message}");
            }
        }

        [RelayCommand]
        private void LogOut()
        {
            SessionService.SignOut();
            NotificationService.ShowInfo("Logged out successfully.");
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
            AutoBackupFolder = settings.AutoBackupFolder;

            // Load FBR Settings
            IsFbrTaxEnabled = settings.IsFbrTaxEnabled;
            FbrEnvironment = string.IsNullOrWhiteSpace(settings.FbrEnvironment) ? "Sandbox" : settings.FbrEnvironment;
            FbrPosId = string.IsNullOrWhiteSpace(settings.FbrPosId) ? "102345" : settings.FbrPosId;
            FbrBearerToken = settings.FbrBearerToken;
            FbrNtn = settings.FbrNtn;
            FbrStrn = settings.FbrStrn;
            FbrDefaultTaxRate = settings.FbrDefaultTaxRate > 0 ? settings.FbrDefaultTaxRate : 18.0m;
            FbrAutoSync = settings.FbrAutoSync;
            FbrOfflineModeAllowed = settings.FbrOfflineModeAllowed;
            PendingOfflineFbrCount = FbrTaxService.GetPendingOfflineCount();

            var printers = PrintService.GetInstalledPrinters();
            if (!string.IsNullOrWhiteSpace(PrinterName) && !printers.Contains(PrinterName))
            {
                printers.Insert(0, PrinterName);
            }
            InstalledPrinters = new ObservableCollection<string>(printers);

            LoadStaffUsers();

            // Calculate current shift stats
            var today = DateTime.Today;
            using var context = new AppDbContext();
            var todaySales = context.Sales.Where(s => s.CreatedAt >= today && s.RecordType == "Sale").ToList();
            if (!SessionService.IsOwner && SessionService.CurrentUser != null)
            {
                var cashierSales = todaySales.Where(s => string.Equals(s.CreatedBy, SessionService.CurrentUserName, StringComparison.OrdinalIgnoreCase)).ToList();
                TodayShiftSalesCount = cashierSales.Count;
                TodayShiftSalesVolume = cashierSales.Sum(s => s.TotalAmount);
            }
            else
            {
                TodayShiftSalesCount = todaySales.Count;
                TodayShiftSalesVolume = todaySales.Sum(s => s.TotalAmount);
            }

            string name = SessionService.CurrentUserName;
            UserInitials = string.IsNullOrWhiteSpace(name) ? "U" : (name.Length >= 2 ? name.Substring(0, 2).ToUpperInvariant() : name.ToUpperInvariant());
            RoleDescription = IsOwner
                ? "Full Owner Access · Product catalog pricing, Khata ledgers, Staff PINs, FBR Taxes, and System Backups."
                : "Cashier Terminal Access · Rapid POS checkout, holding active carts, and customer receipts printing.";
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
            settings.PrinterName = PrinterName?.Trim() ?? string.Empty;
            settings.PreferredPrintFormat = "A4";
            settings.UseBitmapThermalForUrdu = false;
            settings.Language = CurrentLanguage;
            settings.Theme = CurrentTheme;
            settings.AutoBackupFolder = AutoBackupFolder.Trim();

            // Save FBR Settings
            settings.IsFbrTaxEnabled = IsFbrTaxEnabled;
            settings.FbrEnvironment = string.IsNullOrWhiteSpace(FbrEnvironment) ? "Sandbox" : FbrEnvironment.Trim();
            settings.FbrPosId = string.IsNullOrWhiteSpace(FbrPosId) ? "102345" : FbrPosId.Trim();
            settings.FbrBearerToken = FbrBearerToken.Trim();
            settings.FbrNtn = FbrNtn.Trim();
            settings.FbrStrn = FbrStrn.Trim();
            settings.FbrDefaultTaxRate = FbrDefaultTaxRate > 0 ? FbrDefaultTaxRate : 18.0m;
            settings.FbrAutoSync = FbrAutoSync;
            settings.FbrOfflineModeAllowed = FbrOfflineModeAllowed;
            settings.UpdatedAt = DateTime.Now;

            context.SaveChanges();
            WeakReferenceMessenger.Default.Send(DataChangedMessage.SettingsUpdated);
            SetStatus("Store branding, printer & FBR tax settings saved successfully.", isError: false);
        }

        [RelayCommand]
        private void SetFbrEnvironment(string env)
        {
            FbrEnvironment = env;
            SaveStoreSettings();
            SetStatus($"FBR Tax mode set to {env}.", isError: false);
        }

        [RelayCommand]
        private async Task TestFbrConnection()
        {
            IsFbrTesting = true;
            FbrTestResult = "Connecting to FBR Gateway...";
            try
            {
                var testSettings = new StoreSettings
                {
                    IsFbrTaxEnabled = IsFbrTaxEnabled,
                    FbrEnvironment = FbrEnvironment,
                    FbrPosId = FbrPosId,
                    FbrBearerToken = FbrBearerToken,
                    FbrNtn = FbrNtn,
                    FbrStrn = FbrStrn,
                    FbrDefaultTaxRate = FbrDefaultTaxRate
                };

                var (success, message) = await _fbrTaxService.TestConnectionAsync(testSettings);
                FbrTestResult = message;
                SetStatus(message, isError: !success);
            }
            catch (Exception ex)
            {
                FbrTestResult = $"Test error: {ex.Message}";
                SetStatus(FbrTestResult, isError: true);
            }
            finally
            {
                IsFbrTesting = false;
            }
        }

        [RelayCommand]
        private async Task SyncPendingFbrInvoices()
        {
            IsFbrSyncing = true;
            try
            {
                var settings = ThemeService.GetSettings();
                var (synced, failed, message) = await _fbrTaxService.SyncPendingInvoicesAsync(settings);
                PendingOfflineFbrCount = FbrTaxService.GetPendingOfflineCount();
                SetStatus(message, isError: failed > 0);
            }
            catch (Exception ex)
            {
                SetStatus($"Sync failed: {ex.Message}", isError: true);
            }
            finally
            {
                IsFbrSyncing = false;
            }
        }

        [RelayCommand]
        private void BrowseAutoBackupFolder()
        {
            // OpenFolderDialog is available in WPF on .NET 8+ (we are on net10.0-windows)
            var dlg = new Microsoft.Win32.OpenFolderDialog
            {
                Title = "Select folder for automatic backups (e.g. Google Drive, OneDrive, USB)",
                InitialDirectory = Directory.Exists(AutoBackupFolder) ? AutoBackupFolder : Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments)
            };

            if (dlg.ShowDialog() == true)
            {
                AutoBackupFolder = dlg.FolderName;
                SetStatus($"Auto-backup folder set: {AutoBackupFolder}  —  Save settings to apply.", isError: false);
            }
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
                    FileName = $"KarobarPOS_Backup_{DateTime.Now:yyyyMMdd_HHmm}.kposbackup",
                    DefaultExt = ".kposbackup",
                    AddExtension = true,
                    Filter = "Karobar POS portable backup (*.kposbackup)|*.kposbackup"
                };

                string? chosenPath = dlg.ShowDialog() == true ? dlg.FileName : null;
                string savedPath = await _backupService.ExportDatabaseBackupAsync(chosenPath);
                SetStatus($"Database backed up to {savedPath} (Spreadsheets & auto-cleanup updated)", isError: false);
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
                    Filter = "Karobar POS backups (*.kposbackup;*.db)|*.kposbackup;*.db|Portable backup (*.kposbackup)|*.kposbackup|Legacy SQLite database (*.db)|*.db"
                };
                if (dlg.ShowDialog() != true) return;

                var answer = System.Windows.MessageBox.Show(
                    "Restoring replaces the current POS data with the selected backup. A safety backup of this computer's current data will be created first. Continue?",
                    "Restore POS data",
                    System.Windows.MessageBoxButton.YesNo,
                    System.Windows.MessageBoxImage.Warning);
                if (answer != System.Windows.MessageBoxResult.Yes) return;

                await _backupService.RestoreDatabaseBackupAsync(dlg.FileName);
                RefreshData();
                WeakReferenceMessenger.Default.Send(DataChangedMessage.ProductUpdated);
                WeakReferenceMessenger.Default.Send(DataChangedMessage.CustomerUpdated);
                WeakReferenceMessenger.Default.Send(DataChangedMessage.TransactionUpdated);
                WeakReferenceMessenger.Default.Send(DataChangedMessage.SettingsUpdated);
                SetStatus("Backup restored successfully. The app is now using the restored data.", isError: false);
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
            if (isError)
            {
                NotificationService.ShowError(message);
            }
            else
            {
                NotificationService.ShowSuccess(message);
            }
        }

        private void SetShiftStatus(string message)
        {
            ShiftStatusMessage = message;
            SetStatus(message, false);
        }
    }
}
