using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using Microsoft.EntityFrameworkCore;
using Microsoft.Win32;
using POS.Models;
using POS.Services;

namespace POS.ViewModels
{
    public sealed record InstallmentContractRow(
        InstallmentContract Contract,
        string CustomerName,
        decimal Paid,
        decimal Outstanding,
        DateTime? NextDue,
        int OverdueCount);

    public sealed record InstallmentScheduleRow(InstallmentSchedule Schedule, decimal Outstanding);
    public sealed record CustomerDocumentRow(long Id, string DocumentType, string FileName);

    public sealed class InstallmentLineDraft : ObservableObject
    {
        public int ProductId { get; init; }
        public string ProductName { get; init; } = string.Empty;
        public string Unit { get; init; } = "pcs";
        private decimal _quantity;
        private decimal _unitPrice;
        public decimal Quantity
        {
            get => _quantity;
            set
            {
                if (SetProperty(ref _quantity, value)) OnPropertyChanged(nameof(LineTotal));
            }
        }
        public decimal UnitPrice
        {
            get => _unitPrice;
            set
            {
                if (SetProperty(ref _unitPrice, value)) OnPropertyChanged(nameof(LineTotal));
            }
        }
        public decimal UnitCost { get; init; }
        public decimal LineTotal => decimal.Round(Quantity * UnitPrice, 2, MidpointRounding.AwayFromZero);
    }

    public partial class InstallmentsViewModel : ObservableObject, IRecipient<DataChangedMessage>
    {
        private readonly PrintService _printService = new();

        [ObservableProperty] private ObservableCollection<Customer> _customers = new();
        [ObservableProperty] private ObservableCollection<Product> _products = new();
        [ObservableProperty] private ObservableCollection<InstallmentContractRow> _contracts = new();
        [ObservableProperty] private ObservableCollection<InstallmentContractRow> _dueToday = new();
        [ObservableProperty] private ObservableCollection<InstallmentContractRow> _upcoming = new();
        [ObservableProperty] private ObservableCollection<InstallmentContractRow> _overdue = new();
        [ObservableProperty] private ObservableCollection<InstallmentScheduleRow> _selectedSchedule = new();
        [ObservableProperty] private ObservableCollection<InstallmentGuarantor> _selectedGuarantors = new();
        [ObservableProperty] private ObservableCollection<CustomerDocumentRow> _customerDocuments = new();
        [ObservableProperty] private CustomerDocumentRow? _selectedCustomerDocument;
        [ObservableProperty] private ObservableCollection<KhataAccount> _recoveryAgents = new();
        [ObservableProperty] private ObservableCollection<InstallmentLineDraft> _draftItems = new();
        [ObservableProperty] private InstallmentContractRow? _selectedContract;
        [ObservableProperty] private Customer? _selectedCustomer;
        [ObservableProperty] private bool _isNewCustomerPage;
        [ObservableProperty] private bool _isNewCustomer;
        [ObservableProperty] private string _newCustomerName = string.Empty;
        [ObservableProperty] private decimal _newCustomerCreditLimit;
        [ObservableProperty] private Product? _selectedProduct;
        [ObservableProperty] private decimal _itemQuantity = 1m;
        [ObservableProperty] private decimal _itemUnitPrice;
        [ObservableProperty] private decimal _cashPrice;
        [ObservableProperty] private string _markupMode = "Percent";
        [ObservableProperty] private decimal _markupValue;
        [ObservableProperty] private decimal _processingFee;
        [ObservableProperty] private decimal _downPayment;
        [ObservableProperty] private decimal _totalPayable;
        [ObservableProperty] private decimal _installmentPreview;
        [ObservableProperty] private int _installmentsCount = 12;
        [ObservableProperty] private string _frequency = "Monthly";
        [ObservableProperty] private int _customIntervalDays = 30;
        [ObservableProperty] private DateTime _firstDueDate = DateTime.Today.AddMonths(1);
        [ObservableProperty] private int _graceDays = 3;
        [ObservableProperty] private decimal _lateFeePerInstallment;
        [ObservableProperty] private string _selectedRecoveryOfficer = string.Empty;
        [ObservableProperty] private string _customerCNIC = string.Empty;
        [ObservableProperty] private string _customerFatherName = string.Empty;
        [ObservableProperty] private string _verifiedPhone = string.Empty;
        [ObservableProperty] private bool _phoneVerified;
        [ObservableProperty] private string _residentialAddress = string.Empty;
        [ObservableProperty] private string _workAddress = string.Empty;
        [ObservableProperty] private string _cnicFrontPath = string.Empty;
        [ObservableProperty] private string _cnicBackPath = string.Empty;
        [ObservableProperty] private string _agreementPath = string.Empty;
        [ObservableProperty] private string _guarantor1Name = string.Empty;
        [ObservableProperty] private string _guarantor1CNIC = string.Empty;
        [ObservableProperty] private string _guarantor1Phone = string.Empty;
        [ObservableProperty] private string _guarantor1Address = string.Empty;
        [ObservableProperty] private string _guarantor1Relationship = string.Empty;
        [ObservableProperty] private string _guarantor2Name = string.Empty;
        [ObservableProperty] private string _guarantor2CNIC = string.Empty;
        [ObservableProperty] private string _guarantor2Phone = string.Empty;
        [ObservableProperty] private string _guarantor2Address = string.Empty;
        [ObservableProperty] private string _guarantor2Relationship = string.Empty;
        [ObservableProperty] private bool _isPaymentModalOpen;
        [ObservableProperty] private string _paymentAmount = string.Empty;
        [ObservableProperty] private string _paymentMode = "Cash";
        [ObservableProperty] private string _paymentNotes = string.Empty;
        [ObservableProperty] private InstallmentScheduleRow? _selectedScheduleForWaiver;
        [ObservableProperty] private string _waiverReason = string.Empty;
        [ObservableProperty] private string _errorMessage = string.Empty;
        [ObservableProperty] private string _statusMessage = string.Empty;
        [ObservableProperty] private bool _isErrorStatus;
        [ObservableProperty] private decimal _totalOutstanding;
        [ObservableProperty] private decimal _dueTodayTotal;
        [ObservableProperty] private decimal _upcomingTotal;
        [ObservableProperty] private decimal _overdueTotal;

        public List<string> MarkupModes { get; } = new() { "Percent", "Fixed PKR" };
        public List<string> Frequencies { get; } = new() { "Monthly", "Weekly", "Custom" };
        public List<int> Tenures { get; } = new() { 3, 6, 12, 18, 24, 36 };
        public List<string> PaymentModes { get; } = new() { "Cash", "Bank Transfer", "Raast", "JazzCash", "EasyPaisa" };
        public decimal MarkupAmount => MarkupMode == "Percent"
            ? decimal.Round(CashPrice * MarkupValue / 100m, 2, MidpointRounding.AwayFromZero)
            : decimal.Round(MarkupValue, 2, MidpointRounding.AwayFromZero);

        public InstallmentsViewModel()
        {
            WeakReferenceMessenger.Default.Register(this);
            RefreshData();
        }

        partial void OnSelectedCustomerChanged(Customer? value)
        {
            if (value == null)
            {
                CustomerCNIC = CustomerFatherName = VerifiedPhone = ResidentialAddress = WorkAddress = string.Empty;
                PhoneVerified = false;
                return;
            }
            VerifiedPhone = value.Phone;
            ResidentialAddress = value.Address;
            using var context = new AppDbContext();
            var account = context.KhataAccounts.FirstOrDefault(a => a.CustomerId == value.Id);
            CustomerCNIC = !string.IsNullOrWhiteSpace(value.Cnic) ? value.Cnic : (account?.CNIC ?? string.Empty);
            CustomerFatherName = account?.FatherOrHusbandName ?? string.Empty;
            WorkAddress = account?.WorkAddress ?? string.Empty;
            PhoneVerified = false;
        }

        partial void OnSelectedProductChanged(Product? value)
        {
            if (value != null) ItemUnitPrice = value.SellingPrice;
        }

        partial void OnMarkupModeChanged(string value) => RecalculatePlan();
        partial void OnMarkupValueChanged(decimal value) => RecalculatePlan();
        partial void OnProcessingFeeChanged(decimal value) => RecalculatePlan();
        partial void OnDownPaymentChanged(decimal value) => RecalculatePlan();
        partial void OnInstallmentsCountChanged(int value) => RecalculatePlan();
        partial void OnFrequencyChanged(string value) => RecalculatePlan();
        partial void OnCustomIntervalDaysChanged(int value) => RecalculatePlan();
        partial void OnFirstDueDateChanged(DateTime value) => RecalculatePlan();

        public void Receive(DataChangedMessage message)
        {
            if (message.Value is "Customer" or "Transaction" or "Product")
                App.Current?.Dispatcher.Invoke(RefreshData);
        }

        public void RefreshData()
        {
            using var context = new AppDbContext();
            var activeContracts = context.InstallmentContracts.Where(c => c.Status == "Active").ToList();
            bool changed = false;
            foreach (var contract in activeContracts)
            {
                var before = context.InstallmentSchedules.Where(s => s.ContractId == contract.Id).ToList();
                int existingFees = before.Count(schedule => schedule.LateFee > 0m);
                InstallmentRecoveryService.AccrueLateFees(context, contract);
                changed |= before.Count(schedule => schedule.LateFee > 0m) != existingFees;
                foreach (var schedule in before)
                {
                    string previousStatus = schedule.Status;
                    InstallmentRecoveryService.SetScheduleStatus(schedule, contract.GraceDays);
                    changed |= previousStatus != schedule.Status;
                }
                if (before.Count > 0 && before.All(schedule => schedule.Status == "Paid"))
                {
                    contract.Status = "Completed";
                    contract.ModifiedAt = DateTime.Now;
                    contract.ModifiedReason = "All installments paid";
                    changed = true;
                }
            }
            if (changed) context.SaveChanges();

            Customers = new ObservableCollection<Customer>(context.Customers.AsNoTracking().OrderBy(c => c.Name).ToList());
            Products = new ObservableCollection<Product>(context.Products.AsNoTracking().Where(p => !p.IsDeleted && p.Quantity > 0m).OrderBy(p => p.Name).ToList());
            RecoveryAgents = new ObservableCollection<KhataAccount>(context.KhataAccounts.AsNoTracking()
                .Where(a => a.PartyType == "Recovery Agent" && a.IsActive).OrderBy(a => a.Name).ToList());
            var customerNames = Customers.ToDictionary(customer => customer.Id, customer => customer.Name);
            var schedules = context.InstallmentSchedules.AsNoTracking().ToList();
            var allRows = context.InstallmentContracts.AsNoTracking().OrderByDescending(c => c.CreatedAt).ToList().Select(contract =>
            {
                var contractSchedules = schedules.Where(s => s.ContractId == contract.Id).ToList();
                decimal paid = contract.DownPayment + contractSchedules.Sum(s => s.AmountPaid + s.LateFeePaid);
                decimal balance = contractSchedules.Sum(GetScheduleOutstanding);
                var next = contractSchedules.Where(s => (s.Status != "Paid" && s.AmountPaid < s.AmountDue) || (!s.LateFeeWaived && s.LateFeePaid < s.LateFee))
                    .OrderBy(s => s.DueDate).FirstOrDefault();
                int overdueCount = contractSchedules.Count(s => s.Status == "Overdue");
                return new InstallmentContractRow(contract, customerNames.GetValueOrDefault(contract.CustomerId, "Unknown customer"), paid, balance, next?.DueDate, overdueCount);
            }).ToList();

            Contracts = new ObservableCollection<InstallmentContractRow>(allRows);
            var today = DateTime.Today;
            var todayIds = schedules.Where(s => s.Status != "Paid" && s.DueDate.Date == today && GetScheduleOutstanding(s) > 0m).Select(s => s.ContractId).ToHashSet();
            var upcomingIds = schedules.Where(s => s.Status != "Paid" && s.DueDate.Date > today && s.DueDate.Date <= today.AddDays(7) && GetScheduleOutstanding(s) > 0m).Select(s => s.ContractId).ToHashSet();
            var overdueIds = schedules.Where(s => s.Status == "Overdue").Select(s => s.ContractId).ToHashSet();
            DueToday = new ObservableCollection<InstallmentContractRow>(allRows.Where(row => todayIds.Contains(row.Contract.Id)));
            Upcoming = new ObservableCollection<InstallmentContractRow>(allRows.Where(row => upcomingIds.Contains(row.Contract.Id)));
            Overdue = new ObservableCollection<InstallmentContractRow>(allRows.Where(row => overdueIds.Contains(row.Contract.Id)));
            TotalOutstanding = allRows.Where(row => row.Contract.Status == "Active").Sum(row => row.Outstanding);
            DueTodayTotal = schedules.Where(s => s.DueDate.Date == today && s.Status != "Paid").Sum(GetScheduleOutstanding);
            UpcomingTotal = schedules.Where(s => s.DueDate.Date > today && s.DueDate.Date <= today.AddDays(7) && s.Status != "Paid").Sum(GetScheduleOutstanding);
            OverdueTotal = schedules.Where(s => s.Status == "Overdue").Sum(GetScheduleOutstanding);

            if (SelectedContract != null)
                SelectedContract = Contracts.FirstOrDefault(row => row.Contract.Id == SelectedContract.Contract.Id);
            else if (Contracts.Count > 0)
                SelectedContract = Contracts[0];
        }

        partial void OnSelectedContractChanged(InstallmentContractRow? value)
        {
            if (value == null) { SelectedSchedule.Clear(); SelectedGuarantors.Clear(); CustomerDocuments.Clear(); return; }
            using var context = new AppDbContext();
            var items = context.InstallmentSchedules.AsNoTracking().Where(s => s.ContractId == value.Contract.Id)
                .OrderBy(s => s.Sequence).ToList();
            SelectedSchedule = new ObservableCollection<InstallmentScheduleRow>(items.Select(item => new InstallmentScheduleRow(item, GetScheduleOutstanding(item))));
            SelectedGuarantors = new ObservableCollection<InstallmentGuarantor>(context.InstallmentGuarantors.AsNoTracking()
                .Where(g => g.ContractId == value.Contract.Id).ToList());
            var customerId = value.Contract.CustomerId;
            CustomerDocuments = new ObservableCollection<CustomerDocumentRow>(context.CustomerDocuments.AsNoTracking()
                .Where(document => document.CustomerId == customerId)
                .OrderBy(document => document.UploadedAt)
                .Select(document => new CustomerDocumentRow(document.Id, document.DocumentType, document.FileName)).ToList());
        }

        private void RecalculatePlan()
        {
            CashPrice = DraftItems.Sum(item => item.LineTotal);
            TotalPayable = decimal.Round(CashPrice + MarkupAmount + ProcessingFee, 2, MidpointRounding.AwayFromZero);
            if (InstallmentsCount > 0)
                InstallmentPreview = decimal.Round(Math.Max(0m, TotalPayable - DownPayment) / InstallmentsCount, 2, MidpointRounding.AwayFromZero);
            OnPropertyChanged(nameof(MarkupAmount));
        }

        private static decimal GetScheduleOutstanding(InstallmentSchedule schedule) =>
            Math.Max(0m, schedule.AmountDue - schedule.AmountPaid)
            + (schedule.LateFeeWaived ? 0m : Math.Max(0m, schedule.LateFee - schedule.LateFeePaid));

        [RelayCommand]
        private void OpenNewCustomerPage()
        {
            IsPaymentModalOpen = false;
            SelectedCustomer = null;
            IsNewCustomer = true;
            NewCustomerName = string.Empty;
            NewCustomerCreditLimit = 0m;
            SelectedProduct = null;
            DraftItems.Clear();
            MarkupMode = "Percent";
            MarkupValue = 0m;
            ProcessingFee = DownPayment = 0m;
            InstallmentsCount = 12;
            Frequency = "Monthly";
            CustomIntervalDays = 30;
            FirstDueDate = DateTime.Today.AddMonths(1);
            GraceDays = 3;
            LateFeePerInstallment = 0m;
            SelectedRecoveryOfficer = string.Empty;
            CustomerCNIC = CustomerFatherName = VerifiedPhone = ResidentialAddress = WorkAddress = string.Empty;
            PhoneVerified = false;
            CnicFrontPath = CnicBackPath = AgreementPath = string.Empty;
            ClearGuarantors();
            ErrorMessage = string.Empty;
            IsNewCustomerPage = true;
        }

        [RelayCommand]
        private void UseExistingCustomer()
        {
            IsNewCustomer = false;
            if (SelectedCustomer == null && Customers.Count > 0)
                SelectedCustomer = Customers[0];
        }

        [RelayCommand]
        private void StartNewCustomer()
        {
            IsNewCustomer = true;
            SelectedCustomer = null;
            NewCustomerName = string.Empty;
            NewCustomerCreditLimit = 0m;
            CustomerCNIC = CustomerFatherName = VerifiedPhone = ResidentialAddress = WorkAddress = string.Empty;
            PhoneVerified = false;
        }

        [RelayCommand]
        private void AddProductLine()
        {
            if (SelectedProduct == null || ItemQuantity <= 0m || ItemUnitPrice <= 0m)
            {
                ErrorMessage = "Select an in-stock product and enter a positive quantity and price.";
                return;
            }
            var existing = DraftItems.FirstOrDefault(item => item.ProductId == SelectedProduct.Id);
            if (existing != null) existing.Quantity += ItemQuantity;
            else DraftItems.Add(new InstallmentLineDraft
            {
                ProductId = SelectedProduct.Id,
                ProductName = SelectedProduct.Name,
                Unit = SelectedProduct.Unit,
                Quantity = ItemQuantity,
                UnitPrice = ItemUnitPrice,
                UnitCost = SelectedProduct.CostPrice
            });
            ItemQuantity = 1m;
            RecalculatePlan();
        }

        [RelayCommand]
        private void RemoveProductLine(InstallmentLineDraft? item)
        {
            if (item != null) DraftItems.Remove(item);
            RecalculatePlan();
        }

        [RelayCommand]
        private void BrowseCnicFront() => CnicFrontPath = ChooseDocument("CNIC front");
        [RelayCommand]
        private void BrowseCnicBack() => CnicBackPath = ChooseDocument("CNIC back");
        [RelayCommand]
        private void BrowseAgreement() => AgreementPath = ChooseDocument("Agreement / stamp paper");

        private static string ChooseDocument(string title)
        {
            var dialog = new OpenFileDialog { Title = $"Select {title}", Filter = "Documents and images|*.png;*.jpg;*.jpeg;*.pdf;*.bmp|All files|*.*" };
            return dialog.ShowDialog() == true ? dialog.FileName : string.Empty;
        }

        [RelayCommand]
        private async Task CreateContract()
        {
            ErrorMessage = string.Empty;
            if (!IsNewCustomer && SelectedCustomer == null) { ErrorMessage = "Select an existing customer."; return; }
            if (IsNewCustomer && string.IsNullOrWhiteSpace(NewCustomerName)) { ErrorMessage = "Enter the customer's full name."; return; }
            if (IsNewCustomer && NewCustomerCreditLimit < 0m) { ErrorMessage = "Credit limit cannot be negative."; return; }
            if (DraftItems.Count == 0) { ErrorMessage = "Add at least one product to the contract."; return; }
            if (!TryNormalizeCnic(CustomerCNIC, out string customerCnic)) { ErrorMessage = "Customer CNIC must contain 13 digits."; return; }
            if (!PhoneVerified || string.IsNullOrWhiteSpace(VerifiedPhone)) { ErrorMessage = "Confirm the customer's phone number before signing the contract."; return; }
            if (string.IsNullOrWhiteSpace(CustomerFatherName) || string.IsNullOrWhiteSpace(ResidentialAddress)) { ErrorMessage = "Father/husband name and residential address are required."; return; }
            if (!TryValidateGuarantor(Guarantor1Name, Guarantor1CNIC, Guarantor1Phone, Guarantor1Address, Guarantor1Relationship, out string guarantorError)) { ErrorMessage = $"First guarantor: {guarantorError}"; return; }
            bool hasSecond = new[] { Guarantor2Name, Guarantor2CNIC, Guarantor2Phone, Guarantor2Address, Guarantor2Relationship }.Any(stringValue => !string.IsNullOrWhiteSpace(stringValue));
            if (hasSecond && !TryValidateGuarantor(Guarantor2Name, Guarantor2CNIC, Guarantor2Phone, Guarantor2Address, Guarantor2Relationship, out guarantorError)) { ErrorMessage = $"Second guarantor: {guarantorError}"; return; }
            if (GraceDays is < 0 or > 90 || LateFeePerInstallment < 0m) { ErrorMessage = "Grace days and late fee must be non-negative (grace period max 90 days)."; return; }
            if (!TryValidateDocument(CnicFrontPath, out string documentError)
                || !TryValidateDocument(CnicBackPath, out documentError)
                || !TryValidateDocument(AgreementPath, out documentError))
            {
                ErrorMessage = documentError;
                return;
            }

            IReadOnlyList<PlannedInstallment> plan;
            try
            {
                plan = InstallmentPlanService.BuildSchedule(new InstallmentPlanInput(CashPrice, MarkupAmount, ProcessingFee, DownPayment,
                    InstallmentsCount, Frequency, CustomIntervalDays, FirstDueDate));
            }
            catch (Exception ex) { ErrorMessage = ex.Message; return; }

            try
            {
                using var context = new AppDbContext();
                await using var transaction = await context.Database.BeginTransactionAsync();
                Customer customer;
                if (IsNewCustomer)
                {
                    bool duplicate = await context.Customers.AnyAsync(c => c.Name.ToLower() == NewCustomerName.Trim().ToLower()
                        && c.Phone == VerifiedPhone.Trim());
                    if (duplicate) throw new InvalidOperationException("A customer with this name and phone already exists. Choose the existing customer instead.");
                    customer = new Customer
                    {
                        Name = NewCustomerName.Trim(),
                        Phone = VerifiedPhone.Trim(),
                        Address = ResidentialAddress.Trim(),
                        CreditLimit = decimal.Round(NewCustomerCreditLimit, 2, MidpointRounding.AwayFromZero),
                        CreatedAt = DateTime.Now,
                        UpdatedAt = DateTime.Now
                    };
                    context.Customers.Add(customer);
                    await context.SaveChangesAsync();
                }
                else
                {
                    customer = await context.Customers.FirstAsync(c => c.Id == SelectedCustomer!.Id);
                }
                var products = new Dictionary<int, Product>();
                foreach (var line in DraftItems)
                {
                    var product = await context.Products.FirstOrDefaultAsync(p => p.Id == line.ProductId && !p.IsDeleted);
                    if (product == null || product.Quantity < line.Quantity) throw new InvalidOperationException($"Insufficient stock for {line.ProductName}.");
                    products[product.Id] = product;
                }
                var account = KhataService.GetCustomerAccount(context, customer);
                if (account.CreditLimit > 0m && customer.TotalDues + TotalPayable - DownPayment > account.CreditLimit)
                {
                    var choice = MessageBox.Show($"This contract will take {customer.Name}'s balance above the PKR {account.CreditLimit:N2} credit limit. Continue?",
                        "Credit limit exceeded", MessageBoxButton.YesNo, MessageBoxImage.Warning);
                    if (choice != MessageBoxResult.Yes) return;
                }

                string contractNumber = $"KST-{DateTime.Now:yyyyMMdd}-{Guid.NewGuid():N}"[..22].ToUpperInvariant();
                decimal openingDues = customer.TotalDues;
                var sale = new Sale
                {
                    RecordType = "Sale",
                    CustomerId = customer.Id,
                    CustomerName = customer.Name,
                    TotalAmount = TotalPayable,
                    AmountPaid = DownPayment,
                    PreviousDues = openingDues,
                    BalanceDue = openingDues + TotalPayable - DownPayment,
                    PaymentMethod = "Installment",
                    IsPaid = false,
                    Notes = $"Installment contract {contractNumber}",
                    CreatedBy = SessionService.CurrentUserName,
                    ReceiptNumber = contractNumber
                };

                foreach (var line in DraftItems)
                {
                    var product = products[line.ProductId];
                    product.Quantity -= line.Quantity;
                    product.SalesCount += (int)Math.Ceiling(line.Quantity);
                    product.UpdatedAt = DateTime.Now;
                    sale.SaleItems.Add(new SaleItem
                    {
                        ProductId = product.Id,
                        Name = product.Name,
                        Quantity = line.Quantity,
                        UnitPrice = line.UnitPrice,
                        UnitCost = product.CostPrice,
                        Price = line.LineTotal,
                        Unit = product.Unit
                    });
                }
                context.Sales.Add(sale);
                await context.SaveChangesAsync();

                var contract = new InstallmentContract
                {
                    ContractNumber = contractNumber,
                    CustomerId = customer.Id,
                    SaleId = sale.Id,
                    CashPrice = CashPrice,
                    MarkupAmount = MarkupAmount,
                    ProcessingFee = ProcessingFee,
                    DownPayment = DownPayment,
                    TotalPayable = TotalPayable,
                    Frequency = Frequency,
                    IntervalDays = Frequency == "Monthly" ? 0 : Frequency == "Weekly" ? 7 : CustomIntervalDays,
                    InstallmentsCount = InstallmentsCount,
                    FirstDueDate = FirstDueDate.Date,
                    GraceDays = GraceDays,
                    LateFeePerInstallment = LateFeePerInstallment,
                    RecoveryOfficer = SelectedRecoveryOfficer,
                    CreatedBy = SessionService.CurrentUserName,
                    CreatedAt = DateTime.Now
                };
                context.InstallmentContracts.Add(contract);
                await context.SaveChangesAsync();

                foreach (var line in DraftItems)
                {
                    var product = products[line.ProductId];
                    context.InstallmentContractItems.Add(new InstallmentContractItem
                    {
                        ContractId = contract.Id,
                        ProductId = product.Id,
                        ProductName = product.Name,
                        Quantity = line.Quantity,
                        Unit = product.Unit,
                        UnitPrice = line.UnitPrice,
                        UnitCost = product.CostPrice
                    });
                }
                foreach (var row in plan)
                    context.InstallmentSchedules.Add(new InstallmentSchedule { ContractId = contract.Id, Sequence = row.Sequence, DueDate = row.DueDate, AmountDue = row.AmountDue });
                context.InstallmentGuarantors.Add(CreateGuarantor(contract.Id, Guarantor1Name, Guarantor1CNIC, Guarantor1Phone, Guarantor1Address, Guarantor1Relationship));
                if (hasSecond)
                    context.InstallmentGuarantors.Add(CreateGuarantor(contract.Id, Guarantor2Name, Guarantor2CNIC, Guarantor2Phone, Guarantor2Address, Guarantor2Relationship));

                account.CNIC = customerCnic;
                account.FatherOrHusbandName = CustomerFatherName.Trim();
                account.Phone = VerifiedPhone.Trim();
                account.Address = ResidentialAddress.Trim();
                account.WorkAddress = WorkAddress.Trim();
                account.UpdatedAt = DateTime.Now;
                customer.Phone = VerifiedPhone.Trim();
                customer.Address = ResidentialAddress.Trim();
                customer.Cnic = customerCnic;
                customer.TotalDues = openingDues + TotalPayable - DownPayment;
                customer.UpdatedAt = DateTime.Now;
                if (TotalPayable > 0m)
                    KhataService.Post(context, account, $"Installment contract · {contractNumber}", "Installment sale", TotalPayable, 0m,
                        "InstallmentSale", $"{contract.Id}:charge", contract.CreatedAt);
                if (DownPayment > 0m)
                    KhataService.Post(context, account, $"Down payment · {contractNumber}", "Cash", 0m, DownPayment,
                        "InstallmentDownPayment", contract.Id.ToString(), contract.CreatedAt);
                AddCustomerDocument(context, customer.Id, "CNIC front", CnicFrontPath);
                AddCustomerDocument(context, customer.Id, "CNIC back", CnicBackPath);
                AddCustomerDocument(context, customer.Id, "Agreement", AgreementPath);
                await context.SaveChangesAsync();
                await transaction.CommitAsync();

                _printService.PrintA4Invoice(sale);
                IsNewCustomerPage = false;
                IsNewCustomer = false;
                SelectedCustomer = customer;
                WeakReferenceMessenger.Default.Send(DataChangedMessage.ProductUpdated);
                WeakReferenceMessenger.Default.Send(DataChangedMessage.CustomerUpdated);
                WeakReferenceMessenger.Default.Send(DataChangedMessage.TransactionUpdated);
                RefreshData();
                SetStatus($"Contract {contractNumber} activated. {InstallmentsCount} scheduled installments created and stock deducted.", false);
            }
            catch (Exception ex) { ErrorMessage = $"Contract could not be created: {ex.Message}"; }
        }

        [RelayCommand]
        private void OpenPaymentModal()
        {
            if (SelectedContract?.Contract.Status != "Active") { SetStatus("Select an active installment contract.", true); return; }
            PaymentAmount = string.Empty;
            PaymentMode = "Cash";
            PaymentNotes = string.Empty;
            ErrorMessage = string.Empty;
            IsPaymentModalOpen = true;
        }

        [RelayCommand]
        private async Task RecordPayment()
        {
            if (SelectedContract == null) return;
            if (!decimal.TryParse(PaymentAmount, System.Globalization.NumberStyles.Number, System.Globalization.CultureInfo.CurrentCulture, out decimal amount) || amount <= 0m)
            {
                ErrorMessage = "Enter a payment greater than zero.";
                return;
            }
            try
            {
                using var context = new AppDbContext();
                await using var transaction = await context.Database.BeginTransactionAsync();
                var contract = await context.InstallmentContracts.FirstAsync(c => c.Id == SelectedContract.Contract.Id);
                var receipt = await InstallmentRecoveryService.RecordPaymentAsync(context, contract, amount, PaymentMode, PaymentNotes);
                await transaction.CommitAsync();
                IsPaymentModalOpen = false;
                RefreshData();
                WeakReferenceMessenger.Default.Send(DataChangedMessage.CustomerUpdated);
                WeakReferenceMessenger.Default.Send(DataChangedMessage.TransactionUpdated);
                var paymentReceipt = context.Sales.AsNoTracking().FirstOrDefault(s => s.ReceiptNumber == receipt.ReceiptNumber);
                if (paymentReceipt != null) _printService.PrintReceipt(paymentReceipt);
                SetStatus($"Receipt {receipt.ReceiptNumber} recorded. Next due: {SelectedContract?.NextDue?.ToString("dd MMM yyyy") ?? "contract settled"}.", false);
            }
            catch (Exception ex) { ErrorMessage = $"Payment could not be recorded: {ex.Message}"; }
        }

        [RelayCommand]
        private void SelectScheduleForWaiver(InstallmentScheduleRow? schedule) => SelectedScheduleForWaiver = schedule;

        [RelayCommand]
        private void ExportSelectedCustomerDocument()
        {
            if (SelectedCustomerDocument == null) return;
            try
            {
                using var context = new AppDbContext();
                var document = context.CustomerDocuments.AsNoTracking().FirstOrDefault(item => item.Id == SelectedCustomerDocument.Id);
                if (document == null) { SetStatus("The selected vault document was not found.", true); return; }
                var dialog = new SaveFileDialog { FileName = Path.GetFileName(document.FileName), Filter = "All files|*.*", AddExtension = false };
                if (dialog.ShowDialog() != true) return;
                File.WriteAllBytes(dialog.FileName, document.Content);
                SetStatus($"Exported {document.DocumentType} to {dialog.FileName}.", false);
            }
            catch (Exception ex) { SetStatus($"Could not export the document: {ex.Message}", true); }
        }

        [RelayCommand]
        private async Task WaiveLateFee()
        {
            if (SelectedScheduleForWaiver == null || SelectedContract == null) return;
            if (string.IsNullOrWhiteSpace(WaiverReason)) { SetStatus("A reason is required to waive a late fee.", true); return; }
            try
            {
                using var context = new AppDbContext();
                await using var transaction = await context.Database.BeginTransactionAsync();
                var schedule = await context.InstallmentSchedules.FirstAsync(s => s.Id == SelectedScheduleForWaiver.Schedule.Id);
                var customer = await context.Customers.FirstAsync(c => c.Id == SelectedContract.Contract.CustomerId);
                decimal waived = Math.Max(0m, schedule.LateFee - schedule.LateFeePaid);
                if (schedule.LateFeeWaived || waived <= 0m) throw new InvalidOperationException("This schedule has no outstanding late fee to waive.");
                schedule.LateFeeWaived = true;
                schedule.WaiverReason = WaiverReason.Trim();
                customer.TotalDues -= waived;
                customer.UpdatedAt = DateTime.Now;
                InstallmentRecoveryService.SetScheduleStatus(schedule, SelectedContract.Contract.GraceDays);
                var account = KhataService.GetCustomerAccount(context, customer);
                KhataService.Post(context, account, $"Late fee waived · {SelectedContract.Contract.ContractNumber} · {schedule.Sequence} · {WaiverReason.Trim()}",
                    "Waiver", 0m, waived, "InstallmentLateFeeWaiver", schedule.Id.ToString());
                await context.SaveChangesAsync();
                await transaction.CommitAsync();
                WaiverReason = string.Empty;
                RefreshData();
                WeakReferenceMessenger.Default.Send(DataChangedMessage.CustomerUpdated);
                SetStatus($"PKR {waived:N2} late fee waived and ledger adjusted.", false);
            }
            catch (Exception ex) { SetStatus($"Could not waive late fee: {ex.Message}", true); }
        }

        [RelayCommand]
        private void CloseModal() { IsPaymentModalOpen = false; IsNewCustomerPage = false; ErrorMessage = string.Empty; }

        private void ClearGuarantors()
        {
            Guarantor1Name = Guarantor1CNIC = Guarantor1Phone = Guarantor1Address = Guarantor1Relationship = string.Empty;
            Guarantor2Name = Guarantor2CNIC = Guarantor2Phone = Guarantor2Address = Guarantor2Relationship = string.Empty;
        }

        private static InstallmentGuarantor CreateGuarantor(int contractId, string name, string cnic, string phone, string address, string relationship)
        {
            TryNormalizeCnic(cnic, out string normalized);
            return new InstallmentGuarantor
            {
                ContractId = contractId,
                FullName = name.Trim(),
                CNIC = normalized,
                Phone = phone.Trim(),
                Address = address.Trim(),
                Relationship = relationship.Trim()
            };
        }

        private static bool TryValidateGuarantor(string name, string cnic, string phone, string address, string relationship, out string error)
        {
            error = string.Empty;
            if (string.IsNullOrWhiteSpace(name)) error = "name is required.";
            else if (!TryNormalizeCnic(cnic, out _)) error = "CNIC must contain 13 digits.";
            else if (string.IsNullOrWhiteSpace(phone)) error = "phone number is required.";
            else if (string.IsNullOrWhiteSpace(address)) error = "address is required.";
            else if (string.IsNullOrWhiteSpace(relationship)) error = "relationship is required.";
            return error.Length == 0;
        }

        private static bool TryNormalizeCnic(string value, out string normalized)
        {
            string digits = new string(value.Where(char.IsDigit).ToArray());
            normalized = digits.Length == 13 ? $"{digits[..5]}-{digits.Substring(5, 7)}-{digits[^1]}" : string.Empty;
            return digits.Length == 13;
        }

        private static bool TryValidateDocument(string path, out string error)
        {
            error = string.Empty;
            if (string.IsNullOrWhiteSpace(path)) return true;
            if (!File.Exists(path)) { error = "One of the selected customer documents cannot be found."; return false; }
            if (new FileInfo(path).Length > 12 * 1024 * 1024) { error = "Customer documents must be 12 MB or smaller."; return false; }
            return true;
        }

        private static void AddCustomerDocument(AppDbContext context, int customerId, string type, string path)
        {
            if (string.IsNullOrWhiteSpace(path)) return;
            context.CustomerDocuments.Add(new CustomerDocument
            {
                CustomerId = customerId,
                DocumentType = type,
                FileName = Path.GetFileName(path),
                ContentType = Path.GetExtension(path).ToLowerInvariant() switch
                {
                    ".png" => "image/png", ".jpg" or ".jpeg" => "image/jpeg", ".bmp" => "image/bmp", ".pdf" => "application/pdf", _ => "application/octet-stream"
                },
                Content = File.ReadAllBytes(path),
                UploadedBy = SessionService.CurrentUserName,
                UploadedAt = DateTime.Now
            });
        }

        private void SetStatus(string message, bool isError) { StatusMessage = message; IsErrorStatus = isError; }
    }
}
