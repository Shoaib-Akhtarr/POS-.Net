using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using POS.Models;
using POS.Services;

namespace POS.ViewModels
{
    public partial class PurchasesViewModel : ObservableObject, IRecipient<DataChangedMessage>
    {
        public List<string> AvailableDepartments { get; } = new()
        {
            "Electric", "Sanitary", "Hardware", "Tiles", "Paint", "Solar System", "Electronics"
        };

        public List<string> AvailableUnits { get; } = new()
        {
            "pcs", "m", "ft", "coil", "box", "sqft", "kg", "L"
        };

        [ObservableProperty] private ObservableCollection<Purchase> _purchases = new();
        [ObservableProperty] private ObservableCollection<Product> _availableProducts = new();
        [ObservableProperty] private Purchase? _selectedPurchaseHistory;
        [ObservableProperty] private bool _isDetailModalOpen;

        // Form Fields
        [ObservableProperty] private string _supplierName = string.Empty;
        [ObservableProperty] private decimal _costPrice;
        [ObservableProperty] private decimal _sellingPrice;
        [ObservableProperty] private decimal _quantity = 1m;
        [ObservableProperty] private bool _isNewProduct;
        [ObservableProperty] private Product? _selectedProduct;
        [ObservableProperty] private string _newProductName = string.Empty;
        [ObservableProperty] private string _barcode = string.Empty;
        [ObservableProperty] private string _brand = string.Empty;
        [ObservableProperty] private string _category = "Electric";
        [ObservableProperty] private string _unit = "pcs";
        [ObservableProperty] private string _packUnitName = string.Empty;
        [ObservableProperty] private decimal _unitsPerPack = 1m;
        [ObservableProperty] private string _defaultWarranty = string.Empty;

        [ObservableProperty] private string _statusMessage = string.Empty;
        [ObservableProperty] private bool _isErrorStatus;

        public bool IsOwner => SessionService.IsOwner;

        public PurchasesViewModel()
        {
            RefreshData();
            WeakReferenceMessenger.Default.Register(this);
        }

        public void Receive(DataChangedMessage message)
        {
            if (message.Value is "Product" or "Transaction")
            {
                App.Current?.Dispatcher.Invoke(RefreshData);
            }
            else if (message.Value == "Session")
            {
                App.Current?.Dispatcher.Invoke(() => OnPropertyChanged(nameof(IsOwner)));
            }
        }

        partial void OnSelectedProductChanged(Product? value)
        {
            if (value != null)
            {
                CostPrice = value.CostPrice;
                SellingPrice = value.SellingPrice;
                Category = string.IsNullOrWhiteSpace(value.Category) ? "Electric" : value.Category;
                Unit = string.IsNullOrWhiteSpace(value.Unit) ? "pcs" : value.Unit;
                Brand = value.Brand;
                Barcode = value.Barcode;
            }
        }

        [RelayCommand]
        private void IncrementQuantity() => Quantity += 1m;

        [RelayCommand]
        private void DecrementQuantity()
        {
            if (Quantity > 1m) Quantity -= 1m;
        }

        [RelayCommand]
        private void OpenDetail(Purchase? purchase)
        {
            if (purchase == null) return;
            SelectedPurchaseHistory = purchase;
            IsDetailModalOpen = true;
        }

        [RelayCommand]
        private void CloseDetail() => IsDetailModalOpen = false;

        public void RefreshData()
        {
            using var context = new AppDbContext();

            var dbPurchases = context.Purchases.OrderByDescending(p => p.PurchaseDate).ToList();
            Purchases = new ObservableCollection<Purchase>(dbPurchases);

            var products = context.Products.Where(p => !p.IsDeleted).OrderBy(p => p.Name).ToList();
            AvailableProducts = new ObservableCollection<Product>(products);
        }

        [RelayCommand]
        private void RecordPurchase()
        {
            StatusMessage = string.Empty;
            IsErrorStatus = false;

            if (string.IsNullOrWhiteSpace(SupplierName))
            {
                SetStatus("Please enter the supplier name.", isError: true);
                return;
            }
            if (CostPrice <= 0m)
            {
                SetStatus("Unit cost must be greater than 0.", isError: true);
                return;
            }
            if (Quantity <= 0m)
            {
                SetStatus("Quantity must be greater than 0.", isError: true);
                return;
            }

            using var context = new AppDbContext();
            Product? targetProduct;

            if (IsNewProduct)
            {
                string trimmedName = NewProductName.Trim();
                if (string.IsNullOrWhiteSpace(trimmedName))
                {
                    SetStatus("Please enter the new product name.", isError: true);
                    return;
                }

                targetProduct = context.Products.FirstOrDefault(p => p.Name.ToLower() == trimmedName.ToLower());
                if (targetProduct == null)
                {
                    targetProduct = new Product
                    {
                        Name = trimmedName,
                        Barcode = Barcode.Trim(),
                        Brand = Brand.Trim(),
                        Category = string.IsNullOrWhiteSpace(Category) ? "Electric" : Category.Trim(),
                        Unit = string.IsNullOrWhiteSpace(Unit) ? "pcs" : Unit.Trim(),
                        PackUnitName = PackUnitName.Trim(),
                        UnitsPerPack = UnitsPerPack > 0m ? UnitsPerPack : 1m,
                        DefaultWarranty = DefaultWarranty.Trim(),
                        Quantity = 0m,
                        CostPrice = CostPrice,
                        SellingPrice = SellingPrice > 0m ? SellingPrice : CostPrice,
                        CreatedAt = DateTime.Now,
                        UpdatedAt = DateTime.Now
                    };
                    context.Products.Add(targetProduct);
                    context.SaveChanges();
                }
                else
                {
                    // Reactivate if previously soft-deleted
                    targetProduct.IsDeleted = false;
                    if (!string.IsNullOrWhiteSpace(Barcode)) targetProduct.Barcode = Barcode.Trim();
                    if (!string.IsNullOrWhiteSpace(Brand)) targetProduct.Brand = Brand.Trim();
                    if (!string.IsNullOrWhiteSpace(Category)) targetProduct.Category = Category.Trim();
                    if (!string.IsNullOrWhiteSpace(Unit)) targetProduct.Unit = Unit.Trim();
                    targetProduct.UpdatedAt = DateTime.Now;
                }
            }
            else
            {
                if (SelectedProduct == null)
                {
                    SetStatus("Please select a product to restock.", isError: true);
                    return;
                }
                targetProduct = context.Products.Find(SelectedProduct.Id);
                if (targetProduct == null)
                {
                    SetStatus("Selected product was not found.", isError: true);
                    return;
                }
            }

            var purchase = new Purchase
            {
                ProductId = targetProduct.Id,
                ProductName = targetProduct.Name,
                SupplierName = SupplierName.Trim(),
                CostPrice = CostPrice,
                Quantity = Quantity,
                Unit = targetProduct.Unit,
                TotalCost = CostPrice * Quantity,
                CreatedBy = SessionService.CurrentUserName,
                PurchaseDate = DateTime.Now
            };

            targetProduct.Quantity += Quantity;
            targetProduct.CostPrice = CostPrice;
            if (SellingPrice > 0m) targetProduct.SellingPrice = SellingPrice;
            targetProduct.UpdatedAt = DateTime.Now;

            context.Purchases.Add(purchase);
            context.SaveChanges();

            string recordedName = targetProduct.Name;
            decimal recordedQty = Quantity;
            string recordedUnit = targetProduct.Unit;

            // Reset Form
            SupplierName = string.Empty;
            CostPrice = 0m;
            SellingPrice = 0m;
            Quantity = 1m;
            NewProductName = string.Empty;
            Barcode = string.Empty;
            Brand = string.Empty;
            PackUnitName = string.Empty;
            UnitsPerPack = 1m;
            DefaultWarranty = string.Empty;
            SelectedProduct = null;

            RefreshData();
            WeakReferenceMessenger.Default.Send(DataChangedMessage.ProductUpdated);
            WeakReferenceMessenger.Default.Send(DataChangedMessage.TransactionUpdated);

            SetStatus($"Restocked {(recordedQty % 1m == 0 ? recordedQty.ToString("N0") : recordedQty.ToString("0.##"))} {recordedUnit} of {recordedName}.", isError: false);
        }

        private void SetStatus(string msg, bool isError)
        {
            StatusMessage = msg;
            IsErrorStatus = isError;
        }
    }
}
