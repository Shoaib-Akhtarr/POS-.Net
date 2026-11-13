using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using Microsoft.EntityFrameworkCore;
using POS.Models;
using POS.Services;

namespace POS.ViewModels
{
    public partial class CustomersViewModel : ObservableObject, IRecipient<DataChangedMessage>
    {
        [ObservableProperty] private ObservableCollection<Customer> _customers = new();
        [ObservableProperty] private ObservableCollection<Customer> _filteredCustomers = new();
        [ObservableProperty] private Customer? _selectedCustomer;
        [ObservableProperty] private ObservableCollection<Sale> _customerHistory = new();
        [ObservableProperty] private Sale? _selectedSaleHistory;
        [ObservableProperty] private bool _isReceiptModalOpen;
        [ObservableProperty] private string _searchText = string.Empty;

        // Modal States
        [ObservableProperty] private bool _isAddModalOpen;
        [ObservableProperty] private bool _isEditMode;
        [ObservableProperty] private bool _isPaymentModalOpen;
        [ObservableProperty] private string _modalError = string.Empty;

        // Form Fields
        [ObservableProperty] private string _newCustomerName = string.Empty;
        [ObservableProperty] private string _newCustomerPhone = string.Empty;
        [ObservableProperty] private string _newCustomerAddress = string.Empty;
        [ObservableProperty] private string _newCustomerCnic = string.Empty;
        [ObservableProperty] private string _newCustomerNtn = string.Empty;
        [ObservableProperty] private decimal _newCustomerInitialDues;
        [ObservableProperty] private decimal _newCustomerCreditLimit;

        [ObservableProperty] private decimal _paymentAmount;
        [ObservableProperty] private string _paymentMethod = "Cash";
        [ObservableProperty] private string _paymentNotes = string.Empty;
        public List<string> AvailablePaymentMethods { get; } = new() { "Cash", "Bank Transfer", "Raast", "JazzCash", "EasyPaisa" };

        private readonly BackupService _backupService;
        private readonly PrintService _printService;

        public string CustomerModalTitle => IsEditMode ? "EDIT CUSTOMER" : "ADD NEW CUSTOMER";

        public CustomersViewModel()
        {
            _backupService = new BackupService();
            _printService = new PrintService();
            LoadCustomers();
            WeakReferenceMessenger.Default.Register(this);
        }

        public void Receive(DataChangedMessage message)
        {
            if (message.Value is "Customer" or "Transaction")
            {
                App.Current?.Dispatcher.Invoke(() =>
                {
                    int? selectedId = SelectedCustomer?.Id;
                    LoadCustomers();
                    if (selectedId.HasValue)
                    {
                        SelectedCustomer = Customers.FirstOrDefault(c => c.Id == selectedId.Value);
                    }
                });
            }
        }

        partial void OnSearchTextChanged(string value) => ApplyFilter();
        partial void OnSelectedCustomerChanged(Customer? value) => LoadCustomerHistory(value);
        partial void OnIsEditModeChanged(bool value) => OnPropertyChanged(nameof(CustomerModalTitle));

        public void LoadCustomers()
        {
            using var context = new AppDbContext();
            var list = context.Customers.OrderBy(c => c.Name).ToList();
            Customers = new ObservableCollection<Customer>(list);
            ApplyFilter();
        }

        private void ApplyFilter()
        {
            if (string.IsNullOrWhiteSpace(SearchText))
            {
                FilteredCustomers = new ObservableCollection<Customer>(Customers);
            }
            else
            {
                var lower = SearchText.Trim().ToLowerInvariant();
                var filtered = Customers.Where(c =>
                    c.Name.ToLowerInvariant().Contains(lower) ||
                    c.Phone.Contains(lower) ||
                    c.Address.ToLowerInvariant().Contains(lower) ||
                    (!string.IsNullOrEmpty(c.Cnic) && c.Cnic.Contains(lower)) ||
                    (!string.IsNullOrEmpty(c.Ntn) && c.Ntn.ToLowerInvariant().Contains(lower))).ToList();
                FilteredCustomers = new ObservableCollection<Customer>(filtered);
            }
        }

        private void LoadCustomerHistory(Customer? customer)
        {
            if (customer == null)
            {
                CustomerHistory = new ObservableCollection<Sale>();
                return;
            }

            using var context = new AppDbContext();
            var history = context.Sales
                .Where(s => s.CustomerId == customer.Id && s.RecordType != "Estimate")
                .OrderByDescending(s => s.CreatedAt)
                .ToList();

            CustomerHistory = new ObservableCollection<Sale>(history);
        }

        [RelayCommand]
        private void OpenAddModal()
        {
            IsEditMode = false;
            ResetForm();
            ModalError = string.Empty;
            IsAddModalOpen = true;
        }

        [RelayCommand]
        private void OpenEditModal()
        {
            if (SelectedCustomer == null) return;
            IsEditMode = true;
            NewCustomerName = SelectedCustomer.Name;
            NewCustomerPhone = SelectedCustomer.Phone;
            NewCustomerAddress = SelectedCustomer.Address;
            NewCustomerCnic = SelectedCustomer.Cnic;
            NewCustomerNtn = SelectedCustomer.Ntn;
            NewCustomerInitialDues = SelectedCustomer.TotalDues;
            NewCustomerCreditLimit = SelectedCustomer.CreditLimit;
            ModalError = string.Empty;
            IsAddModalOpen = true;
        }

        [RelayCommand]
        private void CloseModals()
        {
            IsAddModalOpen = false;
            IsPaymentModalOpen = false;
            ModalError = string.Empty;
            ResetForm();
        }

        [RelayCommand]
        private async Task SaveCustomer()
        {
            ModalError = string.Empty;
            if (string.IsNullOrWhiteSpace(NewCustomerName))
            {
                ModalError = "Customer name is required.";
                return;
            }
            if (NewCustomerCreditLimit < 0m)
            {
                ModalError = "Credit limit cannot be negative.";
                return;
            }

            using var context = new AppDbContext();
            await using var transaction = await context.Database.BeginTransactionAsync();
            string cleanPhone = NewCustomerPhone.Trim();

            if (IsEditMode && SelectedCustomer != null)
            {
                if (!string.IsNullOrEmpty(cleanPhone) &&
                    context.Customers.Any(c => c.Phone == cleanPhone && c.Id != SelectedCustomer.Id))
                {
                    ModalError = "Another customer already has this phone number.";
                    return;
                }

                var dbCustomer = await context.Customers.FindAsync(SelectedCustomer.Id);
                if (dbCustomer != null)
                {
                    dbCustomer.Name = NewCustomerName.Trim();
                    dbCustomer.Phone = cleanPhone;
                    dbCustomer.Address = NewCustomerAddress.Trim();
                    dbCustomer.Cnic = NewCustomerCnic.Trim();
                    dbCustomer.Ntn = NewCustomerNtn.Trim();
                    dbCustomer.CreditLimit = NewCustomerCreditLimit;
                    dbCustomer.UpdatedAt = DateTime.Now;
                    var account = KhataService.GetCustomerAccount(context, dbCustomer);
                    account.Name = dbCustomer.Name;
                    account.Phone = dbCustomer.Phone;
                    account.Address = dbCustomer.Address;
                    account.CreditLimit = dbCustomer.CreditLimit;
                    account.UpdatedAt = DateTime.Now;
                    await context.SaveChangesAsync();
                    await transaction.CommitAsync();
                }

                int editedId = SelectedCustomer.Id;
                LoadCustomers();
                SelectedCustomer = Customers.FirstOrDefault(c => c.Id == editedId);
            }
            else
            {
                if (!string.IsNullOrEmpty(cleanPhone) && context.Customers.Any(c => c.Phone == cleanPhone))
                {
                    ModalError = "A customer with this phone number already exists.";
                    return;
                }

                var customer = new Customer
                {
                    Name = NewCustomerName.Trim(),
                    Phone = cleanPhone,
                    Address = NewCustomerAddress.Trim(),
                    Cnic = NewCustomerCnic.Trim(),
                    Ntn = NewCustomerNtn.Trim(),
                    TotalDues = NewCustomerInitialDues,
                    CreditLimit = NewCustomerCreditLimit,
                    CreatedAt = DateTime.Now,
                    UpdatedAt = DateTime.Now
                };

                context.Customers.Add(customer);
                await context.SaveChangesAsync();
                var account = KhataService.GetCustomerAccount(context, customer);
                await context.SaveChangesAsync();
                await transaction.CommitAsync();

                LoadCustomers();
                SelectedCustomer = Customers.FirstOrDefault(c => c.Id == customer.Id);
            }

            await _backupService.BackupCustomersAsync(Customers);
            CloseModals();
            WeakReferenceMessenger.Default.Send(DataChangedMessage.CustomerUpdated);
        }

        [RelayCommand]
        private void OpenReceipt(Sale? sale)
        {
            if (sale == null) return;

            using var context = new AppDbContext();
            SelectedSaleHistory = context.Sales
                .Include(s => s.SaleItems)
                .FirstOrDefault(s => s.Id == sale.Id);

            IsReceiptModalOpen = true;
        }

        [RelayCommand]
        private void CloseReceipt() => IsReceiptModalOpen = false;

        [RelayCommand]
        private void ReprintReceipt80mm()
        {
            if (SelectedSaleHistory != null)
            {
                _printService.PrintThermalReceipt(SelectedSaleHistory);
            }
        }

        [RelayCommand]
        private void PrintReceiptA4()
        {
            if (SelectedSaleHistory != null)
            {
                _printService.PrintA4Invoice(SelectedSaleHistory);
            }
        }

        [RelayCommand]
        private void PrintKhataStatement()
        {
            if (SelectedCustomer == null) return;
            _printService.PrintCustomerLedger(SelectedCustomer, CustomerHistory);
        }

        [RelayCommand]
        private void OpenPaymentModal()
        {
            if (SelectedCustomer == null) return;
            PaymentAmount = SelectedCustomer.TotalDues > 0m ? SelectedCustomer.TotalDues : 0m;
            PaymentMethod = "Cash";
            PaymentNotes = string.Empty;
            ModalError = string.Empty;
            IsPaymentModalOpen = true;
        }

        [RelayCommand]
        private async Task ProcessPayment()
        {
            ModalError = string.Empty;
            if (SelectedCustomer == null) return;
            if (PaymentAmount <= 0m)
            {
                ModalError = "Enter a valid payment amount greater than zero.";
                return;
            }

            using var context = new AppDbContext();
            var dbCustomer = await context.Customers.FindAsync(SelectedCustomer.Id);
            if (dbCustomer == null) return;

            decimal prevDues = dbCustomer.TotalDues;
            dbCustomer.TotalDues -= PaymentAmount;
            dbCustomer.UpdatedAt = DateTime.Now;

            var paymentRecord = new Sale
            {
                RecordType = "Payment",
                CustomerId = dbCustomer.Id,
                CustomerName = dbCustomer.Name,
                TotalAmount = 0m,
                PreviousDues = prevDues,
                AmountPaid = PaymentAmount,
                BalanceDue = dbCustomer.TotalDues,
                PaymentMethod = PaymentMethod,
                IsPaid = true,
                Notes = PaymentNotes.Trim(),
                CreatedBy = SessionService.CurrentUserName,
                CreatedAt = DateTime.Now,
                UpdatedAt = DateTime.Now,
                ReceiptNumber = $"PAY-{DateTime.Now:yyyyMMddHHmmss}-{Guid.NewGuid():N}"[..28]
            };

            context.Sales.Add(paymentRecord);
            var account = KhataService.GetCustomerAccount(context, dbCustomer);
            KhataService.Post(context, account, "Khata payment received", PaymentMethod, 0m, PaymentAmount, "CustomerPayment", paymentRecord.ReceiptNumber ?? Guid.NewGuid().ToString("N"));
            await context.SaveChangesAsync();

            LoadCustomers();
            await _backupService.BackupCustomersAsync(Customers);
            await _backupService.BackupSaleAsync(paymentRecord);
            SelectedCustomer = Customers.FirstOrDefault(c => c.Id == dbCustomer.Id);
            CloseModals();

            WeakReferenceMessenger.Default.Send(DataChangedMessage.TransactionUpdated);
            WeakReferenceMessenger.Default.Send(DataChangedMessage.CustomerUpdated);
        }

        private void ResetForm()
        {
            NewCustomerName = string.Empty;
            NewCustomerPhone = string.Empty;
            NewCustomerAddress = string.Empty;
            NewCustomerCnic = string.Empty;
            NewCustomerNtn = string.Empty;
            NewCustomerInitialDues = 0m;
            NewCustomerCreditLimit = 0m;
            PaymentAmount = 0m;
            PaymentNotes = string.Empty;
        }
    }
}
