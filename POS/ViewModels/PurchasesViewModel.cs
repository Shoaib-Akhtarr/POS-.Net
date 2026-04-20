using System.Collections.ObjectModel;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Messaging;
using POS.Models;

namespace POS.ViewModels
{
    public partial class PurchasesViewModel : ObservableObject
    {
        [ObservableProperty]
        private ObservableCollection<Purchase> _purchases;

        [ObservableProperty]
        private ObservableCollection<Product> _availableProducts;

        [ObservableProperty]
        private Purchase? _selectedPurchaseHistory;

        [ObservableProperty]
        private bool _isDetailModalOpen;

        // Form Fields
        [ObservableProperty]
        private string _supplierName = string.Empty;

        [ObservableProperty]
        private decimal _costPrice;

        [ObservableProperty]
        private decimal _sellingPrice;

        [ObservableProperty]
        private int _quantity = 1;

        [ObservableProperty]
        private bool _isNewProduct = false;

        [ObservableProperty]
        private Product? _selectedProduct;

        partial void OnSelectedProductChanged(Product? value)
        {
            if (value != null)
            {
                CostPrice = value.CostPrice;
                SellingPrice = value.SellingPrice;
            }
        }

        [CommunityToolkit.Mvvm.Input.RelayCommand]
        private void IncrementQuantity() => Quantity++;

        [CommunityToolkit.Mvvm.Input.RelayCommand]
        private void DecrementQuantity()
        {
            if (Quantity > 1) Quantity--;
        }

        [CommunityToolkit.Mvvm.Input.RelayCommand]
        private void OpenDetail(Purchase purchase)
        {
            SelectedPurchaseHistory = purchase;
            IsDetailModalOpen = true;
        }

        [CommunityToolkit.Mvvm.Input.RelayCommand]
        private void CloseDetail() => IsDetailModalOpen = false;

        [ObservableProperty]
        private string _newProductName = string.Empty;

        [ObservableProperty]
        private string _category = "General";

        public PurchasesViewModel()
        {
            Purchases = new ObservableCollection<Purchase>();
            AvailableProducts = new ObservableCollection<Product>();
            RefreshData();
        }

        public void RefreshData()
        {
            using (var context = new AppDbContext())
            {
                // Refresh History
                var dbPurchases = context.Purchases.OrderByDescending(p => p.PurchaseDate).ToList();
                Purchases.Clear();
                foreach (var p in dbPurchases) Purchases.Add(p);

                // Refresh Product List for selection
                var products = context.Products.OrderBy(p => p.Name).ToList();
                AvailableProducts.Clear();
                foreach (var p in products) AvailableProducts.Add(p);
            }
        }

        [CommunityToolkit.Mvvm.Input.RelayCommand]
        private void RecordPurchase()
        {
            if (string.IsNullOrWhiteSpace(SupplierName)) return;
            if (CostPrice <= 0 || Quantity <= 0) return;

            using (var context = new AppDbContext())
            {
                Product? targetProduct = null;

                if (IsNewProduct)
                {
                    if (string.IsNullOrWhiteSpace(NewProductName)) return;
                    
                    // Check if exists
                    targetProduct = context.Products.FirstOrDefault(p => p.Name.ToLower() == NewProductName.ToLower());
                    if (targetProduct == null)
                    {
                        targetProduct = new Product
                        {
                            Name = NewProductName,
                            Category = Category,
                            Quantity = 0, // Initial
                            CostPrice = CostPrice,
                            SellingPrice = SellingPrice
                        };
                        context.Products.Add(targetProduct);
                        context.SaveChanges(); // Get ID
                    }
                }
                else
                {
                    if (SelectedProduct == null) return;
                    targetProduct = context.Products.Find(SelectedProduct.Id);
                }

                if (targetProduct == null) return;

                // 1. Create Purchase record
                var purchase = new Purchase
                {
                    ProductId = targetProduct.Id,
                    ProductName = targetProduct.Name,
                    SupplierName = SupplierName,
                    CostPrice = CostPrice,
                    Quantity = Quantity,
                    TotalCost = CostPrice * Quantity,
                    PurchaseDate = System.DateTime.Now
                };

                // 2. Update Product
                targetProduct.Quantity += Quantity;
                targetProduct.CostPrice = CostPrice; // Update to latest cost
                if (SellingPrice > 0) targetProduct.SellingPrice = SellingPrice;

                context.Purchases.Add(purchase);
                context.SaveChanges();
            }

            // Reset Form
            SupplierName = string.Empty;
            CostPrice = 0;
            SellingPrice = 0;
            Quantity = 1;
            NewProductName = string.Empty;
            SelectedProduct = null;

            RefreshData();
            
            // Notify other ViewModels to refresh (POS, Products, Records)
            WeakReferenceMessenger.Default.Send(DataChangedMessage.ProductUpdated);
            WeakReferenceMessenger.Default.Send(DataChangedMessage.TransactionUpdated);
        }
    }
}
