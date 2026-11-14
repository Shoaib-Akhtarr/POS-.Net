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
    public partial class ReturnLineItem : ObservableObject
    {
        public int? ProductId { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Unit { get; set; } = "pcs";
        public decimal MaxQuantity { get; set; }
        public decimal UnitPrice { get; set; }
        public decimal UnitCost { get; set; }

        [ObservableProperty]
        private decimal _returnQuantity;

        public decimal ReturnTotal => ReturnQuantity * UnitPrice;

        partial void OnReturnQuantityChanged(decimal value)
        {
            if (value < 0m) _returnQuantity = 0m;
            if (value > MaxQuantity) _returnQuantity = MaxQuantity;
            OnPropertyChanged(nameof(ReturnTotal));
            ReturnChanged?.Invoke();
        }

        public event Action? ReturnChanged;
    }

    public class UnifiedRecord
    {
        public int Id { get; set; }
        public string Type { get; set; } = string.Empty; // SALE, ESTIMATE, RETURN, PURCHASE, PAYMENT
        public string RefId { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string Notes { get; set; } = string.Empty;
        public string CreatedBy { get; set; } = string.Empty;
        public decimal Amount { get; set; }
        public DateTime Date { get; set; }
        public List<string> Details { get; set; } = new();
        public object? OriginalObject { get; set; }
        public bool IsEstimate => Type == "ESTIMATE";
        public bool IsReturnableSale => Type == "SALE";
        public bool IsSaleType => OriginalObject is Sale;

        // FBR Fiscal Information
        public string? FbrInvoiceNumber { get; set; }
        public string FbrStatus { get; set; } = "NotIntegrated";
        public decimal FbrTaxAmount { get; set; }
        public bool HasFbr => !string.IsNullOrWhiteSpace(FbrInvoiceNumber) || FbrStatus == "PendingOffline" || (FbrStatus != null && FbrStatus.StartsWith("Synced"));
        public bool IsFbrPending => FbrStatus == "PendingOffline";
        public bool IsFbrSynced => FbrStatus != null && FbrStatus.StartsWith("Synced", StringComparison.OrdinalIgnoreCase);
        public string FbrBadgeText => IsFbrPending ? "FBR OFFLINE" : (IsFbrSynced ? (FbrStatus.Contains("Sandbox") ? "FBR SANDBOX" : "FBR SYNCED") : "");
    }

    public partial class RecordsViewModel : ObservableObject, IRecipient<DataChangedMessage>
    {
        [ObservableProperty] private ObservableCollection<UnifiedRecord> _allRecords = new();
        [ObservableProperty] private ObservableCollection<UnifiedRecord> _filteredRecords = new();
        [ObservableProperty] private string _searchText = string.Empty;
        [ObservableProperty] private string _selectedFilter = "All"; // All, Sales, Estimates, Returns, Purchases, Payments
        [ObservableProperty] private string _selectedDateRange = "All"; // All, Today, 7 Days, 30 Days
        [ObservableProperty] private bool _isDetailModalOpen;
        [ObservableProperty] private UnifiedRecord? _selectedRecord;
        [ObservableProperty] private string _actionError = string.Empty;

        // Return Modal
        [ObservableProperty] private bool _isReturnModalOpen;
        [ObservableProperty] private ObservableCollection<ReturnLineItem> _returnItems = new();
        [ObservableProperty] private decimal _returnRefundTotal;
        [ObservableProperty] private string _returnNotes = string.Empty;

        // Daily Cash Closing / Z-Report Modal
        [ObservableProperty] private bool _isZReportModalOpen;
        [ObservableProperty] private DateTime _zReportDate = DateTime.Today;
        [ObservableProperty] private DailyZReportSummary? _zReportData;
        [ObservableProperty] private decimal _countedDrawerCash;
        [ObservableProperty] private decimal _drawerVariance;
        [ObservableProperty] private string _zReportStatus = string.Empty;

        public List<string> AvailableDateRanges { get; } = new() { "All", "Today", "7 Days", "30 Days" };

        private readonly PrintService _printService;
        private readonly BackupService _backupService;

        public RecordsViewModel()
        {
            _printService = new PrintService();
            _backupService = new BackupService();
            RefreshData();
            WeakReferenceMessenger.Default.Register(this);
        }

        public void Receive(DataChangedMessage message)
        {
            if (message.Value == "Transaction")
            {
                App.Current?.Dispatcher.Invoke(RefreshData);
            }
        }

        partial void OnSearchTextChanged(string value) => ApplyFilter();
        partial void OnSelectedFilterChanged(string value) => ApplyFilter();
        partial void OnSelectedDateRangeChanged(string value) => ApplyFilter();

        [RelayCommand]
        public void RefreshData()
        {
            using var context = new AppDbContext();

            var sales = context.Sales
                .Include(s => s.SaleItems)
                .OrderByDescending(s => s.CreatedAt)
                .ToList()
                .Select(s =>
                {
                    string typeLabel = (s.RecordType ?? "Sale").ToUpperInvariant();
                    decimal displayAmount = typeLabel == "PAYMENT" ? s.AmountPaid : s.TotalAmount;
                    var details = typeLabel == "PAYMENT"
                        ? new List<string> { $"Khata Payment ({s.PaymentMethod}): Rs {s.AmountPaid:N0}", $"Remaining Dues: Rs {s.BalanceDue:N0}" }
                        : s.SaleItems.Select(si =>
                            $"{(si.Quantity % 1m == 0 ? si.Quantity.ToString("N0") : si.Quantity.ToString("0.##"))} {si.Unit} x {si.Name} = Rs {si.Price:N0}" +
                            (string.IsNullOrWhiteSpace(si.SerialNumber) ? "" : $" [S/N: {si.SerialNumber}]")
                        ).ToList();

                    return new UnifiedRecord
                    {
                        Id = s.Id,
                        Type = typeLabel,
                        RefId = s.ReceiptNumber ?? s.Id.ToString(),
                        Name = s.CustomerName ?? "Walk-in Customer",
                        Notes = s.Notes,
                        CreatedBy = s.CreatedBy,
                        Amount = displayAmount,
                        Date = s.CreatedAt,
                        Details = details,
                        OriginalObject = s,
                        FbrInvoiceNumber = s.FbrInvoiceNumber,
                        FbrStatus = s.FbrStatus,
                        FbrTaxAmount = s.FbrTaxAmount
                    };
                });

            var purchases = context.Purchases
                .OrderByDescending(p => p.PurchaseDate)
                .ToList()
                .Select(p => new UnifiedRecord
                {
                    Id = p.Id,
                    Type = "PURCHASE",
                    RefId = $"PO-{p.Id}",
                    Name = p.SupplierName ?? "Unknown Supplier",
                    CreatedBy = p.CreatedBy,
                    Amount = p.TotalCost,
                    Date = p.PurchaseDate,
                    Details = new List<string>
                    {
                        $"{(p.Quantity % 1m == 0 ? p.Quantity.ToString("N0") : p.Quantity.ToString("0.##"))} {p.Unit} x {p.ProductName} (at Rs {p.CostPrice:N0})"
                    },
                    OriginalObject = p
                });

            var combined = sales.Concat(purchases).OrderByDescending(r => r.Date).ToList();
            AllRecords = new ObservableCollection<UnifiedRecord>(combined);
            ApplyFilter();
        }

        private void ApplyFilter()
        {
            var query = AllRecords.AsEnumerable();

            query = SelectedFilter switch
            {
                "Sales" => query.Where(r => r.Type == "SALE"),
                "Estimates" => query.Where(r => r.Type == "ESTIMATE"),
                "Returns" => query.Where(r => r.Type == "RETURN"),
                "Purchases" => query.Where(r => r.Type == "PURCHASE"),
                "Payments" => query.Where(r => r.Type == "PAYMENT"),
                _ => query
            };

            DateTime minDate = SelectedDateRange switch
            {
                "Today" => DateTime.Today,
                "7 Days" => DateTime.Today.AddDays(-6),
                "30 Days" => DateTime.Today.AddDays(-29),
                _ => DateTime.MinValue
            };
            if (minDate > DateTime.MinValue)
            {
                query = query.Where(r => r.Date >= minDate);
            }

            if (!string.IsNullOrWhiteSpace(SearchText))
            {
                var search = SearchText.Trim().ToLowerInvariant();
                query = query.Where(r =>
                    r.RefId.ToLowerInvariant().Contains(search) ||
                    r.Name.ToLowerInvariant().Contains(search) ||
                    (!string.IsNullOrEmpty(r.Notes) && r.Notes.ToLowerInvariant().Contains(search)) ||
                    (!string.IsNullOrEmpty(r.CreatedBy) && r.CreatedBy.ToLowerInvariant().Contains(search)));
            }

            FilteredRecords = new ObservableCollection<UnifiedRecord>(query);
        }

        [RelayCommand]
        private void OpenDetail(UnifiedRecord? record)
        {
            if (record == null) return;
            SelectedRecord = record;
            ActionError = string.Empty;
            IsDetailModalOpen = true;
        }

        [RelayCommand]
        private void CloseDetail()
        {
            IsDetailModalOpen = false;
            ActionError = string.Empty;
        }

        [RelayCommand]
        private void ReprintThermal()
        {
            if (SelectedRecord?.OriginalObject is Sale sale)
            {
                _printService.PrintThermalReceipt(sale);
            }
        }

        [RelayCommand]
        private void PrintA4()
        {
            if (SelectedRecord?.OriginalObject is Sale sale)
            {
                _printService.PrintA4Invoice(sale);
            }
        }

        [RelayCommand]
        private async Task ConvertEstimateToSale()
        {
            ActionError = string.Empty;
            if (SelectedRecord?.OriginalObject is not Sale estimate || estimate.RecordType != "Estimate")
                return;

            using var context = new AppDbContext();
            using var tx = await context.Database.BeginTransactionAsync();
            try
            {
                var dbSale = await context.Sales
                    .Include(s => s.SaleItems)
                    .FirstOrDefaultAsync(s => s.Id == estimate.Id);

                if (dbSale == null) return;

                foreach (var item in dbSale.SaleItems)
                {
                    if (item.ProductId.HasValue)
                    {
                        var prod = await context.Products.FindAsync(item.ProductId.Value);
                        if (prod == null || prod.Quantity < item.Quantity)
                        {
                            ActionError = $"Insufficient stock for '{item.Name}'.";
                            return;
                        }
                        prod.Quantity -= item.Quantity;
                        prod.SalesCount += (int)Math.Ceiling(item.Quantity);
                        prod.UpdatedAt = DateTime.Now;
                    }
                }

                dbSale.RecordType = "Sale";
                dbSale.ReceiptNumber = $"INV-{DateTime.Now:yyyyMMddHHmmss}";
                dbSale.CreatedBy = SessionService.CurrentUserName;
                dbSale.UpdatedAt = DateTime.Now;

                if (dbSale.CustomerId.HasValue)
                {
                    var cust = await context.Customers.FindAsync(dbSale.CustomerId.Value);
                    if (cust != null)
                    {
                        dbSale.PreviousDues = cust.TotalDues;
                        dbSale.AmountPaid = 0m;
                        dbSale.BalanceDue = cust.TotalDues + dbSale.TotalAmount;
                        dbSale.PaymentMethod = "Credit";
                        dbSale.IsPaid = dbSale.BalanceDue <= 0m;
                        cust.TotalDues = dbSale.BalanceDue;
                        cust.TotalDiscount += dbSale.Discount;
                        cust.UpdatedAt = DateTime.Now;
                    }
                }
                else
                {
                    dbSale.AmountPaid = dbSale.TotalAmount;
                    dbSale.BalanceDue = 0m;
                    dbSale.PaymentMethod = "Cash";
                    dbSale.IsPaid = true;
                }

                await context.SaveChangesAsync();
                await tx.CommitAsync();

                _printService.PrintReceipt(dbSale);
                await _backupService.BackupSaleAsync(dbSale);

                RefreshData();
                IsDetailModalOpen = false;
                WeakReferenceMessenger.Default.Send(DataChangedMessage.ProductUpdated);
                WeakReferenceMessenger.Default.Send(DataChangedMessage.TransactionUpdated);
                WeakReferenceMessenger.Default.Send(DataChangedMessage.CustomerUpdated);
            }
            catch (Exception ex)
            {
                await tx.RollbackAsync();
                ActionError = $"Failed to convert estimate: {ex.Message}";
            }
        }

        [RelayCommand]
        private void OpenReturnModal()
        {
            if (SelectedRecord?.OriginalObject is not Sale sale || sale.RecordType != "Sale")
                return;

            using var context = new AppDbContext();
            var priorReturns = context.Sales
                .Include(s => s.SaleItems)
                .Where(s => s.RecordType == "Return" && s.OriginalSaleId == sale.Id)
                .SelectMany(s => s.SaleItems)
                .ToList();

            ReturnItems.Clear();
            foreach (var item in sale.SaleItems)
            {
                decimal alreadyReturned = priorReturns
                    .Where(r => r.ProductId == item.ProductId && string.Equals(r.Name, item.Name, StringComparison.OrdinalIgnoreCase))
                    .Sum(r => r.Quantity);
                decimal remainingReturnable = Math.Max(0m, item.Quantity - alreadyReturned);
                if (remainingReturnable <= 0m) continue;

                decimal netUnitPrice = item.Quantity > 0m ? item.Price / item.Quantity : item.EffectiveUnitPrice;
                var line = new ReturnLineItem
                {
                    ProductId = item.ProductId,
                    Name = item.Name,
                    Unit = item.Unit,
                    MaxQuantity = remainingReturnable,
                    UnitPrice = netUnitPrice,
                    UnitCost = item.UnitCost,
                    ReturnQuantity = 0m
                };
                line.ReturnChanged += RecalculateReturnTotal;
                ReturnItems.Add(line);
            }

            ReturnNotes = string.Empty;
            ReturnRefundTotal = 0m;
            ActionError = ReturnItems.Count == 0 ? "All items from this sale have already been returned." : string.Empty;
            IsDetailModalOpen = false;
            IsReturnModalOpen = true;
        }

        [RelayCommand]
        private void SelectAllForReturn()
        {
            foreach (var item in ReturnItems)
            {
                item.ReturnQuantity = item.MaxQuantity;
            }
            RecalculateReturnTotal();
        }

        private void RecalculateReturnTotal()
        {
            ReturnRefundTotal = ReturnItems.Sum(i => i.ReturnTotal);
        }

        [RelayCommand]
        private void CloseReturnModal() => IsReturnModalOpen = false;

        [RelayCommand]
        private async Task ConfirmReturn()
        {
            if (SelectedRecord?.OriginalObject is not Sale originalSale) return;
            var toReturn = ReturnItems.Where(i => i.ReturnQuantity > 0m).ToList();
            if (!toReturn.Any())
            {
                ActionError = "Select at least one item quantity to return.";
                return;
            }

            using var context = new AppDbContext();
            using var tx = await context.Database.BeginTransactionAsync();
            try
            {
                decimal totalReturn = toReturn.Sum(i => i.ReturnTotal);
                decimal prevDues = 0m;
                decimal newBalance = 0m;

                if (originalSale.CustomerId.HasValue)
                {
                    var cust = await context.Customers.FindAsync(originalSale.CustomerId.Value);
                    if (cust != null)
                    {
                        prevDues = cust.TotalDues;
                        newBalance = cust.TotalDues - totalReturn;
                        cust.TotalDues = newBalance;
                        cust.UpdatedAt = DateTime.Now;
                    }
                }

                var returnSale = new Sale
                {
                    RecordType = "Return",
                    OriginalSaleId = originalSale.Id,
                    CustomerId = originalSale.CustomerId,
                    CustomerName = originalSale.CustomerName,
                    TotalAmount = totalReturn,
                    PreviousDues = prevDues,
                    AmountPaid = originalSale.CustomerId.HasValue ? 0m : totalReturn,
                    BalanceDue = newBalance,
                    PaymentMethod = originalSale.CustomerId.HasValue ? "Khata Credit" : "Cash Refund",
                    IsPaid = true,
                    Notes = string.IsNullOrWhiteSpace(ReturnNotes)
                        ? $"Return for #{originalSale.ReceiptNumber ?? originalSale.Id.ToString()}"
                        : ReturnNotes.Trim(),
                    CreatedBy = SessionService.CurrentUserName,
                    ReceiptNumber = $"RET-{DateTime.Now:yyyyMMddHHmmss}"
                };

                foreach (var r in toReturn)
                {
                    if (r.ProductId.HasValue)
                    {
                        var prod = await context.Products.FindAsync(r.ProductId.Value);
                        if (prod != null)
                        {
                            prod.Quantity += r.ReturnQuantity;
                            prod.UpdatedAt = DateTime.Now;
                        }
                    }

                    returnSale.SaleItems.Add(new SaleItem
                    {
                        ProductId = r.ProductId,
                        Name = r.Name,
                        Quantity = r.ReturnQuantity,
                        UnitPrice = r.UnitPrice,
                        UnitCost = r.UnitCost,
                        Price = r.ReturnTotal,
                        Unit = r.Unit
                    });
                }

                context.Sales.Add(returnSale);
                await context.SaveChangesAsync();
                if (originalSale.CustomerId.HasValue && totalReturn > 0m)
                {
                    var customer = await context.Customers.FindAsync(originalSale.CustomerId.Value);
                    if (customer != null)
                    {
                        var account = KhataService.GetCustomerAccount(context, customer);
                        KhataService.Post(context, account, $"Sale return · {returnSale.ReceiptNumber}", "Return", 0m, totalReturn, "SaleReturn", returnSale.Id.ToString(), returnSale.CreatedAt);
                    }
                }
                await context.SaveChangesAsync();
                await tx.CommitAsync();

                _printService.PrintReceipt(returnSale);
                await _backupService.BackupSaleAsync(returnSale);

                IsReturnModalOpen = false;
                RefreshData();
                WeakReferenceMessenger.Default.Send(DataChangedMessage.ProductUpdated);
                WeakReferenceMessenger.Default.Send(DataChangedMessage.TransactionUpdated);
                if (originalSale.CustomerId.HasValue)
                {
                    WeakReferenceMessenger.Default.Send(DataChangedMessage.CustomerUpdated);
                }
            }
            catch (Exception ex)
            {
                await tx.RollbackAsync();
                ActionError = $"Return failed: {ex.Message}";
            }
        }

        [RelayCommand]
        private async Task SyncSingleFbrRecord(UnifiedRecord? record)
        {
            if (record?.OriginalObject is not Sale sale) return;
            try
            {
                var settings = ThemeService.GetSettings();
                var fbrService = new FbrTaxService();
                var result = await fbrService.ProcessSaleFiscalizationAsync(sale, settings);
                if (result.Success)
                {
                    using var context = new AppDbContext();
                    var dbSale = await context.Sales.FindAsync(sale.Id);
                    if (dbSale != null)
                    {
                        dbSale.FbrInvoiceNumber = sale.FbrInvoiceNumber;
                        dbSale.FbrStatus = sale.FbrStatus;
                        dbSale.FbrSyncTimestamp = sale.FbrSyncTimestamp;
                        dbSale.FbrQrCodeData = sale.FbrQrCodeData;
                        dbSale.FbrErrorMessage = sale.FbrErrorMessage;
                        dbSale.FbrTaxRate = sale.FbrTaxRate;
                        dbSale.FbrTaxAmount = sale.FbrTaxAmount;
                        await context.SaveChangesAsync();
                    }
                    RefreshData();
                }
                else
                {
                    ActionError = $"FBR sync notice: {result.Message}";
                }
            }
            catch (Exception ex)
            {
                ActionError = $"FBR sync error: {ex.Message}";
            }
        }

        partial void OnCountedDrawerCashChanged(decimal value)
        {
            if (ZReportData != null)
            {
                DrawerVariance = value - ZReportData.ExpectedDrawerCash;
                ZReportData = ZReportData with { ActualDrawerCash = value, Variance = DrawerVariance };
            }
        }

        partial void OnZReportDateChanged(DateTime value)
        {
            CalculateZReport();
        }

        [RelayCommand]
        public void OpenZReportModal()
        {
            ZReportDate = DateTime.Today;
            CountedDrawerCash = 0m;
            CalculateZReport();
            IsZReportModalOpen = true;
        }

        [RelayCommand]
        public void CloseZReportModal()
        {
            IsZReportModalOpen = false;
        }

        [RelayCommand]
        public void CalculateZReport()
        {
            using var context = new AppDbContext();
            var dayStart = ZReportDate.Date;
            var dayEnd = dayStart.AddDays(1);

            var sales = context.Sales.Include(s => s.SaleItems)
                .Where(s => s.CreatedAt >= dayStart && s.CreatedAt < dayEnd && s.RecordType != "Estimate")
                .ToList();

            int totalInvoices = sales.Count(s => s.RecordType == "Sale");
            decimal cashSales = sales.Where(s => s.RecordType == "Sale" && (string.Equals(s.PaymentMethod, "Cash", StringComparison.OrdinalIgnoreCase) || (s.AmountPaid > 0 && !string.Equals(s.PaymentMethod, "Credit", StringComparison.OrdinalIgnoreCase)))).Sum(s => s.AmountPaid);
            decimal cardSales = sales.Where(s => s.RecordType == "Sale" && string.Equals(s.PaymentMethod, "Card", StringComparison.OrdinalIgnoreCase)).Sum(s => s.AmountPaid);
            decimal bankSales = sales.Where(s => s.RecordType == "Sale" && (string.Equals(s.PaymentMethod, "Bank", StringComparison.OrdinalIgnoreCase) || string.Equals(s.PaymentMethod, "Raast", StringComparison.OrdinalIgnoreCase))).Sum(s => s.AmountPaid);
            decimal creditSales = sales.Where(s => s.RecordType == "Sale" && (s.TotalAmount - s.AmountPaid) > 0).Sum(s => s.TotalAmount - s.AmountPaid);
            decimal totalSalesRev = sales.Where(s => s.RecordType == "Sale").Sum(s => s.TotalAmount);

            var khataEntries = context.KhataEntries
                .Where(e => e.CreatedAt >= dayStart && e.CreatedAt < dayEnd)
                .ToList();
            decimal khataCashIn = khataEntries.Where(e => e.Credit > 0 && (e.SourceType == "CashIn" || e.PaymentMode == "Cash")).Sum(e => e.Credit);

            var instPayments = context.InstallmentPayments
                .Where(p => p.CreatedAt >= dayStart && p.CreatedAt < dayEnd && !p.IsReversed)
                .ToList();
            decimal instCash = instPayments.Where(p => string.Equals(p.PaymentMode, "Cash", StringComparison.OrdinalIgnoreCase)).Sum(p => p.Amount);

            decimal khataCashOut = khataEntries.Where(e => e.Debit > 0 && (e.SourceType == "CashOut" || e.PaymentMode == "Cash")).Sum(e => e.Debit);
            decimal returnRefunds = sales.Where(s => s.RecordType == "Return").Sum(s => s.AmountPaid);
            decimal cashOutTotal = khataCashOut + returnRefunds;

            decimal totalCashInflow = cashSales + khataCashIn + instCash;
            decimal expectedDrawer = Math.Max(0m, totalCashInflow - cashOutTotal);
            decimal variance = CountedDrawerCash - expectedDrawer;

            int offlineFbr = context.Sales.Count(s => s.FbrStatus == "PendingOffline");

            ZReportData = new DailyZReportSummary(
                Date: ZReportDate,
                CashierName: SessionService.CurrentUserName,
                TotalInvoicesCount: totalInvoices,
                CashSales: cashSales,
                CardSales: cardSales,
                BankSales: bankSales,
                CreditSales: creditSales,
                TotalSalesRevenue: totalSalesRev,
                KhataCashIn: khataCashIn,
                InstallmentRecoveries: instCash,
                TotalCashInflow: totalCashInflow,
                CashOutExpenses: cashOutTotal,
                ExpectedDrawerCash: expectedDrawer,
                ActualDrawerCash: CountedDrawerCash,
                Variance: variance,
                FbrOfflinePendingCount: offlineFbr);

            DrawerVariance = variance;
        }

        [RelayCommand]
        public void PrintZReport()
        {
            if (ZReportData == null) CalculateZReport();
            if (ZReportData == null) return;

            var settings = ThemeService.GetSettings();
            var result = _printService.PrintDailyZReport(ZReportDate, ZReportData, settings);
            ZReportStatus = result.message;
        }
    }
}
