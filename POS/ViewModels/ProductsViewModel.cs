using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using POS.Models;
using POS.Services;

namespace POS.ViewModels
{
    public partial class ProductsViewModel : ObservableObject, IRecipient<DataChangedMessage>
    {
        [ObservableProperty]
        private ObservableCollection<Product> _allProducts = new();

        [ObservableProperty]
        private ObservableCollection<Product> _filteredProducts = new();

        [ObservableProperty]
        private ObservableCollection<string> _categories = new();

        [ObservableProperty]
        private string _searchText = string.Empty;

        [ObservableProperty]
        private string? _selectedCategory;

        [ObservableProperty]
        private int _totalRemainingStock;

        [ObservableProperty]
        private int _totalSKUs;

        // Modal State
        [ObservableProperty] private bool _isEditModalOpen;
        [ObservableProperty] private bool _isDeleteConfirmOpen;

        [ObservableProperty] private Product? _selectedProduct;
        private readonly BackupService _backupService;

        public ProductsViewModel()
        {
            _backupService = new BackupService();
            RefreshData();
            WeakReferenceMessenger.Default.Register(this);
        }

        public void Receive(DataChangedMessage message)
        {
            if (message.Value == "Product")
            {
                App.Current.Dispatcher.Invoke(RefreshData);
            }
        }

        public void RefreshData()
        {
            using var context = new AppDbContext();
            var dbProducts = context.Products.Where(p => !p.IsDeleted).ToList();
            
            AllProducts = new ObservableCollection<Product>(dbProducts);
            Categories = new ObservableCollection<string>(dbProducts.Select(p => p.Category).Distinct().OrderBy(c => c).ToList());
            ApplyFilter();
        }

        partial void OnSearchTextChanged(string value) => ApplyFilter();
        partial void OnSelectedCategoryChanged(string? value) => ApplyFilter();

        private void ApplyFilter()
        {
            var query = AllProducts.AsEnumerable();

            if (!string.IsNullOrWhiteSpace(SearchText))
            {
                var lower = SearchText.ToLower();
                query = query.Where(p => p.Name.ToLower().Contains(lower) || p.Category.ToLower().Contains(lower));
            }

            if (!string.IsNullOrWhiteSpace(SelectedCategory))
            {
                query = query.Where(p => p.Category == SelectedCategory);
            }

            var result = query.OrderBy(p => p.Name).ToList();
            FilteredProducts = new ObservableCollection<Product>(result);
            
            // Recalculate Summary
            TotalRemainingStock = result.Sum(p => p.Quantity);
            TotalSKUs = result.Count;
        }

        [RelayCommand]
        private void OpenEditModal(Product product)
        {
            SelectedProduct = product;
            IsEditModalOpen = true;
        }

        [RelayCommand]
        private void OpenDeleteConfirm(Product product)
        {
            SelectedProduct = product;
            IsDeleteConfirmOpen = true;
        }

        [RelayCommand]
        private void CloseModals()
        {
            IsEditModalOpen = false;
            IsDeleteConfirmOpen = false;
            SelectedProduct = null;
        }

        [RelayCommand]
        private async Task SaveChanges()
        {
            if (SelectedProduct == null) return;

            using var context = new AppDbContext();
            var dbProduct = await context.Products.FindAsync(SelectedProduct.Id);
            if (dbProduct != null)
            {
                dbProduct.Name = SelectedProduct.Name;
                dbProduct.SellingPrice = SelectedProduct.SellingPrice;
                dbProduct.UpdatedAt = DateTime.Now;

                await context.SaveChangesAsync();
                RefreshData();
                await _backupService.BackupProductsAsync(AllProducts);
                CloseModals();
            }
        }

        [RelayCommand]
        private async Task DeleteConfirmed()
        {
            if (SelectedProduct == null) return;

            using var context = new AppDbContext();
            var dbProduct = await context.Products.FindAsync(SelectedProduct.Id);
            if (dbProduct != null)
            {
                // Soft Delete
                dbProduct.IsDeleted = true;
                dbProduct.UpdatedAt = DateTime.Now;
                
                await context.SaveChangesAsync();
                RefreshData();
                await _backupService.BackupProductsAsync(AllProducts);
                CloseModals();
            }
        }
    }
}
