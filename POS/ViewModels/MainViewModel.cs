using CommunityToolkit.Mvvm.ComponentModel;
using System.Collections.ObjectModel;
using POS.Models;

namespace POS.ViewModels
{
    public partial class MainViewModel : ObservableObject
    {
        [ObservableProperty]
        private object _currentViewModel;

        [ObservableProperty]
        private bool _isSidebarCollapsed = false;

        [CommunityToolkit.Mvvm.Input.RelayCommand]
        private void ToggleSidebar()
        {
            IsSidebarCollapsed = !IsSidebarCollapsed;
        }

        // View Models
        private readonly HomeViewModel _homeViewModel;
        private readonly POSViewModel _posViewModel;
        private readonly ProductsViewModel _productsViewModel;
        private readonly PurchasesViewModel _purchasesViewModel;
        private readonly CustomersViewModel _customersViewModel;
        private readonly RecordsViewModel _recordsViewModel;

        public MainViewModel()
        {
            _homeViewModel = new HomeViewModel();
            _posViewModel = new POSViewModel();
            _productsViewModel = new ProductsViewModel();
            _purchasesViewModel = new PurchasesViewModel();
            _customersViewModel = new CustomersViewModel();
            _recordsViewModel = new RecordsViewModel();

            // Default View
            CurrentViewModel = _posViewModel;
        }

        [CommunityToolkit.Mvvm.Input.RelayCommand]
        private void NavigateToCustomers()
        {
            _customersViewModel.LoadCustomers();
            CurrentViewModel = _customersViewModel;
        }

        [CommunityToolkit.Mvvm.Input.RelayCommand]
        private void NavigateToHome()
        {
            CurrentViewModel = _homeViewModel;
        }

        [CommunityToolkit.Mvvm.Input.RelayCommand]
        private void NavigateToPOS()
        {
            CurrentViewModel = _posViewModel;
        }

        [CommunityToolkit.Mvvm.Input.RelayCommand]
        private void NavigateToProducts()
        {
            _productsViewModel.RefreshData();
            CurrentViewModel = _productsViewModel;
        }

        [CommunityToolkit.Mvvm.Input.RelayCommand]
        private void NavigateToPurchases()
        {
            _purchasesViewModel.RefreshData();
            CurrentViewModel = _purchasesViewModel;
        }

        [CommunityToolkit.Mvvm.Input.RelayCommand]
        private void NavigateToRecords()
        {
            _recordsViewModel.RefreshData();
            CurrentViewModel = _recordsViewModel;
        }


    }
}
