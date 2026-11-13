using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using POS.Models;
using POS.Services;

namespace POS.ViewModels
{
    public partial class MainViewModel : ObservableObject, IRecipient<DataChangedMessage>
    {
        [ObservableProperty] private object _currentViewModel = null!;
        [ObservableProperty] private bool _isSidebarCollapsed;
        [ObservableProperty] private string _shopName = "Aizaz Fareed Center";
        [ObservableProperty] private string _currentUserName = "Counter Cashier";
        [ObservableProperty] private string _currentUserRole = "Cashier";
        [ObservableProperty] private bool _isOwner;

        // Staff Login / Switch PIN Overlay
        [ObservableProperty] private bool _isLoginModalOpen = true;
        [ObservableProperty] private ObservableCollection<StaffUser> _staffUsers = new();
        [ObservableProperty] private StaffUser? _selectedLoginUser;
        [ObservableProperty] private string _loginPin = string.Empty;
        [ObservableProperty] private string _loginError = string.Empty;

        // ViewModels
        private readonly HomeViewModel _homeViewModel = null!;
        private readonly POSViewModel _posViewModel = null!;
        private readonly ProductsViewModel _productsViewModel = null!;
        private readonly PurchasesViewModel _purchasesViewModel = null!;
        private readonly CustomersViewModel _customersViewModel = null!;
        private readonly KhataViewModel _khataViewModel = null!;
        private readonly InstallmentsViewModel _installmentsViewModel = null!;
        private readonly RecordsViewModel _recordsViewModel = null!;
        private readonly ProfileViewModel _profileViewModel = null!;

        public MainViewModel()
        {
            if (DesignerProperties.GetIsInDesignMode(new DependencyObject()))
            {
                return;
            }

            DatabaseInitializer.Initialize();
            SessionService.SignInAsDefaultCashier();

            _homeViewModel = new HomeViewModel();
            _posViewModel = new POSViewModel();
            _productsViewModel = new ProductsViewModel();
            _purchasesViewModel = new PurchasesViewModel();
            _customersViewModel = new CustomersViewModel();
            _khataViewModel = new KhataViewModel();
            _installmentsViewModel = new InstallmentsViewModel();
            _recordsViewModel = new RecordsViewModel();
            _profileViewModel = new ProfileViewModel();

            _currentViewModel = _posViewModel;

            LoadHeaderMetadata();
            LoadLoginUsers();
            WeakReferenceMessenger.Default.Register(this);
        }

        public void Receive(DataChangedMessage message)
        {
            if (message.Value == "Settings")
            {
                App.Current?.Dispatcher.Invoke(LoadHeaderMetadata);
            }
            else if (message.Value == "Session")
            {
                App.Current?.Dispatcher.Invoke(() =>
                {
                    CurrentUserName = SessionService.CurrentUserName;
                    CurrentUserRole = SessionService.CurrentUser?.Role ?? "Cashier";
                    IsOwner = SessionService.IsOwner;
                });
            }
        }

        private void LoadHeaderMetadata()
        {
            var settings = ThemeService.GetSettings();
            ShopName = string.IsNullOrWhiteSpace(settings.ShopName) ? "Aizaz Fareed Center" : settings.ShopName;
            CurrentUserName = SessionService.CurrentUserName;
            CurrentUserRole = SessionService.CurrentUser?.Role ?? "Cashier";
            IsOwner = SessionService.IsOwner;
        }

        private void LoadLoginUsers()
        {
            var users = SessionService.GetActiveUsers();
            StaffUsers = new ObservableCollection<StaffUser>(users);
            SelectedLoginUser = StaffUsers.FirstOrDefault();
        }

        [RelayCommand]
        private void ToggleSidebar() => IsSidebarCollapsed = !IsSidebarCollapsed;

        [RelayCommand]
        private void OpenSwitchUser()
        {
            LoadLoginUsers();
            LoginPin = string.Empty;
            LoginError = string.Empty;
            IsLoginModalOpen = true;
        }

        [RelayCommand]
        private void SignInSelectedUser()
        {
            LoginError = string.Empty;
            if (SelectedLoginUser == null)
            {
                LoginError = "Please select a staff member.";
                return;
            }

            if (SessionService.SignIn(SelectedLoginUser.Id, LoginPin.Trim()))
            {
                LoginPin = string.Empty;
                IsLoginModalOpen = false;
                return;
            }

            LoginError = "Incorrect PIN. Try again.";
        }

        [RelayCommand]
        private void QuickCashierSignIn()
        {
            SessionService.SignInAsDefaultCashier();
            LoginPin = string.Empty;
            LoginError = string.Empty;
            IsLoginModalOpen = false;
            CurrentViewModel = _posViewModel;
        }

        [RelayCommand]
        private void NavigateToHome()
        {
            _homeViewModel.RefreshData();
            CurrentViewModel = _homeViewModel;
        }

        [RelayCommand]
        private void NavigateToPOS() => CurrentViewModel = _posViewModel;

        [RelayCommand]
        private void NavigateToProducts()
        {
            _productsViewModel.RefreshData();
            CurrentViewModel = _productsViewModel;
        }

        [RelayCommand]
        private void NavigateToPurchases()
        {
            _purchasesViewModel.RefreshData();
            CurrentViewModel = _purchasesViewModel;
        }

        [RelayCommand]
        private void NavigateToCustomers()
        {
            _customersViewModel.LoadCustomers();
            CurrentViewModel = _customersViewModel;
        }

        [RelayCommand]
        private void NavigateToKhata()
        {
            _khataViewModel.RefreshData();
            CurrentViewModel = _khataViewModel;
        }

        [RelayCommand]
        private void NavigateToInstallments()
        {
            _installmentsViewModel.RefreshData();
            CurrentViewModel = _installmentsViewModel;
        }

        [RelayCommand]
        private void NavigateToRecords()
        {
            _recordsViewModel.RefreshData();
            CurrentViewModel = _recordsViewModel;
        }

        [RelayCommand]
        private void NavigateToProfile()
        {
            _profileViewModel.RefreshData();
            CurrentViewModel = _profileViewModel;
        }
    }
}
