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
    public sealed record KhataAccountRow(KhataAccount Account, decimal Balance)
    {
        public string BalanceLabel => Balance > 0m ? "Banaam · Receivable" : Balance < 0m ? "Jama · Payable" : "Settled";
        public decimal AbsoluteBalance => Math.Abs(Balance);
    }

    public sealed record KhataEntryRow(DateTime Date, string Voucher, string Description, string Mode, decimal Debit, decimal Credit, decimal RunningBalance);
    public sealed record PaymentInstrumentRow(PaymentInstrument Instrument, string PartyName);

    public partial class KhataViewModel : ObservableObject, IRecipient<DataChangedMessage>
    {
        private readonly PrintService _printService = new();
        [ObservableProperty] private ObservableCollection<KhataAccountRow> _accounts = new();
        [ObservableProperty] private ObservableCollection<KhataEntryRow> _entries = new();
        [ObservableProperty] private ObservableCollection<PaymentInstrumentRow> _instruments = new();
        [ObservableProperty] private ObservableCollection<InstallmentContractChoice> _activeContracts = new();
        [ObservableProperty] private KhataAccountRow? _selectedAccount;
        [ObservableProperty] private decimal _totalReceivable;
        [ObservableProperty] private decimal _totalPayable;
        [ObservableProperty] private int _pendingChequeCount;
        [ObservableProperty] private string _searchText = string.Empty;
        [ObservableProperty] private bool _isAccountModalOpen;
        [ObservableProperty] private bool _isPaymentModalOpen;
        [ObservableProperty] private string _errorMessage = string.Empty;
        [ObservableProperty] private string _statusMessage = string.Empty;
        [ObservableProperty] private bool _isErrorStatus;

        [ObservableProperty] private string _newPartyType = "Supplier";
        [ObservableProperty] private string _newPartyName = string.Empty;
        [ObservableProperty] private string _newPartyPhone = string.Empty;
        [ObservableProperty] private string _newPartyCnic = string.Empty;
        [ObservableProperty] private string _newPartyFatherName = string.Empty;
        [ObservableProperty] private string _newPartyAddress = string.Empty;
        [ObservableProperty] private decimal _newPartyCreditLimit;
        [ObservableProperty] private decimal _newPartyOpeningBalance;
        [ObservableProperty] private string _newPartyOpeningSide = "Payable · Jama";

        [ObservableProperty] private string _paymentDirection = "In";
        [ObservableProperty] private decimal _paymentAmount;
        [ObservableProperty] private string _paymentMode = "Cash";
        [ObservableProperty] private string _paymentNotes = string.Empty;
        [ObservableProperty] private string _chequeNumber = string.Empty;
        [ObservableProperty] private string _bankName = string.Empty;
        [ObservableProperty] private DateTime _clearingDate = DateTime.Today;
        [ObservableProperty] private PaymentInstrumentRow? _selectedInstrument;
        [ObservableProperty] private int? _selectedInstallmentContractId;
        [ObservableProperty] private string _bounceReason = string.Empty;

        public List<string> PartyTypes { get; } = new() { "Supplier", "Recovery Agent" };
        public List<string> OpeningBalanceSides { get; } = new() { "Payable · Jama", "Receivable · Banaam" };
        public List<string> PaymentModes { get; } = new() { "Cash", "Bank Transfer", "Raast", "JazzCash", "EasyPaisa", "PDC" };
        public bool IsPdc => PaymentMode == "PDC";
        public KhataViewModel()
        {
            WeakReferenceMessenger.Default.Register(this);
            RefreshData();
        }

        partial void OnSelectedAccountChanged(KhataAccountRow? value)
        {
            LoadEntries(value?.Account.Id);
            LoadActiveInstallmentContracts(value?.Account.CustomerId);
        }
        partial void OnSearchTextChanged(string value) => RefreshData();
        partial void OnPaymentModeChanged(string value) => OnPropertyChanged(nameof(IsPdc));

        public void Receive(DataChangedMessage message)
        {
            if (message.Value is "Customer" or "Transaction" or "Product")
                App.Current?.Dispatcher.Invoke(RefreshData);
        }

        public void RefreshData()
        {
            int? selectedId = SelectedAccount?.Account.Id;
            using var context = new AppDbContext();
            var allAccounts = context.KhataAccounts.AsNoTracking().Where(a => a.IsActive).OrderBy(a => a.PartyType).ThenBy(a => a.Name).ToList();
            var balances = context.KhataEntries.AsNoTracking().ToList()
                .GroupBy(entry => entry.AccountId)
                .ToDictionary(group => group.Key, group => group.Sum(entry => entry.Debit - entry.Credit));
            var allRows = allAccounts.Select(account => new KhataAccountRow(account, balances.GetValueOrDefault(account.Id))).ToList();
            TotalReceivable = allRows.Where(row => row.Balance > 0m).Sum(row => row.Balance);
            TotalPayable = allRows.Where(row => row.Balance < 0m).Sum(row => -row.Balance);
            string filter = SearchText.Trim();
            Accounts = new ObservableCollection<KhataAccountRow>(string.IsNullOrEmpty(filter)
                ? allRows
                : allRows.Where(row => row.Account.Name.Contains(filter, StringComparison.OrdinalIgnoreCase)
                    || row.Account.PartyType.Contains(filter, StringComparison.OrdinalIgnoreCase)
                    || row.Account.Phone.Contains(filter, StringComparison.OrdinalIgnoreCase)));

            var accountNames = allAccounts.ToDictionary(account => account.Id, account => account.Name);
            var pending = context.PaymentInstruments.AsNoTracking()
                .Where(instrument => instrument.Status == "Pending" || instrument.Status == "Cleared")
                .OrderBy(instrument => instrument.ClearingDate)
                .ToList();
            Instruments = new ObservableCollection<PaymentInstrumentRow>(pending.Select(instrument =>
                new PaymentInstrumentRow(instrument, accountNames.GetValueOrDefault(instrument.AccountId, "Unknown party"))));
            PendingChequeCount = pending.Count(instrument => instrument.Status == "Pending");

            SelectedAccount = selectedId.HasValue
                ? Accounts.FirstOrDefault(row => row.Account.Id == selectedId.Value) ?? Accounts.FirstOrDefault()
                : Accounts.FirstOrDefault();
        }

        private void LoadEntries(int? accountId)
        {
            if (!accountId.HasValue)
            {
                Entries.Clear();
                return;
            }

            using var context = new AppDbContext();
            var history = context.KhataEntries.AsNoTracking().Where(entry => entry.AccountId == accountId.Value)
                .OrderBy(entry => entry.CreatedAt).ThenBy(entry => entry.Id).ToList();
            decimal running = 0m;
            Entries = new ObservableCollection<KhataEntryRow>(history.Select(entry =>
            {
                running += entry.Debit - entry.Credit;
                return new KhataEntryRow(entry.CreatedAt, entry.VoucherNumber, entry.Description, entry.PaymentMode, entry.Debit, entry.Credit, running);
            }).Reverse());
        }

        private void LoadActiveInstallmentContracts(int? customerId)
        {
            if (!customerId.HasValue)
            {
                ActiveContracts.Clear();
                SelectedInstallmentContractId = null;
                return;
            }
            using var context = new AppDbContext();
            ActiveContracts = new ObservableCollection<InstallmentContractChoice>(context.InstallmentContracts.AsNoTracking()
                .Where(contract => contract.CustomerId == customerId.Value && contract.Status == "Active")
                .OrderByDescending(contract => contract.CreatedAt)
                .Select(contract => new InstallmentContractChoice(contract.Id, contract.ContractNumber)).ToList());
            SelectedInstallmentContractId = null;
        }

        [RelayCommand]
        private void PrintSelectedStatement()
        {
            if (SelectedAccount == null) return;
            using var context = new AppDbContext();
            var source = context.KhataEntries.AsNoTracking().Where(entry => entry.AccountId == SelectedAccount.Account.Id)
                .OrderBy(entry => entry.CreatedAt).ThenBy(entry => entry.Id).ToList();
            decimal running = 0m;
            var rows = source.Select(entry =>
            {
                running += entry.Debit - entry.Credit;
                return (entry, running);
            }).ToList();
            var result = _printService.PrintPartyLedger(SelectedAccount.Account, rows);
            if (result.success) SetStatus("Party statement sent to the selected printer.", false);
            else if (!string.IsNullOrWhiteSpace(result.message)) SetStatus(result.message, true);
        }

        [RelayCommand]
        private void PrintDailyCashBook()
        {
            using var context = new AppDbContext();
            var names = context.KhataAccounts.AsNoTracking().ToDictionary(account => account.Id, account => account.Name);
            var from = DateTime.Today;
            var to = from.AddDays(1);
            var entries = new List<DailyCashBookLine>();
            var receipts = context.Sales.AsNoTracking()
                .Where(sale => sale.CreatedAt >= from && sale.CreatedAt < to &&
                    (sale.RecordType == "Sale" || sale.RecordType == "Payment" || sale.RecordType == "Return" || sale.RecordType == "Payment Reversal"))
                .ToList();
            foreach (var sale in receipts)
            {
                decimal received = sale.RecordType == "Payment Reversal" ? 0m : Math.Max(0m, sale.AmountPaid);
                decimal paidOut = sale.RecordType == "Payment Reversal" ? Math.Abs(sale.AmountPaid) : sale.RecordType == "Return" ? Math.Max(0m, sale.AmountPaid) : 0m;
                if (received == 0m && paidOut == 0m) continue;
                entries.Add(new DailyCashBookLine(sale.CreatedAt, sale.ReceiptNumber ?? sale.Id.ToString(), sale.CustomerName ?? "Walk-in",
                    sale.Notes, sale.PaymentMethod, received, paidOut));
            }

            var cashMoves = context.KhataEntries.AsNoTracking()
                .Where(entry => entry.SourceType == "CashMovement" && entry.CreatedAt >= from && entry.CreatedAt < to)
                .ToList();
            foreach (var entry in cashMoves)
                entries.Add(new DailyCashBookLine(entry.CreatedAt, entry.VoucherNumber, names.GetValueOrDefault(entry.AccountId, "Party"), entry.Description,
                    entry.PaymentMode, entry.Credit, entry.Debit));

            var clearedPdc = context.KhataEntries.AsNoTracking()
                .Where(entry => entry.SourceType == "PDC" && entry.CreatedAt >= from && entry.CreatedAt < to)
                .ToList();
            foreach (var entry in clearedPdc)
            {
                var instrumentId = long.TryParse(entry.SourceKey, out long id) ? id : 0;
                bool installmentPdc = context.PaymentInstruments.AsNoTracking().Any(instrument => instrument.Id == instrumentId && instrument.InstallmentContractId.HasValue);
                if (installmentPdc) continue;
                entries.Add(new DailyCashBookLine(entry.CreatedAt, entry.VoucherNumber, names.GetValueOrDefault(entry.AccountId, "Party"), entry.Description,
                    entry.PaymentMode, entry.Credit, entry.Debit));
            }

            entries = entries.OrderBy(entry => entry.Date).ThenBy(entry => entry.Voucher).ToList();
            var result = _printService.PrintDailyCashBook(DateTime.Today, entries);
            if (result.success) SetStatus("Daily cash book sent to the selected printer.", false);
            else if (!string.IsNullOrWhiteSpace(result.message)) SetStatus(result.message, true);
        }

        [RelayCommand]
        private void OpenAccountModal()
        {
            NewPartyType = "Supplier";
            NewPartyName = NewPartyPhone = NewPartyCnic = NewPartyFatherName = NewPartyAddress = string.Empty;
            NewPartyCreditLimit = 0m;
            NewPartyOpeningBalance = 0m;
            NewPartyOpeningSide = "Payable · Jama";
            ErrorMessage = string.Empty;
            IsAccountModalOpen = true;
        }

        [RelayCommand]
        private async Task SavePartyAccount()
        {
            ErrorMessage = string.Empty;
            if (string.IsNullOrWhiteSpace(NewPartyName)) { ErrorMessage = "Enter a party name."; return; }
            if (NewPartyCreditLimit < 0m) { ErrorMessage = "Credit limit cannot be negative."; return; }
            if (NewPartyOpeningBalance < 0m) { ErrorMessage = "Opening balance cannot be negative; choose the balance side instead."; return; }
            try
            {
                using var context = new AppDbContext();
                await using var transaction = await context.Database.BeginTransactionAsync();
                var duplicate = context.KhataAccounts.Any(account => account.PartyType == NewPartyType && account.Name.ToLower() == NewPartyName.Trim().ToLower());
                if (duplicate) { ErrorMessage = "A ledger with this name and type already exists."; return; }
                var account = new KhataAccount
                {
                    PartyType = NewPartyType,
                    Name = NewPartyName.Trim(),
                    Phone = NewPartyPhone.Trim(),
                    CNIC = NewPartyCnic.Trim(),
                    FatherOrHusbandName = NewPartyFatherName.Trim(),
                    Address = NewPartyAddress.Trim(),
                    CreditLimit = NewPartyCreditLimit,
                    CreatedBy = SessionService.CurrentUserName,
                    CreatedAt = DateTime.Now,
                    UpdatedAt = DateTime.Now
                };
                context.KhataAccounts.Add(account);
                await context.SaveChangesAsync();
                if (NewPartyOpeningBalance > 0m)
                {
                    bool payable = NewPartyOpeningSide == "Payable · Jama";
                    KhataService.Post(context, account, "Opening balance", "Opening", payable ? 0m : NewPartyOpeningBalance,
                        payable ? NewPartyOpeningBalance : 0m, "PartyOpening", account.Id.ToString());
                    await context.SaveChangesAsync();
                }
                await transaction.CommitAsync();
                IsAccountModalOpen = false;
                RefreshData();
                SetStatus($"{NewPartyType} ledger created.", false);
            }
            catch (Exception ex) { ErrorMessage = $"Could not create ledger: {ex.Message}"; }
        }

        [RelayCommand]
        private void OpenCashIn() => OpenPayment("In");

        [RelayCommand]
        private void OpenCashOut() => OpenPayment("Out");

        private void OpenPayment(string direction)
        {
            if (SelectedAccount == null) { SetStatus("Select a party ledger first.", true); return; }
            PaymentDirection = direction;
            PaymentAmount = 0m;
            PaymentMode = "Cash";
            PaymentNotes = ChequeNumber = BankName = BounceReason = string.Empty;
            SelectedInstallmentContractId = null;
            ClearingDate = DateTime.Today;
            ErrorMessage = string.Empty;
            IsPaymentModalOpen = true;
        }

        [RelayCommand]
        private async Task SavePayment()
        {
            if (SelectedAccount == null) return;
            ErrorMessage = string.Empty;
            if (PaymentAmount <= 0m) { ErrorMessage = "Enter an amount greater than zero."; return; }
            try
            {
                using var context = new AppDbContext();
                await using var transaction = await context.Database.BeginTransactionAsync();
                var account = await context.KhataAccounts.FirstAsync(a => a.Id == SelectedAccount.Account.Id);
                if (IsPdc)
                {
                    await PaymentInstrumentService.RecordAsync(context, new PaymentInstrument
                    {
                        AccountId = account.Id,
                        Direction = PaymentDirection,
                        Amount = decimal.Round(PaymentAmount, 2, MidpointRounding.AwayFromZero),
                        ChequeNumber = ChequeNumber.Trim(),
                        BankName = BankName.Trim(),
                        ClearingDate = ClearingDate,
                        Notes = PaymentNotes.Trim(),
                        InstallmentContractId = PaymentDirection == "In" ? SelectedInstallmentContractId : null
                    });
                    await transaction.CommitAsync();
                    IsPaymentModalOpen = false;
                    RefreshData();
                    SetStatus("Post-dated cheque recorded as pending. Ledger balance will change when it clears.", false);
                    return;
                }

                decimal debit = PaymentDirection == "Out" ? PaymentAmount : 0m;
                decimal credit = PaymentDirection == "In" ? PaymentAmount : 0m;
                KhataService.Post(context, account, PaymentNotes.Trim().Length == 0 ? $"Cash {PaymentDirection.ToLowerInvariant()}" : PaymentNotes.Trim(), PaymentMode, debit, credit, "CashMovement", Guid.NewGuid().ToString("N"));
                await PaymentInstrumentService.ApplyCustomerBalanceDelta(context, account, debit - credit);
                await context.SaveChangesAsync();
                await transaction.CommitAsync();

                IsPaymentModalOpen = false;
                RefreshData();
                WeakReferenceMessenger.Default.Send(DataChangedMessage.CustomerUpdated);
                WeakReferenceMessenger.Default.Send(DataChangedMessage.TransactionUpdated);
                SetStatus("Khata cash movement posted.", false);
            }
            catch (Exception ex) { ErrorMessage = $"Could not post payment: {ex.Message}"; }
        }

        [RelayCommand]
        private async Task ClearSelectedCheque()
        {
            if (SelectedInstrument == null) return;
            try
            {
                using var context = new AppDbContext();
                await PaymentInstrumentService.TransitionAsync(context, SelectedInstrument.Instrument.Id, "Cleared", "Cheque cleared");
                RefreshData();
                WeakReferenceMessenger.Default.Send(DataChangedMessage.CustomerUpdated);
                WeakReferenceMessenger.Default.Send(DataChangedMessage.TransactionUpdated);
                SetStatus("Cheque cleared and party balance updated.", false);
            }
            catch (Exception ex) { SetStatus($"Could not clear cheque: {ex.Message}", true); }
        }

        [RelayCommand]
        private async Task BounceSelectedCheque()
        {
            if (SelectedInstrument == null) return;
            if (string.IsNullOrWhiteSpace(BounceReason)) { SetStatus("Enter a bounce reason before posting the reversal.", true); return; }
            try
            {
                using var context = new AppDbContext();
                await PaymentInstrumentService.TransitionAsync(context, SelectedInstrument.Instrument.Id, "Bounced", BounceReason);
                RefreshData();
                WeakReferenceMessenger.Default.Send(DataChangedMessage.CustomerUpdated);
                WeakReferenceMessenger.Default.Send(DataChangedMessage.TransactionUpdated);
                BounceReason = string.Empty;
                SetStatus("Cheque marked bounced. Any cleared posting has been reversed.", false);
            }
            catch (Exception ex) { SetStatus($"Could not bounce cheque: {ex.Message}", true); }
        }

        [RelayCommand]
        private void CloseModal() { IsAccountModalOpen = false; IsPaymentModalOpen = false; ErrorMessage = string.Empty; }

        private void SetStatus(string message, bool isError) { StatusMessage = message; IsErrorStatus = isError; }
    }
}
