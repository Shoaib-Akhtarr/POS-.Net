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
        private static readonly string[] DefaultDepartments =
        {
            "All", "Electric", "Sanitary", "Hardware", "Tiles", "Paint", "Solar System", "Electronics"
        };

        public List<string> AvailableDepartments { get; } = new()
        {
            "Electric", "Sanitary", "Hardware", "Tiles", "Paint", "Solar System", "Electronics"
        };

        public List<string> AvailableUnits { get; } = new()
        {
            "pcs", "m", "ft", "coil", "box", "sqft", "kg", "L"
        };

        [ObservableProperty] private ObservableCollection<Product> _allProducts = new();
        [ObservableProperty] private ObservableCollection<Product> _filteredProducts = new();
        [ObservableProperty] private ObservableCollection<string> _categories = new();
        [ObservableProperty] private string _searchText = string.Empty;
        [ObservableProperty] private string _selectedCategory = "All";
        [ObservableProperty] private decimal _totalRemainingStock;
        [ObservableProperty] private int _totalSKUs;

        // Modal State
        [ObservableProperty] private bool _isEditModalOpen;
        [ObservableProperty] private bool _isAddMode;
        [ObservableProperty] private bool _isDeleteConfirmOpen;
        [ObservableProperty] private Product? _selectedProduct;
        [ObservableProperty] private string _modalError = string.Empty;

        // Buffered Editor Fields (prevents mutating grid items before Save)
        [ObservableProperty] private int _editId;
        [ObservableProperty] private string _editName = string.Empty;
        [ObservableProperty] private string _editBarcode = string.Empty;
        [ObservableProperty] private string _editBrand = string.Empty;
        [ObservableProperty] private string _editCategory = "Electric";
        [ObservableProperty] private string _editUnit = "pcs";
        [ObservableProperty] private string _editPackUnitName = string.Empty;
        [ObservableProperty] private decimal _editUnitsPerPack = 1m;
        [ObservableProperty] private decimal _editCostPrice;
        [ObservableProperty] private decimal _editSellingPrice;
        [ObservableProperty] private decimal _editQuantity;
        [ObservableProperty] private decimal _editLowStockThreshold = 10m;
        [ObservableProperty] private string _editDefaultWarranty = string.Empty;

        private readonly BackupService _backupService;

        public bool IsOwner => SessionService.IsOwner;
        public string ModalTitle => IsAddMode ? "Add New Product" : "Edit Product Details";

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
                App.Current?.Dispatcher.Invoke(RefreshData);
            }
            else if (message.Value == "Session")
            {
                App.Current?.Dispatcher.Invoke(() => OnPropertyChanged(nameof(IsOwner)));
            }
        }

        partial void OnSearchTextChanged(string value) => ApplyFilter();
        partial void OnSelectedCategoryChanged(string value) => ApplyFilter();
        partial void OnIsAddModeChanged(bool value) => OnPropertyChanged(nameof(ModalTitle));

        public void RefreshData()
        {
            using var context = new AppDbContext();
            var dbProducts = context.Products.Where(p => !p.IsDeleted).OrderBy(p => p.Name).ToList();
            AllProducts = new ObservableCollection<Product>(dbProducts);

            var cats = new List<string>(DefaultDepartments);
            foreach (var c in dbProducts.Select(p => p.Category).Where(c => !string.IsNullOrWhiteSpace(c)).Distinct(StringComparer.OrdinalIgnoreCase))
            {
                if (!cats.Any(x => string.Equals(x, c, StringComparison.OrdinalIgnoreCase)))
                    cats.Add(c.Trim());
            }
            Categories = new ObservableCollection<string>(cats);
            ApplyFilter();
        }

        private void ApplyFilter()
        {
            var query = AllProducts.AsEnumerable();

            if (!string.IsNullOrWhiteSpace(SelectedCategory) && !string.Equals(SelectedCategory, "All", StringComparison.OrdinalIgnoreCase))
            {
                query = query.Where(p => string.Equals(p.Category, SelectedCategory, StringComparison.OrdinalIgnoreCase));
            }

            if (!string.IsNullOrWhiteSpace(SearchText))
            {
                var lower = SearchText.Trim().ToLowerInvariant();
                query = query.Where(p =>
                    p.Name.ToLowerInvariant().Contains(lower) ||
                    p.Category.ToLowerInvariant().Contains(lower) ||
                    (!string.IsNullOrEmpty(p.Barcode) && p.Barcode.ToLowerInvariant().Contains(lower)) ||
                    (!string.IsNullOrEmpty(p.Brand) && p.Brand.ToLowerInvariant().Contains(lower)));
            }

            var result = query.OrderBy(p => p.Name).ToList();
            FilteredProducts = new ObservableCollection<Product>(result);

            TotalRemainingStock = result.Sum(p => p.Quantity);
            TotalSKUs = result.Count;
        }

        [RelayCommand]
        private void ChangeCategory(string category) => SelectedCategory = category;

        [RelayCommand]
        private void OpenAddModal()
        {
            if (!SessionService.IsOwner) return;
            IsAddMode = true;
            EditId = 0;
            EditName = string.Empty;
            EditBarcode = string.Empty;
            EditBrand = string.Empty;
            EditCategory = string.Equals(SelectedCategory, "All", StringComparison.OrdinalIgnoreCase) ? "Electric" : SelectedCategory;
            EditUnit = "pcs";
            EditPackUnitName = string.Empty;
            EditUnitsPerPack = 1m;
            EditCostPrice = 0m;
            EditSellingPrice = 0m;
            EditQuantity = 0m;
            EditLowStockThreshold = 10m;
            EditDefaultWarranty = string.Empty;
            ModalError = string.Empty;
            IsEditModalOpen = true;
        }

        [RelayCommand]
        private void OpenEditModal(Product? product)
        {
            if (product == null || !SessionService.IsOwner) return;
            SelectedProduct = product;
            IsAddMode = false;
            EditId = product.Id;
            EditName = product.Name;
            EditBarcode = product.Barcode;
            EditBrand = product.Brand;
            EditCategory = string.IsNullOrWhiteSpace(product.Category) ? "Electric" : product.Category;
            EditUnit = string.IsNullOrWhiteSpace(product.Unit) ? "pcs" : product.Unit;
            EditPackUnitName = product.PackUnitName;
            EditUnitsPerPack = product.UnitsPerPack > 0m ? product.UnitsPerPack : 1m;
            EditCostPrice = product.CostPrice;
            EditSellingPrice = product.SellingPrice;
            EditQuantity = product.Quantity;
            EditLowStockThreshold = product.LowStockThreshold;
            EditDefaultWarranty = product.DefaultWarranty;
            ModalError = string.Empty;
            IsEditModalOpen = true;
        }

        [RelayCommand]
        private void OpenDeleteConfirm(Product? product)
        {
            if (product == null || !SessionService.IsOwner) return;
            SelectedProduct = product;
            IsDeleteConfirmOpen = true;
        }

        [RelayCommand]
        private void CloseModals()
        {
            IsEditModalOpen = false;
            IsDeleteConfirmOpen = false;
            SelectedProduct = null;
            ModalError = string.Empty;
        }

        [RelayCommand]
        private async Task SaveChanges()
        {
            if (!SessionService.IsOwner) return;

            if (string.IsNullOrWhiteSpace(EditName))
            {
                ModalError = "Product name is required.";
                return;
            }
            if (EditSellingPrice < 0m || EditCostPrice < 0m || EditQuantity < 0m)
            {
                ModalError = "Prices and stock cannot be negative.";
                return;
            }

            using var context = new AppDbContext();
            if (IsAddMode)
            {
                string trimmedName = EditName.Trim();
                bool duplicate = context.Products.Any(p => !p.IsDeleted && p.Name.ToLower() == trimmedName.ToLower());
                if (duplicate)
                {
                    ModalError = "A product with this name already exists.";
                    return;
                }

                var product = new Product
                {
                    Name = trimmedName,
                    Barcode = EditBarcode.Trim(),
                    Brand = EditBrand.Trim(),
                    Category = string.IsNullOrWhiteSpace(EditCategory) ? "Hardware" : EditCategory.Trim(),
                    Unit = string.IsNullOrWhiteSpace(EditUnit) ? "pcs" : EditUnit.Trim(),
                    PackUnitName = EditPackUnitName.Trim(),
                    UnitsPerPack = EditUnitsPerPack > 0m ? EditUnitsPerPack : 1m,
                    CostPrice = EditCostPrice,
                    SellingPrice = EditSellingPrice,
                    Quantity = EditQuantity,
                    LowStockThreshold = EditLowStockThreshold >= 0m ? EditLowStockThreshold : 10m,
                    DefaultWarranty = EditDefaultWarranty.Trim(),
                    CreatedAt = DateTime.Now,
                    UpdatedAt = DateTime.Now
                };
                context.Products.Add(product);
                await context.SaveChangesAsync();
            }
            else
            {
                var dbProduct = await context.Products.FindAsync(EditId);
                if (dbProduct == null)
                {
                    ModalError = "Product record not found.";
                    return;
                }

                dbProduct.Name = EditName.Trim();
                dbProduct.Barcode = EditBarcode.Trim();
                dbProduct.Brand = EditBrand.Trim();
                dbProduct.Category = string.IsNullOrWhiteSpace(EditCategory) ? "Hardware" : EditCategory.Trim();
                dbProduct.Unit = string.IsNullOrWhiteSpace(EditUnit) ? "pcs" : EditUnit.Trim();
                dbProduct.PackUnitName = EditPackUnitName.Trim();
                dbProduct.UnitsPerPack = EditUnitsPerPack > 0m ? EditUnitsPerPack : 1m;
                dbProduct.CostPrice = EditCostPrice;
                dbProduct.SellingPrice = EditSellingPrice;
                dbProduct.Quantity = EditQuantity;
                dbProduct.LowStockThreshold = EditLowStockThreshold >= 0m ? EditLowStockThreshold : 10m;
                dbProduct.DefaultWarranty = EditDefaultWarranty.Trim();
                dbProduct.UpdatedAt = DateTime.Now;

                await context.SaveChangesAsync();
            }

            RefreshData();
            await _backupService.BackupProductsAsync(AllProducts);
            WeakReferenceMessenger.Default.Send(DataChangedMessage.ProductUpdated);
            CloseModals();
        }

        [RelayCommand]
        private async Task DeleteConfirmed()
        {
            if (SelectedProduct == null || !SessionService.IsOwner) return;

            using var context = new AppDbContext();
            var dbProduct = await context.Products.FindAsync(SelectedProduct.Id);
            if (dbProduct != null)
            {
                dbProduct.IsDeleted = true;
                dbProduct.UpdatedAt = DateTime.Now;
                await context.SaveChangesAsync();
            }

            RefreshData();
            await _backupService.BackupProductsAsync(AllProducts);
            WeakReferenceMessenger.Default.Send(DataChangedMessage.ProductUpdated);
            CloseModals();
        }
    }
}
