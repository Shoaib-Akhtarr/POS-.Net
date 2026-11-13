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
    public partial class CartLineItem : ObservableObject
    {
        public int ProductId { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Unit { get; set; } = "pcs";
        public string PackUnitName { get; set; } = string.Empty;
        public decimal UnitsPerPack { get; set; } = 1m;
        public decimal CostPrice { get; set; }
        public decimal AvailableStock { get; set; }

        [ObservableProperty]
        private decimal _quantity = 1m;

        [ObservableProperty]
        private decimal _unitPrice;

        [ObservableProperty]
        private decimal _discount;

        [ObservableProperty]
        private string _serialNumber = string.Empty;

        [ObservableProperty]
        private string _warrantyPeriod = string.Empty;

        public decimal LineTotal => Math.Max(0m, (Quantity * UnitPrice) - Discount);
        public bool HasPackConversion => UnitsPerPack > 1m && !string.IsNullOrWhiteSpace(PackUnitName);
        public bool IsBelowCost => CostPrice > 0m && UnitPrice < CostPrice;

        public event Action? LineChanged;

        partial void OnQuantityChanged(decimal value)
        {
            if (value < 0m) _quantity = 0m;
            OnPropertyChanged(nameof(LineTotal));
            LineChanged?.Invoke();
        }

        partial void OnUnitPriceChanged(decimal value)
        {
            if (value < 0m) _unitPrice = 0m;
            OnPropertyChanged(nameof(LineTotal));
            OnPropertyChanged(nameof(IsBelowCost));
            LineChanged?.Invoke();
        }

        partial void OnDiscountChanged(decimal value)
        {
            if (value < 0m) _discount = 0m;
            OnPropertyChanged(nameof(LineTotal));
            LineChanged?.Invoke();
        }
    }

    public class HeldCart
    {
        public string Id { get; set; } = Guid.NewGuid().ToString("N")[..6].ToUpperInvariant();
        public DateTime HeldAt { get; set; } = DateTime.Now;
        public Customer? Customer { get; set; }
        public string Notes { get; set; } = string.Empty;
        public decimal DiscountAmount { get; set; }
        public List<CartLineItem> Items { get; set; } = new();
        public string SummaryLabel => $"{Customer?.Name ?? "Walk-in"} ({Items.Count} items)";
        public decimal TotalEstimate => Math.Max(0m, Items.Sum(i => i.LineTotal) - DiscountAmount);
    }

    public partial class POSViewModel : ObservableObject, IRecipient<DataChangedMessage>
    {
        private static readonly string[] DefaultDepartments =
        {
            "All", "Electric", "Sanitary", "Hardware", "Tiles", "Paint", "Solar System", "Electronics"
        };

        [ObservableProperty] private ObservableCollection<Product> _products = new();
        [ObservableProperty] private ObservableCollection<Product> _filteredProducts = new();
        [ObservableProperty] private ObservableCollection<string> _categories = new();
        [ObservableProperty] private string _productSearchText = string.Empty;
        [ObservableProperty] private string _selectedCategory = "All";
        [ObservableProperty] private bool _isCompactListView;

        [ObservableProperty] private ObservableCollection<CartLineItem> _cartItems = new();
        [ObservableProperty] private decimal _subtotalAmount;
        [ObservableProperty] private decimal _totalAmount;
        [ObservableProperty] private string _customerName = "Walk-in Customer";
        [ObservableProperty] private string _customerSearchText = string.Empty;
        [ObservableProperty] private ObservableCollection<Customer> _filteredCustomers = new();
        private ObservableCollection<Customer> _allCustomers = new();

        [ObservableProperty] private Customer? _selectedCustomer;
        [ObservableProperty] private bool _isCustomerDropdownOpen;

        [ObservableProperty] private string _paymentMethod = "Cash";
        public List<string> AvailablePaymentMethods { get; } = new() { "Cash", "Credit", "Bank", "Raast", "JazzCash", "EasyPaisa" };

        [ObservableProperty] private decimal _discountAmount;
        [ObservableProperty] private decimal _amountPaid;
        [ObservableProperty] private decimal _cashTendered;
        [ObservableProperty] private decimal _changeReturned;
        [ObservableProperty] private decimal _totalBillWithOld;
        [ObservableProperty] private decimal _remainingKhata;
        [ObservableProperty] private decimal _nextBalance;
        [ObservableProperty] private string _creditLimitWarning = string.Empty;
        [ObservableProperty] private string _saleNotes = string.Empty;
        [ObservableProperty] private bool _isCustomAmountPaid;

        // FBR Fiscal Status & Tax Preview
        [ObservableProperty] private bool _isFbrTaxEnabled;
        [ObservableProperty] private string _fbrEnvironment = "Sandbox";
        [ObservableProperty] private decimal _fbrTaxRate = 18.0m;
        [ObservableProperty] private decimal _fbrTaxAmount;
        [ObservableProperty] private decimal _totalExclTax;
        [ObservableProperty] private string _buyerCnic = string.Empty;
        [ObservableProperty] private string _buyerNtn = string.Empty;

        public bool IsFbrLive => IsFbrTaxEnabled && string.Equals(FbrEnvironment, "Live", StringComparison.OrdinalIgnoreCase);
        public bool IsFbrSandbox => IsFbrTaxEnabled && !IsFbrLive;
        public string FbrBadgeLabel => IsFbrLive ? $"● FBR Live ({FbrTaxRate:0.#}%)" : $"● FBR Sandbox ({FbrTaxRate:0.#}%)";

        [ObservableProperty] private string _statusMessage = string.Empty;
        [ObservableProperty] private bool _isErrorStatus;

        // Held Carts
        [ObservableProperty] private ObservableCollection<HeldCart> _heldCarts = new();
        [ObservableProperty] private bool _isHeldCartsModalOpen;

        // Item Details / Serial / Warranty / Price Edit Modal
        [ObservableProperty] private bool _isItemEditModalOpen;
        [ObservableProperty] private CartLineItem? _editingCartItem;
        [ObservableProperty] private decimal _editItemQty;
        [ObservableProperty] private decimal _editItemUnitPrice;
        [ObservableProperty] private decimal _editItemDiscount;
        [ObservableProperty] private string _editItemSerial = string.Empty;
        [ObservableProperty] private string _editItemWarranty = string.Empty;
        [ObservableProperty] private string _ownerOverridePin = string.Empty;
        [ObservableProperty] private string _itemEditError = string.Empty;

        // Pack / Sq.Ft. Calculator Modal
        [ObservableProperty] private bool _isPackCalcModalOpen;
        [ObservableProperty] private CartLineItem? _packCalcItem;
        [ObservableProperty] private decimal _packCalcPacks = 1m;
        [ObservableProperty] private decimal _packCalcLoose = 0m;
        [ObservableProperty] private decimal _packCalcTotalUnits;

        private readonly PrintService _printService;
        private readonly BackupService _backupService;
        private readonly FbrTaxService _fbrTaxService;

        public bool IsOwner => SessionService.IsOwner;
        public bool HasHeldCarts => HeldCarts.Count > 0;
        public int HeldCartCount => HeldCarts.Count;

        public POSViewModel()
        {
            _printService = new PrintService();
            _backupService = new BackupService();
            _fbrTaxService = new FbrTaxService();

            LoadProductsFromDb();
            LoadCustomersFromDb();
            LoadFbrSettings();
            WeakReferenceMessenger.Default.Register(this);

            Task.Run(async () =>
            {
                await _backupService.BackupProductsAsync(Products);
                await _backupService.BackupCustomersAsync(_allCustomers);
            });
        }

        partial void OnProductSearchTextChanged(string value) => ApplyProductFilter();
        partial void OnSelectedCategoryChanged(string value) => ApplyProductFilter();
        partial void OnDiscountAmountChanged(decimal value)
        {
            if (value < 0m) _discountAmount = 0m;
            UpdateTotal();
        }

        partial void OnAmountPaidChanged(decimal value)
        {
            if (value < 0m) _amountPaid = 0m;
            UpdateBalancePreview();
        }

        partial void OnCashTenderedChanged(decimal value) => UpdateChangePreview();

        partial void OnCustomerSearchTextChanged(string value)
        {
            ApplyCustomerFilter();
            IsCustomerDropdownOpen = SelectedCustomer == null &&
                                     !string.IsNullOrWhiteSpace(value) &&
                                     FilteredCustomers.Count > 0;
        }

        partial void OnPaymentMethodChanged(string value)
        {
            if (!IsCustomAmountPaid)
            {
                AmountPaid = string.Equals(value, "Credit", StringComparison.OrdinalIgnoreCase) ? 0m : TotalAmount;
            }
            UpdateBalancePreview();
        }

        partial void OnSelectedCustomerChanged(Customer? value)
        {
            if (value != null)
            {
                CustomerName = value.Name;
                BuyerCnic = value.Cnic;
                BuyerNtn = value.Ntn;
                if (PaymentMethod == "Cash" && AmountPaid == 0m) PaymentMethod = "Credit";
                IsCustomerDropdownOpen = false;
            }
            else
            {
                CustomerName = "Walk-in Customer";
                BuyerCnic = string.Empty;
                BuyerNtn = string.Empty;
                if (PaymentMethod == "Credit") PaymentMethod = "Cash";
            }
            UpdateBalancePreview();
        }

        partial void OnPackCalcPacksChanged(decimal value) => RecalculatePackUnits();
        partial void OnPackCalcLooseChanged(decimal value) => RecalculatePackUnits();

        public void Receive(DataChangedMessage message)
        {
            if (message.Value == "Product")
            {
                App.Current?.Dispatcher.Invoke(LoadProductsFromDb);
            }
            else if (message.Value == "Customer")
            {
                App.Current?.Dispatcher.Invoke(LoadCustomersFromDb);
            }
            else if (message.Value == "Settings")
            {
                App.Current?.Dispatcher.Invoke(LoadFbrSettings);
            }
            else if (message.Value == "Session")
            {
                App.Current?.Dispatcher.Invoke(() => OnPropertyChanged(nameof(IsOwner)));
            }
        }

        private void LoadCustomersFromDb()
        {
            using var context = new AppDbContext();
            var customers = context.Customers.OrderBy(c => c.Name).ToList();
            _allCustomers = new ObservableCollection<Customer>(customers);
            ApplyCustomerFilter();
        }

        private void LoadProductsFromDb()
        {
            using var context = new AppDbContext();
            var dbProducts = context.Products.Where(p => !p.IsDeleted).OrderBy(p => p.Name).ToList();
            Products = new ObservableCollection<Product>(dbProducts);

            var cats = new List<string>(DefaultDepartments);
            foreach (var cat in dbProducts.Select(p => p.Category).Where(c => !string.IsNullOrWhiteSpace(c)).Distinct(StringComparer.OrdinalIgnoreCase))
            {
                if (!cats.Any(c => string.Equals(c, cat, StringComparison.OrdinalIgnoreCase)))
                {
                    cats.Add(cat.Trim());
                }
            }
            Categories = new ObservableCollection<string>(cats);
            ApplyProductFilter();
        }

        private void ApplyProductFilter()
        {
            var query = Products.AsEnumerable();

            if (!string.Equals(SelectedCategory, "All", StringComparison.OrdinalIgnoreCase))
            {
                query = query.Where(p => string.Equals(p.Category, SelectedCategory, StringComparison.OrdinalIgnoreCase));
            }

            if (!string.IsNullOrWhiteSpace(ProductSearchText))
            {
                var search = ProductSearchText.Trim().ToLowerInvariant();
                query = query.Where(p =>
                    p.Name.ToLowerInvariant().Contains(search) ||
                    (!string.IsNullOrEmpty(p.Barcode) && p.Barcode.ToLowerInvariant().Contains(search)) ||
                    (!string.IsNullOrEmpty(p.Brand) && p.Brand.ToLowerInvariant().Contains(search)) ||
                    p.Category.ToLowerInvariant().Contains(search));
            }

            FilteredProducts = new ObservableCollection<Product>(query);
        }

        private void ApplyCustomerFilter()
        {
            var items = string.IsNullOrWhiteSpace(CustomerSearchText)
                ? _allCustomers.ToList()
                : _allCustomers.Where(c =>
                    (!string.IsNullOrEmpty(c.Name) && c.Name.ToLowerInvariant().Contains(CustomerSearchText.Trim().ToLowerInvariant())) ||
                    (!string.IsNullOrEmpty(c.Phone) && c.Phone.Contains(CustomerSearchText.Trim()))).ToList();

            FilteredCustomers.Clear();
            foreach (var c in items) FilteredCustomers.Add(c);
        }

        [RelayCommand]
        private void SelectCustomer(Customer? customer)
        {
            if (customer == null) return;
            SelectedCustomer = customer;
            CustomerSearchText = string.Empty;
            IsCustomerDropdownOpen = false;
        }

        [RelayCommand]
        private void ChangeCategory(string category) => SelectedCategory = category;

        [RelayCommand]
        private void ToggleCatalogView() => IsCompactListView = !IsCompactListView;

        [RelayCommand]
        private void ClearCustomer()
        {
            SelectedCustomer = null;
            CustomerSearchText = string.Empty;
            IsCustomerDropdownOpen = false;
        }

        [RelayCommand]
        private void QuickAddFromSearch()
        {
            if (string.IsNullOrWhiteSpace(ProductSearchText)) return;

            var trimmed = ProductSearchText.Trim();
            var exactBarcode = Products.FirstOrDefault(p =>
                !string.IsNullOrEmpty(p.Barcode) &&
                string.Equals(p.Barcode.Trim(), trimmed, StringComparison.OrdinalIgnoreCase));

            if (exactBarcode != null)
            {
                AddToCart(exactBarcode);
                ProductSearchText = string.Empty;
                return;
            }

            if (FilteredProducts.Count == 1)
            {
                AddToCart(FilteredProducts[0]);
                ProductSearchText = string.Empty;
            }
        }

        [RelayCommand]
        private void AddToCart(Product? product)
        {
            if (product == null) return;
            ClearStatus();

            var existing = CartItems.FirstOrDefault(c => c.ProductId == product.Id);
            if (existing != null)
            {
                if (existing.Quantity + 1m > product.Quantity)
                {
                    SetStatus($"Notice: {product.Name} only has {FormatQty(product.Quantity)} {product.Unit} in stock.", isError: true);
                }
                existing.Quantity += 1m;
                UpdateTotal();
                return;
            }

            if (product.Quantity <= 0m)
            {
                SetStatus($"Notice: {product.Name} is currently out of stock (0 {product.Unit}).", isError: true);
            }

            var line = new CartLineItem
            {
                ProductId = product.Id,
                Name = product.Name,
                Unit = string.IsNullOrWhiteSpace(product.Unit) ? "pcs" : product.Unit,
                PackUnitName = product.PackUnitName,
                UnitsPerPack = product.UnitsPerPack > 0m ? product.UnitsPerPack : 1m,
                CostPrice = product.CostPrice,
                AvailableStock = product.Quantity,
                Quantity = 1m,
                UnitPrice = product.SellingPrice,
                WarrantyPeriod = product.DefaultWarranty
            };
            AttachLineHandlers(line);
            CartItems.Add(line);
            UpdateTotal();
        }

        private void AttachLineHandlers(CartLineItem line)
        {
            line.LineChanged += UpdateTotal;
        }

        [RelayCommand]
        private void IncrementCartItem(CartLineItem? item)
        {
            if (item == null) return;
            item.Quantity += 1m;
            if (item.Quantity > item.AvailableStock)
            {
                SetStatus($"Notice: {item.Name} exceeds available stock ({FormatQty(item.AvailableStock)} {item.Unit}).", isError: true);
            }
            UpdateTotal();
        }

        [RelayCommand]
        private void DecrementCartItem(CartLineItem? item)
        {
            if (item == null) return;
            if (item.Quantity > 1m)
            {
                item.Quantity -= 1m;
            }
            else
            {
                RemoveFromCart(item);
            }
            UpdateTotal();
        }

        [RelayCommand]
        private void RemoveFromCart(CartLineItem? item)
        {
            if (item == null) return;
            item.LineChanged -= UpdateTotal;
            CartItems.Remove(item);
            UpdateTotal();
        }

        [RelayCommand]
        private void ClearCart()
        {
            foreach (var item in CartItems) item.LineChanged -= UpdateTotal;
            CartItems.Clear();
            DiscountAmount = 0m;
            AmountPaid = 0m;
            CashTendered = 0m;
            ChangeReturned = 0m;
            IsCustomAmountPaid = false;
            SaleNotes = string.Empty;
            SelectedCustomer = null;
            CustomerSearchText = string.Empty;
            ClearStatus();
            UpdateTotal();
        }

        [RelayCommand]
        private void MarkCustomPayment() => IsCustomAmountPaid = true;

        [RelayCommand]
        private void SetFullPayment()
        {
            IsCustomAmountPaid = false;
            AmountPaid = SelectedCustomer != null ? Math.Max(0m, TotalBillWithOld) : TotalAmount;
            CashTendered = AmountPaid;
            UpdateBalancePreview();
        }

        [RelayCommand]
        private void SetZeroPayment()
        {
            IsCustomAmountPaid = true;
            AmountPaid = 0m;
            CashTendered = 0m;
            UpdateBalancePreview();
        }

        // Item Detail / Serial / Warranty / Negotiated Price Modal
        [RelayCommand]
        private void OpenItemEditModal(CartLineItem? item)
        {
            if (item == null) return;
            EditingCartItem = item;
            EditItemQty = item.Quantity;
            EditItemUnitPrice = item.UnitPrice;
            EditItemDiscount = item.Discount;
            EditItemSerial = item.SerialNumber;
            EditItemWarranty = item.WarrantyPeriod;
            OwnerOverridePin = string.Empty;
            ItemEditError = string.Empty;
            IsItemEditModalOpen = true;
        }

        [RelayCommand]
        private void SaveItemEditModal()
        {
            if (EditingCartItem == null) return;

            if (EditItemQty <= 0m)
            {
                ItemEditError = "Quantity must be greater than zero.";
                return;
            }

            if (EditItemUnitPrice < 0m || EditItemDiscount < 0m)
            {
                ItemEditError = "Price and discount cannot be negative.";
                return;
            }

            if (!SessionService.IsOwner && EditingCartItem.CostPrice > 0m && EditItemUnitPrice < EditingCartItem.CostPrice)
            {
                if (!SessionService.VerifyAnyOwnerPin(OwnerOverridePin))
                {
                    ItemEditError = "Price is below minimum cost floor. Enter an Owner PIN to override.";
                    return;
                }
            }

            EditingCartItem.Quantity = EditItemQty;
            EditingCartItem.UnitPrice = EditItemUnitPrice;
            EditingCartItem.Discount = EditItemDiscount;
            EditingCartItem.SerialNumber = EditItemSerial.Trim();
            EditingCartItem.WarrantyPeriod = EditItemWarranty.Trim();

            IsItemEditModalOpen = false;
            EditingCartItem = null;
            UpdateTotal();
        }

        [RelayCommand]
        private void CloseItemEditModal()
        {
            IsItemEditModalOpen = false;
            EditingCartItem = null;
        }

        // Pack / Box / Sq.Ft. Calculator Modal
        [RelayCommand]
        private void OpenPackCalcModal(CartLineItem? item)
        {
            if (item == null) return;
            PackCalcItem = item;
            decimal perPack = item.UnitsPerPack > 0m ? item.UnitsPerPack : 1m;
            PackCalcPacks = Math.Floor(item.Quantity / perPack);
            PackCalcLoose = item.Quantity % perPack;
            RecalculatePackUnits();
            IsPackCalcModalOpen = true;
        }

        private void RecalculatePackUnits()
        {
            if (PackCalcItem == null) return;
            decimal perPack = PackCalcItem.UnitsPerPack > 0m ? PackCalcItem.UnitsPerPack : 1m;
            PackCalcTotalUnits = Math.Max(0m, (PackCalcPacks * perPack) + PackCalcLoose);
        }

        [RelayCommand]
        private void ApplyPackCalculation()
        {
            if (PackCalcItem != null && PackCalcTotalUnits > 0m)
            {
                PackCalcItem.Quantity = PackCalcTotalUnits;
                UpdateTotal();
            }
            IsPackCalcModalOpen = false;
            PackCalcItem = null;
        }

        [RelayCommand]
        private void ClosePackCalcModal()
        {
            IsPackCalcModalOpen = false;
            PackCalcItem = null;
        }

        // Hold / Resume Carts
        [RelayCommand]
        private void HoldCurrentCart()
        {
            if (!CartItems.Any()) return;
            HeldCarts.Add(new HeldCart
            {
                Customer = SelectedCustomer,
                Notes = SaleNotes,
                DiscountAmount = DiscountAmount,
                Items = CartItems.Select(i => new CartLineItem
                {
                    ProductId = i.ProductId,
                    Name = i.Name,
                    Unit = i.Unit,
                    PackUnitName = i.PackUnitName,
                    UnitsPerPack = i.UnitsPerPack,
                    CostPrice = i.CostPrice,
                    AvailableStock = i.AvailableStock,
                    Quantity = i.Quantity,
                    UnitPrice = i.UnitPrice,
                    Discount = i.Discount,
                    SerialNumber = i.SerialNumber,
                    WarrantyPeriod = i.WarrantyPeriod
                }).ToList()
            });
            NotifyHeldCartCounts();
            ClearCart();
            SetStatus("Bill placed on hold.", isError: false);
        }

        [RelayCommand]
        private void OpenHeldCartsModal() => IsHeldCartsModalOpen = true;

        [RelayCommand]
        private void CloseHeldCartsModal() => IsHeldCartsModalOpen = false;

        [RelayCommand]
        private void ResumeHeldCart(HeldCart? held)
        {
            if (held == null) return;
            ClearCart();
            SelectedCustomer = held.Customer;
            CustomerSearchText = held.Customer?.Name ?? string.Empty;
            SaleNotes = held.Notes;
            DiscountAmount = held.DiscountAmount;

            foreach (var item in held.Items)
            {
                AttachLineHandlers(item);
                CartItems.Add(item);
            }

            HeldCarts.Remove(held);
            NotifyHeldCartCounts();
            IsHeldCartsModalOpen = false;
            UpdateTotal();
        }

        [RelayCommand]
        private void DeleteHeldCart(HeldCart? held)
        {
            if (held == null) return;
            HeldCarts.Remove(held);
            NotifyHeldCartCounts();
        }

        private void NotifyHeldCartCounts()
        {
            OnPropertyChanged(nameof(HasHeldCarts));
            OnPropertyChanged(nameof(HeldCartCount));
        }

        private void LoadFbrSettings()
        {
            var s = ThemeService.GetSettings();
            IsFbrTaxEnabled = s.IsFbrTaxEnabled;
            FbrEnvironment = s.FbrEnvironment;
            FbrTaxRate = s.FbrDefaultTaxRate > 0 ? s.FbrDefaultTaxRate : 18.0m;
            OnPropertyChanged(nameof(IsFbrLive));
            OnPropertyChanged(nameof(IsFbrSandbox));
            OnPropertyChanged(nameof(FbrBadgeLabel));
            UpdateTotal();
        }

        private void UpdateTotal()
        {
            SubtotalAmount = CartItems.Sum(c => c.LineTotal);
            TotalAmount = Math.Max(0m, SubtotalAmount - DiscountAmount);

            if (IsFbrTaxEnabled)
            {
                FbrTaxAmount = Math.Round(TotalAmount * (FbrTaxRate / (100m + FbrTaxRate)), 2);
                TotalExclTax = Math.Max(0m, TotalAmount - FbrTaxAmount);
            }
            else
            {
                FbrTaxAmount = 0m;
                TotalExclTax = TotalAmount;
            }

            if (!IsCustomAmountPaid)
            {
                AmountPaid = SelectedCustomer == null || PaymentMethod != "Credit" ? TotalAmount : 0m;
            }
            UpdateBalancePreview();
        }

        private void UpdateBalancePreview()
        {
            decimal prevDues = SelectedCustomer?.TotalDues ?? 0m;
            TotalBillWithOld = prevDues + TotalAmount;
            RemainingKhata = TotalBillWithOld - AmountPaid;
            NextBalance = RemainingKhata;
            CreditLimitWarning = SelectedCustomer is { CreditLimit: > 0m } customer && NextBalance > customer.CreditLimit
                ? $"Credit limit exceeded by PKR {NextBalance - customer.CreditLimit:N2}. Review before checkout."
                : string.Empty;
            UpdateChangePreview();
        }

        private void UpdateChangePreview()
        {
            decimal tendered = CashTendered > 0m ? CashTendered : AmountPaid;
            ChangeReturned = Math.Max(0m, tendered - AmountPaid);
        }

        [RelayCommand]
        private Task CompleteSale() => ProcessTransactionAsync("Sale", printA4: false);

        [RelayCommand]
        private Task CompleteSaleA4() => ProcessTransactionAsync("Sale", printA4: true);

        [RelayCommand]
        private Task SaveAsEstimate() => ProcessTransactionAsync("Estimate", printA4: false);

        private async Task ProcessTransactionAsync(string recordType, bool printA4)
        {
            if (!CartItems.Any())
            {
                SetStatus("Add at least one item to the cart first.", isError: true);
                return;
            }

            if (CartItems.Any(c => c.Quantity <= 0m))
            {
                SetStatus("Every cart item must have a quantity greater than zero.", isError: true);
                return;
            }

            bool isEstimate = string.Equals(recordType, "Estimate", StringComparison.OrdinalIgnoreCase);

            if (!isEstimate && SelectedCustomer == null &&
                (string.Equals(PaymentMethod, "Credit", StringComparison.OrdinalIgnoreCase) || AmountPaid < TotalAmount))
            {
                SetStatus("Walk-in customers must pay the full bill (select a customer account for Khata credit).", isError: true);
                return;
            }

            if (!SessionService.IsOwner && CartItems.Any(c => c.IsBelowCost))
            {
                SetStatus("One or more items are priced below cost. Owner approval required.", isError: true);
                return;
            }

            using var context = new AppDbContext();
            using var transaction = await context.Database.BeginTransactionAsync();
            try
            {
                if (!isEstimate)
                {
                    foreach (var item in CartItems)
                    {
                        var prod = await context.Products.FindAsync(item.ProductId);
                        if (prod == null || prod.IsDeleted)
                        {
                            SetStatus($"Product '{item.Name}' no longer exists.", isError: true);
                            return;
                        }
                        if (prod.Quantity < item.Quantity)
                        {
                            SetStatus($"Insufficient stock for '{item.Name}' (Available: {FormatQty(prod.Quantity)} {prod.Unit}).", isError: true);
                            return;
                        }
                    }
                }

                decimal finalAmountPaid = AmountPaid;
                decimal effectiveCashTendered = PaymentMethod == "Cash"
                    ? (CashTendered > 0m ? CashTendered : AmountPaid)
                    : AmountPaid;
                decimal finalBalanceDue = NextBalance;

                // Prevent Walk-in overpayments from creating negative Khata balances
                if (!isEstimate && SelectedCustomer == null && AmountPaid > TotalAmount)
                {
                    effectiveCashTendered = Math.Max(effectiveCashTendered, AmountPaid);
                    finalAmountPaid = TotalAmount;
                    finalBalanceDue = 0m;
                }

                decimal change = PaymentMethod == "Cash" ? Math.Max(0m, effectiveCashTendered - finalAmountPaid) : 0m;
                decimal prevDues = SelectedCustomer?.TotalDues ?? 0m;

                string prefix = isEstimate ? "EST" : "INV";
                var sale = new Sale
                {
                    RecordType = isEstimate ? "Estimate" : "Sale",
                    TotalAmount = TotalAmount,
                    Discount = DiscountAmount,
                    AmountPaid = isEstimate ? 0m : finalAmountPaid,
                    CustomerId = SelectedCustomer?.Id,
                    CustomerName = CustomerName,
                    PreviousDues = prevDues,
                    BalanceDue = isEstimate ? prevDues : finalBalanceDue,
                    CashTendered = isEstimate ? 0m : effectiveCashTendered,
                    ChangeReturned = isEstimate ? 0m : change,
                    PaymentMethod = isEstimate ? "Estimate" : PaymentMethod,
                    IsPaid = !isEstimate && finalBalanceDue <= 0m,
                    Notes = SaleNotes.Trim(),
                    CreatedBy = SessionService.CurrentUserName,
                    ReceiptNumber = $"{prefix}-{DateTime.Now:yyyyMMddHHmmss}"
                };

                foreach (var item in CartItems)
                {
                    var product = await context.Products.FindAsync(item.ProductId);
                    decimal unitCost = item.CostPrice;
                    if (product != null)
                    {
                        unitCost = product.CostPrice;
                        if (!isEstimate)
                        {
                            product.Quantity -= item.Quantity;
                            product.SalesCount += (int)Math.Ceiling(item.Quantity);
                            product.UpdatedAt = DateTime.Now;
                        }
                    }

                    sale.SaleItems.Add(new SaleItem
                    {
                        ProductId = item.ProductId,
                        Name = item.Name,
                        Quantity = item.Quantity,
                        UnitPrice = item.UnitPrice,
                        UnitCost = unitCost,
                        Discount = item.Discount,
                        Price = item.LineTotal,
                        Unit = item.Unit,
                        SerialNumber = string.IsNullOrWhiteSpace(item.SerialNumber) ? null : item.SerialNumber,
                        WarrantyPeriod = string.IsNullOrWhiteSpace(item.WarrantyPeriod) ? null : item.WarrantyPeriod
                    });
                }

                if (!isEstimate && SelectedCustomer != null)
                {
                    var customer = await context.Customers.FindAsync(SelectedCustomer.Id);
                    if (customer != null)
                    {
                        customer.TotalDues = NextBalance;
                        customer.TotalDiscount += DiscountAmount;
                        customer.UpdatedAt = DateTime.Now;
                    }
                }

                // Process FBR Fiscal Tax Integration if enabled
                var storeSettings = ThemeService.GetSettings();
                if (!isEstimate && storeSettings.IsFbrTaxEnabled)
                {
                    sale.BuyerCnic = !string.IsNullOrWhiteSpace(BuyerCnic) ? BuyerCnic.Trim() : SelectedCustomer?.Cnic;
                    sale.BuyerNtn = !string.IsNullOrWhiteSpace(BuyerNtn) ? BuyerNtn.Trim() : SelectedCustomer?.Ntn;
                    await _fbrTaxService.ProcessSaleFiscalizationAsync(sale, storeSettings);
                }

                context.Sales.Add(sale);
                await context.SaveChangesAsync();
                if (!isEstimate && SelectedCustomer != null)
                {
                    decimal ledgerDelta = finalBalanceDue - prevDues;
                    if (ledgerDelta != 0m)
                    {
                        var customer = await context.Customers.FindAsync(SelectedCustomer.Id);
                        if (customer != null)
                        {
                            var account = KhataService.GetCustomerAccount(context, customer);
                            KhataService.Post(context, account, $"Sale · {sale.ReceiptNumber}", PaymentMethod,
                                Math.Max(0m, ledgerDelta), Math.Max(0m, -ledgerDelta), "Sale", sale.Id.ToString(), sale.CreatedAt);
                        }
                    }
                }
                await context.SaveChangesAsync();
                await transaction.CommitAsync();

                if (printA4)
                {
                    _printService.PrintA4Invoice(sale);
                }
                else
                {
                    _printService.PrintReceipt(sale);
                }

                await _backupService.BackupSaleAsync(sale);
                if (!isEstimate)
                {
                    await _backupService.BackupProductsAsync(context.Products.Where(p => !p.IsDeleted).ToList());
                    if (SelectedCustomer != null)
                    {
                        await _backupService.BackupCustomersAsync(context.Customers.ToList());
                    }
                }

                ClearCart();
                LoadProductsFromDb();
                LoadCustomersFromDb();

                WeakReferenceMessenger.Default.Send(DataChangedMessage.ProductUpdated);
                WeakReferenceMessenger.Default.Send(DataChangedMessage.TransactionUpdated);
                if (sale.CustomerId != null)
                {
                    WeakReferenceMessenger.Default.Send(DataChangedMessage.CustomerUpdated);
                }

                string fbrNote = !isEstimate && !string.IsNullOrWhiteSpace(sale.FbrInvoiceNumber)
                    ? $" · FBR: {sale.FbrInvoiceNumber} ({sale.FbrStatus})"
                    : string.Empty;
                SetStatus(isEstimate ? $"Estimate #{sale.ReceiptNumber} saved." : $"Sale #{sale.ReceiptNumber} completed{fbrNote}.", isError: false);
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                SetStatus($"Checkout failed: {ex.Message}", isError: true);
            }
        }

        private void SetStatus(string message, bool isError)
        {
            StatusMessage = message;
            IsErrorStatus = isError;
        }

        private void ClearStatus()
        {
            StatusMessage = string.Empty;
            IsErrorStatus = false;
        }

        private static string FormatQty(decimal qty) =>
            qty % 1m == 0 ? qty.ToString("N0") : qty.ToString("0.##");
    }
}
