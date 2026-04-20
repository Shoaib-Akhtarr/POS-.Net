using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using POS.Models;
using Microsoft.EntityFrameworkCore;
using POS.Services;

namespace POS.ViewModels
{
    public partial class CustomersViewModel : ObservableObject
    {
        [ObservableProperty]
        private ObservableCollection<Customer> _customers = new();

        [ObservableProperty]
        private ObservableCollection<Customer> _filteredCustomers = new();

        [ObservableProperty]
        private Customer? _selectedCustomer;

        [ObservableProperty]
        private ObservableCollection<Sale> _customerHistory = new();

        [ObservableProperty]
        private Sale? _selectedSaleHistory;

        [ObservableProperty]
        private bool _isReceiptModalOpen;

        [ObservableProperty]
        private string _searchText = string.Empty;

        // Modal States
        [ObservableProperty] private bool _isAddModalOpen;
        [ObservableProperty] private bool _isPaymentModalOpen;

        // Form Fields
        [ObservableProperty] private string _newCustomerName = string.Empty;
        [ObservableProperty] private string _newCustomerPhone = string.Empty;
        [ObservableProperty] private string _newCustomerAddress = string.Empty;
        [ObservableProperty] private decimal _newCustomerInitialDues;

        [ObservableProperty] private decimal _paymentAmount;
        [ObservableProperty] private string _paymentMethod = "Cash";
        private readonly BackupService _backupService;

        public CustomersViewModel()
        {
            _backupService = new BackupService();
            LoadCustomers();
        }

        public void LoadCustomers()
        {
            using var context = new AppDbContext();
            var list = context.Customers.OrderBy(c => c.Name).ToList();
            Customers = new ObservableCollection<Customer>(list);
            ApplyFilter();
        }

        partial void OnSearchTextChanged(string value) => ApplyFilter();

        private void ApplyFilter()
        {
            if (string.IsNullOrWhiteSpace(SearchText))
            {
                FilteredCustomers = new ObservableCollection<Customer>(Customers);
            }
            else
            {
                var lower = SearchText.ToLower();
                var filtered = Customers.Where(c => 
                    c.Name.ToLower().Contains(lower) || 
                    c.Phone.Contains(lower)).ToList();
                FilteredCustomers = new ObservableCollection<Customer>(filtered);
            }
        }

        partial void OnSelectedCustomerChanged(Customer? value)
        {
            LoadCustomerHistory(value);
        }

        private void LoadCustomerHistory(Customer? customer)
        {
            if (customer == null)
            {
                CustomerHistory = new();
                return;
            }

            using var context = new AppDbContext();
            var history = context.Sales
                .Where(s => s.CustomerId == customer.Id)
                .OrderByDescending(s => s.CreatedAt)
                .ToList();
            
            CustomerHistory = new ObservableCollection<Sale>(history);
        }

        [RelayCommand]
        private void OpenAddModal() => IsAddModalOpen = true;

        [RelayCommand]
        private void CloseModals()
        {
            IsAddModalOpen = false;
            IsPaymentModalOpen = false;
            ResetForm();
        }

        [RelayCommand]
        private async Task SaveCustomer()
        {
            if (string.IsNullOrWhiteSpace(NewCustomerName)) return;

            using var context = new AppDbContext();
            
            // Duplicate check
            if (context.Customers.Any(c => c.Phone == NewCustomerPhone && NewCustomerPhone != string.Empty))
            {
                // In a real app, use a proper dialog or toast
                return;
            }

            var customer = new Customer
            {
                Name = NewCustomerName,
                Phone = NewCustomerPhone,
                Address = NewCustomerAddress,
                TotalDues = NewCustomerInitialDues,
                CreatedAt = DateTime.Now
            };

            context.Customers.Add(customer);
            await context.SaveChangesAsync();
            
            LoadCustomers();
            await _backupService.BackupCustomersAsync(Customers);
            CloseModals();

            // Notify other ViewModels (POS, etc.)
            WeakReferenceMessenger.Default.Send(DataChangedMessage.CustomerUpdated);
        }

        [RelayCommand]
        private void OpenReceipt(Sale? sale)
        {
            if (sale == null) return;
            
            // Ensure items are loaded
            using var context = new AppDbContext();
            SelectedSaleHistory = context.Sales
                .Include(s => s.SaleItems)
                .FirstOrDefault(s => s.Id == sale.Id);
                
            IsReceiptModalOpen = true;
        }

        [RelayCommand]
        private void CloseReceipt() => IsReceiptModalOpen = false;

        [RelayCommand]
        private void OpenPaymentModal()
        {
            if (SelectedCustomer == null) return;
            IsPaymentModalOpen = true;
        }

        [RelayCommand]
        private async Task ProcessPayment()
        {
            if (SelectedCustomer == null || PaymentAmount <= 0) return;

            using var context = new AppDbContext();
            var dbCustomer = await context.Customers.FindAsync(SelectedCustomer.Id);
            
            if (dbCustomer != null)
            {
                // update dues
                dbCustomer.TotalDues -= PaymentAmount; 
                
                // Create transaction breadcrumb
                var paymentRecord = new Sale
                {
                    CustomerId = dbCustomer.Id,
                    CustomerName = dbCustomer.Name,
                    TotalAmount = 0,
                    AmountPaid = PaymentAmount,
                    PaymentMethod = PaymentMethod,
                    IsPaid = true,
                    CreatedAt = DateTime.Now,
                    ReceiptNumber = $"PAY-{DateTime.Now.Ticks}"
                };

                context.Sales.Add(paymentRecord);
                await context.SaveChangesAsync();

                LoadCustomers();
                await _backupService.BackupCustomersAsync(Customers);
                await _backupService.BackupSaleAsync(paymentRecord);
                SelectedCustomer = Customers.FirstOrDefault(c => c.Id == dbCustomer.Id);
                CloseModals();

                // Notify other ViewModels (Records, etc.)
                WeakReferenceMessenger.Default.Send(DataChangedMessage.TransactionUpdated);
                WeakReferenceMessenger.Default.Send(DataChangedMessage.CustomerUpdated);
            }
        }

        private void ResetForm()
        {
            NewCustomerName = string.Empty;
            NewCustomerPhone = string.Empty;
            NewCustomerAddress = string.Empty;
            NewCustomerInitialDues = 0;
            PaymentAmount = 0;
        }
    }
}
