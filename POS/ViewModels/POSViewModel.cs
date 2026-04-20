using System;
using System.Collections.ObjectModel;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Messaging;
using POS.Models;
using POS.Services;

namespace POS.ViewModels
{
    public partial class POSViewModel : ObservableObject, IRecipient<DataChangedMessage>
    {
        [ObservableProperty]
        private ObservableCollection<Product> _products;

        [ObservableProperty]
        private ObservableCollection<Product> _filteredProducts;

        [ObservableProperty]
        private string _productSearchText = string.Empty;

        [ObservableProperty]
        private string _selectedCategory = "All";

        [ObservableProperty]
        private ObservableCollection<SaleItem> _cartItems;

        [ObservableProperty]
        private decimal _totalAmount;

        [ObservableProperty]
        private string _customerName = "Walk-in Customer";

        [ObservableProperty]
        private string _customerSearchText = string.Empty;

        [ObservableProperty]
        private ObservableCollection<Customer> _filteredCustomers;

        private ObservableCollection<Customer> _allCustomers;

        [ObservableProperty]
        private Customer? _selectedCustomer;

        [ObservableProperty]
        private string _paymentMethod = "Cash";

        [ObservableProperty]
        private decimal _discountAmount;

        [ObservableProperty]
        private decimal _amountPaid;

        [ObservableProperty]
        private decimal _totalBillWithOld;

        [ObservableProperty]
        private decimal _remainingKhata;

        [ObservableProperty]
        private decimal _nextBalance;

        private readonly PrintService _printService;
        private readonly BackupService _backupService;

        partial void OnProductSearchTextChanged(string value) => ApplyProductFilter();
        partial void OnSelectedCategoryChanged(string value) => ApplyProductFilter();

        partial void OnDiscountAmountChanged(decimal value) => UpdateTotal();
        partial void OnAmountPaidChanged(decimal value) => UpdateBalancePreview();
        
        [ObservableProperty]
        private bool _isCustomerDropdownOpen;

        partial void OnCustomerSearchTextChanged(string value)
        {
            ApplyCustomerFilter();
            IsCustomerDropdownOpen = !string.IsNullOrWhiteSpace(value);
        }

        partial void OnSelectedCustomerChanged(Customer? value)
        {
            if (value != null)
            {
                CustomerName = value.Name;
                PaymentMethod = "Credit";
                UpdateBalancePreview();
            }
            else
            {
                CustomerName = "Walk-in Customer";
                PaymentMethod = "Cash";
                NextBalance = 0;
                UpdateBalancePreview();
            }
        }

        private void ApplyProductFilter()
        {
            var query = Products.AsEnumerable();

            if (SelectedCategory != "All")
            {
                query = query.Where(p => p.Category == SelectedCategory);
            }

            if (!string.IsNullOrWhiteSpace(ProductSearchText))
            {
                var search = ProductSearchText.ToLower();
                query = query.Where(p => p.Name.ToLower().Contains(search));
            }

            FilteredProducts = new ObservableCollection<Product>(query);
        }

        private void ApplyCustomerFilter()
        {
            if (string.IsNullOrWhiteSpace(CustomerSearchText))
            {
                FilteredCustomers = new ObservableCollection<Customer>(_allCustomers);
            }
            else
            {
                var query = CustomerSearchText.ToLower();
                var filtered = _allCustomers.Where(c => 
                    (c.Name != null && c.Name.ToLower().Contains(query)) || 
                    (c.Phone != null && c.Phone.Contains(query))).ToList();
                
                FilteredCustomers = new ObservableCollection<Customer>(filtered);
            }
        }

        public POSViewModel()
        {
            Products = new ObservableCollection<Product>();
            FilteredProducts = new ObservableCollection<Product>();
            CartItems = new ObservableCollection<SaleItem>();
            _allCustomers = new ObservableCollection<Customer>();
            FilteredCustomers = new ObservableCollection<Customer>();
            LoadProductsFromDb();
            LoadCustomersFromDb();
            
            _printService = new PrintService();
            _backupService = new BackupService();
            WeakReferenceMessenger.Default.Register(this);
            
            // Initial backup on load
            Task.Run(async () => {
                await _backupService.BackupProductsAsync(Products);
                await _backupService.BackupCustomersAsync(_allCustomers);
            });
        }

        public void Receive(DataChangedMessage message)
        {
            if (message.Value == "Product")
            {
                App.Current.Dispatcher.Invoke(LoadProductsFromDb);
            }
            else if (message.Value == "Customer")
            {
                App.Current.Dispatcher.Invoke(LoadCustomersFromDb);
            }
        }

        private void LoadCustomersFromDb()
        {
            using (var context = new AppDbContext())
            {
                var customers = context.Customers.ToList();
                _allCustomers = new ObservableCollection<Customer>(customers);
                FilteredCustomers = new ObservableCollection<Customer>(customers);
            }
        }

        private void LoadProductsFromDb()
        {
            using (var context = new AppDbContext())
            {
                var dbProducts = context.Products.Where(p => !p.IsDeleted).ToList();
                Products = new ObservableCollection<Product>(dbProducts);
                ApplyProductFilter();
            }
        }

        [CommunityToolkit.Mvvm.Input.RelayCommand]
        private void ChangeCategory(string category) => SelectedCategory = category;

        [CommunityToolkit.Mvvm.Input.RelayCommand]
        private void AddToCart(Product product)
        {
            if (product == null) return;
            var existing = CartItems.FirstOrDefault(c => c.ProductId == product.Id);
            if (existing != null)
            {
                int index = CartItems.IndexOf(existing);
                var newItem = new SaleItem 
                { 
                    SaleId = existing.SaleId,
                    ProductId = existing.ProductId,
                    Name = existing.Name,
                    Quantity = existing.Quantity + 1,
                    Price = (existing.Quantity + 1) * product.SellingPrice
                };
                CartItems[index] = newItem; 
            }
            else
            {
                CartItems.Add(new SaleItem 
                { 
                    ProductId = product.Id, 
                    Name = product.Name, 
                    Quantity = 1, 
                    Price = product.SellingPrice 
                });
            }
            UpdateTotal();
        }

        [CommunityToolkit.Mvvm.Input.RelayCommand]
        private void RemoveFromCart(SaleItem item)
        {
            CartItems.Remove(item);
            UpdateTotal();
        }

        private void UpdateTotal()
        {
            decimal subtotal = CartItems.Sum(c => c.Price);
            TotalAmount = Math.Max(0, subtotal - DiscountAmount);
            if (AmountPaid == 0) AmountPaid = TotalAmount; 
            UpdateBalancePreview();
        }

        private void UpdateBalancePreview()
        {
            decimal prevDues = SelectedCustomer?.TotalDues ?? 0;
            TotalBillWithOld = prevDues + TotalAmount;
            
            // Formula: RemainingKhata = (Old + New) - Paid
            RemainingKhata = TotalBillWithOld - AmountPaid;
            
            // NextBalance is what strictly goes back to DB
            NextBalance = TotalBillWithOld - AmountPaid;
        }

        [CommunityToolkit.Mvvm.Input.RelayCommand]
        private async System.Threading.Tasks.Task CompleteSale()
        {
            if (!CartItems.Any()) return;

            using (var context = new AppDbContext())
            {
                using var transaction = await context.Database.BeginTransactionAsync();
                try
                {
                    var sale = new Sale
                    {
                        TotalAmount = TotalAmount,
                        Discount = DiscountAmount,
                        AmountPaid = AmountPaid,
                        CustomerId = SelectedCustomer?.Id,
                        CustomerName = CustomerName,
                        PreviousDues = SelectedCustomer?.TotalDues ?? 0,
                        BalanceDue = NextBalance,
                        PaymentMethod = PaymentMethod,
                        IsPaid = (NextBalance == 0)
                    };

                    foreach (var item in CartItems)
                    {
                        var product = await context.Products.FindAsync(item.ProductId);
                        if (product != null)
                        {
                            product.Quantity -= item.Quantity;
                            product.SalesCount += item.Quantity;
                        }
                        sale.SaleItems.Add(new SaleItem
                        {
                            ProductId = item.ProductId,
                            Name = item.Name,
                            Quantity = item.Quantity,
                            Price = item.Price
                        });
                    }

                    if (SelectedCustomer != null)
                    {
                        var customer = await context.Customers.FindAsync(SelectedCustomer.Id);
                        if (customer != null)
                        {
                            customer.TotalDues = NextBalance;
                            customer.TotalDiscount += DiscountAmount;
                        }
                    }

                    context.Sales.Add(sale);
                    await context.SaveChangesAsync();
                    await transaction.CommitAsync();

                    // Print Receipt
                    _printService.PrintReceipt(sale);

                    // Backup to Text
                    await _backupService.BackupSaleAsync(sale);
                    await _backupService.BackupProductsAsync(context.Products.Where(p => !p.IsDeleted).ToList());
                    if (SelectedCustomer != null)
                    {
                        await _backupService.BackupCustomersAsync(context.Customers.ToList());
                    }

                    // Cleanup
                    CartItems.Clear();
                    TotalAmount = 0;
                    DiscountAmount = 0;
                    AmountPaid = 0;
                    SelectedCustomer = null;
                    CustomerSearchText = string.Empty;
                    
                    LoadProductsFromDb(); 
                    
                    // Notify other ViewModels (Products, Records)
                    WeakReferenceMessenger.Default.Send(DataChangedMessage.ProductUpdated);
                    WeakReferenceMessenger.Default.Send(DataChangedMessage.TransactionUpdated);
                }
                catch
                {
                    await transaction.RollbackAsync();
                }
            }
        }
    }
}
