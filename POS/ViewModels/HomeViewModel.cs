using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using Microsoft.EntityFrameworkCore;
using POS.Models;
using POS.Services;

namespace POS.ViewModels
{
    public sealed record SalesTrendPoint(string Label, decimal Amount, double BarHeight);
    public sealed record TopProductItem(string Name, string Category, string? ImagePath, int SalesCount, decimal Revenue, double ProgressPercent);
    public sealed record PaymentMethodItem(string Method, decimal Amount, int Count, double Percent);
    public sealed record DepartmentRevenueItem(string Department, decimal Amount, int Count, double Percent);
    public sealed record MonthlyTrendPoint(string Month, decimal Amount, double PointHeight);

    public partial class HomeViewModel : ObservableObject, IRecipient<DataChangedMessage>
    {
        [ObservableProperty] private decimal _totalSalesRevenue;
        [ObservableProperty] private decimal _weeklyProfit;
        [ObservableProperty] private decimal _weeklyPurchasesSum;
        [ObservableProperty] private decimal _totalPendingDues;
        [ObservableProperty] private int _totalOrdersCount;
        [ObservableProperty] private string _currentUserName = "Admin";
        [ObservableProperty] private ObservableCollection<TopProductItem> _topSellingProducts = new();
        [ObservableProperty] private int _lowInStockCount;
        [ObservableProperty] private ObservableCollection<Product> _lowStockProducts = new();
        [ObservableProperty] private bool _isLowStockModalOpen;
        [ObservableProperty] private bool _isAlertDismissed;
        [ObservableProperty] private int _criticalAlertCount;
        [ObservableProperty] private string _lowestUnitInfo = "None";

        // Financial Intelligence Metrics
        [ObservableProperty] private decimal _khataTotalReceivable;
        [ObservableProperty] private decimal _khataTotalPayable;
        [ObservableProperty] private decimal _activeInstallmentPortfolio;
        [ObservableProperty] private int _overdueInstallmentCount;
        [ObservableProperty] private decimal _overdueInstallmentAmount;
        [ObservableProperty] private int _fbrOfflinePendingCount;
        public bool HasFbrOfflinePending => FbrOfflinePendingCount > 0;

        [ObservableProperty] private string _selectedPeriod = "Last 7 Days";
        public List<string> AvailablePeriods { get; } = new() { "Today", "Last 7 Days", "Last 30 Days", "All Time" };

        [ObservableProperty] private ObservableCollection<SalesTrendPoint> _salesTrend = new();
        [ObservableProperty] private ObservableCollection<PaymentMethodItem> _paymentMethodMix = new();
        [ObservableProperty] private ObservableCollection<DepartmentRevenueItem> _departmentRevenue = new();
        [ObservableProperty] private ObservableCollection<MonthlyTrendPoint> _monthlyTrend = new();
        [ObservableProperty] private decimal _totalAmountPaid;
        [ObservableProperty] private string _lastRefreshedLabel = "Updated now";

        public bool IsOwner => SessionService.IsOwner;

        public HomeViewModel()
        {
            WeakReferenceMessenger.Default.Register(this);
            RefreshData();
        }

        public void Receive(DataChangedMessage message)
        {
            if (message.Value is "Transaction" or "Product" or "Customer")
            {
                App.Current?.Dispatcher.Invoke(RefreshData);
            }
            else if (message.Value == "Session")
            {
                App.Current?.Dispatcher.Invoke(() => OnPropertyChanged(nameof(IsOwner)));
            }
        }

        partial void OnSelectedPeriodChanged(string value) => RefreshData();

        [RelayCommand]
        private void ChangePeriod(string period) => SelectedPeriod = period;

        [RelayCommand]
        private void QuickNewSale() => WeakReferenceMessenger.Default.Send(new NavigationRequestMessage("POS"));

        [RelayCommand]
        private void QuickStockIn() => WeakReferenceMessenger.Default.Send(new NavigationRequestMessage("Purchases"));

        [RelayCommand]
        private void QuickKhata() => WeakReferenceMessenger.Default.Send(new NavigationRequestMessage("Khata"));

        [RelayCommand]
        private void QuickInstallments() => WeakReferenceMessenger.Default.Send(new NavigationRequestMessage("Installments"));

        [RelayCommand]
        private void QuickProducts() => WeakReferenceMessenger.Default.Send(new NavigationRequestMessage("Products"));

        [RelayCommand]
        private void QuickRecords() => WeakReferenceMessenger.Default.Send(new NavigationRequestMessage("Records"));

        [RelayCommand]
        private void ToggleLowStockModal()
        {
            IsLowStockModalOpen = !IsLowStockModalOpen;
            if (IsLowStockModalOpen) IsAlertDismissed = false;
        }

        [RelayCommand]
        private void DismissAlert() => IsAlertDismissed = true;

        [RelayCommand]
        private void RestoreAlert() => IsAlertDismissed = false;

        [RelayCommand]
        private void RefreshDashboard() => RefreshData();

        public void RefreshData()
        {
            using var context = new AppDbContext();
            DateTime startDate = SelectedPeriod switch
            {
                "Today" => DateTime.Today,
                "Last 30 Days" => DateTime.Today.AddDays(-29),
                "All Time" => DateTime.MinValue,
                _ => DateTime.Today.AddDays(-6)
            };

            var periodTransactions = context.Sales
                .AsNoTracking()
                .Include(s => s.SaleItems)
                .Where(s => s.CreatedAt >= startDate && s.RecordType != "Estimate" && s.RecordType != "Payment")
                .ToList();

            var salesList = periodTransactions.Where(s => s.RecordType == "Sale").ToList();
            var returnsList = periodTransactions.Where(s => s.RecordType == "Return").ToList();
            var installmentTerms = context.InstallmentContracts.AsNoTracking()
                .Where(contract => salesList.Select(sale => sale.Id).Contains(contract.SaleId))
                .ToDictionary(contract => contract.SaleId, contract => contract.MarkupAmount + contract.ProcessingFee);

            decimal grossSales = salesList.Sum(s => s.TotalAmount);
            decimal grossReturns = returnsList.Sum(s => s.TotalAmount);
            TotalSalesRevenue = Math.Max(0m, grossSales - grossReturns);
            TotalOrdersCount = salesList.Count;
            CurrentUserName = SessionService.CurrentUserName;

            // Load product cost dictionary once to avoid N+1 queries
            var productIdsNeedingCost = periodTransactions
                .SelectMany(sale => sale.SaleItems)
                .Where(item => item.UnitCost <= 0m && item.ProductId.HasValue)
                .Select(item => item.ProductId!.Value)
                .Distinct()
                .ToList();
            var productCosts = productIdsNeedingCost.Count == 0
                ? new Dictionary<int, decimal>()
                : context.Products.AsNoTracking()
                    .Where(product => productIdsNeedingCost.Contains(product.Id))
                    .ToDictionary(product => product.Id, product => product.CostPrice);

            decimal netProfit = 0m;
            foreach (var sale in salesList)
            {
                decimal itemsProfit = 0m;
                foreach (var item in sale.SaleItems)
                {
                    decimal cost = item.UnitCost > 0m
                        ? item.UnitCost
                        : (item.ProductId.HasValue && productCosts.TryGetValue(item.ProductId.Value, out decimal c) ? c : 0m);
                    itemsProfit += item.Price - (cost * item.Quantity);
                }
                netProfit += itemsProfit - sale.Discount + installmentTerms.GetValueOrDefault(sale.Id);
            }

            foreach (var ret in returnsList)
            {
                decimal retProfit = 0m;
                foreach (var item in ret.SaleItems)
                {
                    decimal cost = item.UnitCost > 0m
                        ? item.UnitCost
                        : (item.ProductId.HasValue && productCosts.TryGetValue(item.ProductId.Value, out decimal c) ? c : 0m);
                    retProfit += item.Price - (cost * item.Quantity);
                }
                netProfit -= retProfit;
            }
            WeeklyProfit = netProfit;

            WeeklyPurchasesSum = context.Purchases.AsNoTracking()
                .Where(p => p.PurchaseDate >= startDate)
                .Select(p => (decimal?)p.TotalCost)
                .Sum() ?? 0m;

            TotalPendingDues = context.Customers.AsNoTracking()
                .Where(c => c.TotalDues > 0m)
                .Select(c => (decimal?)c.TotalDues)
                .Sum() ?? 0m;

            // Khata party balances (Receivables vs Payables)
            var balances = context.KhataEntries.AsNoTracking()
                .GroupBy(entry => entry.AccountId)
                .Select(group => group.Sum(entry => entry.Debit - entry.Credit))
                .ToList();

            decimal receivable = 0m;
            decimal payable = 0m;
            foreach (var balance in balances)
            {
                if (balance > 0) receivable += balance;
                else if (balance < 0) payable += Math.Abs(balance);
            }
            KhataTotalReceivable = receivable;
            KhataTotalPayable = payable;

            // Active Installments Portfolio (Capital in market)
            var activeContracts = context.InstallmentContracts.AsNoTracking()
                .Where(c => c.Status == "Active")
                .Select(c => new { c.TotalPayable, c.DownPayment })
                .ToList();
            ActiveInstallmentPortfolio = activeContracts.Sum(c => Math.Max(0m, c.TotalPayable - c.DownPayment));

            // Overdue Installments
            var overdueSchedules = context.InstallmentSchedules.AsNoTracking()
                .Where(s => s.Status == "Overdue")
                .Select(s => new { s.AmountDue, s.AmountPaid, s.LateFee, s.LateFeePaid, s.LateFeeWaived })
                .ToList();
            OverdueInstallmentCount = overdueSchedules.Count;
            OverdueInstallmentAmount = overdueSchedules.Sum(s => Math.Max(0m, s.AmountDue - s.AmountPaid) + (s.LateFeeWaived ? 0m : Math.Max(0m, s.LateFee - s.LateFeePaid)));

            // FBR Offline Queue
            FbrOfflinePendingCount = context.Sales.AsNoTracking().Count(s => s.FbrStatus == "PendingOffline");
            OnPropertyChanged(nameof(HasFbrOfflinePending));

            // Low Stock (< threshold)
            var lowStock = context.Products.AsNoTracking()
                .Where(p => !p.IsDeleted && p.Quantity <= p.LowStockThreshold)
                .OrderBy(p => p.Quantity)
                .ToList();

            LowStockProducts = new ObservableCollection<Product>(lowStock);
            LowInStockCount = lowStock.Count;
            CriticalAlertCount = lowStock.Count(p => p.Quantity <= 0m);

            var lowest = lowStock.FirstOrDefault();
            LowestUnitInfo = lowest != null
                ? $"{lowest.Name} ({(lowest.Quantity % 1m == 0 ? lowest.Quantity.ToString("N0") : lowest.Quantity.ToString("0.##"))} {lowest.Unit})"
                : "All stock healthy";

            // Sales Trend Chart (last 7 days always clear on dashboard)
            var chartStart = DateTime.Today.AddDays(-6);
            var last7Days = Enumerable.Range(0, 7)
                .Select(i => DateTime.Today.AddDays(-6 + i))
                .ToList();

            var recentSales = context.Sales
                .AsNoTracking()
                .Where(s => s.CreatedAt >= chartStart && s.RecordType == "Sale")
                .Select(s => new { s.CreatedAt, s.TotalAmount })
                .ToList();
            var recentReturns = context.Sales
                .AsNoTracking()
                .Where(s => s.CreatedAt >= chartStart && s.RecordType == "Return")
                .Select(s => new { s.CreatedAt, s.TotalAmount })
                .ToList();

            var dailySales = last7Days.Select(day =>
            {
                decimal daySale = recentSales.Where(s => s.CreatedAt.Date == day.Date).Sum(s => s.TotalAmount);
                decimal dayRet = recentReturns.Where(s => s.CreatedAt.Date == day.Date).Sum(s => s.TotalAmount);
                return (Day: day, Amount: Math.Max(0m, daySale - dayRet));
            }).ToList();
            decimal maxDailySales = dailySales.Max(point => point.Amount);
            SalesTrend = new ObservableCollection<SalesTrendPoint>(dailySales.Select(point =>
                new SalesTrendPoint(
                    point.Day.ToString("ddd dd"),
                    point.Amount,
                    maxDailySales > 0m ? (double)(point.Amount / maxDailySales * 112m) : 2d)));

            // Payment Method Mix (last 30 days)
            var thirtyDaysAgo = DateTime.Today.AddDays(-30);
            var paymentGroups = context.Sales.AsNoTracking()
                .Where(s => s.CreatedAt >= thirtyDaysAgo && s.RecordType == "Sale")
                .GroupBy(s => s.PaymentMethod)
                .Select(g => new { Method = g.Key, Amount = g.Sum(s => s.AmountPaid), Count = g.Count() })
                .OrderByDescending(g => g.Amount)
                .ToList();
            decimal totalPaymentAmount = paymentGroups.Sum(g => g.Amount);
            TotalAmountPaid = totalPaymentAmount;
            PaymentMethodMix = new ObservableCollection<PaymentMethodItem>(paymentGroups.Select(g =>
                new PaymentMethodItem(
                    g.Method,
                    g.Amount,
                    g.Count,
                    totalPaymentAmount > 0m ? Math.Round((double)(g.Amount / totalPaymentAmount * 100m), 2) : 0d)));

            // Department Revenue (last 30 days)
            var saleItemsForDept = context.SaleItems.AsNoTracking()
                .Where(i => i.Sale.CreatedAt >= thirtyDaysAgo && i.Sale.RecordType == "Sale")
                .Select(i => new { Department = i.Product == null ? "Uncategorized" : i.Product.Category, i.Price })
                .ToList();
            var deptGroups = saleItemsForDept
                .GroupBy(i => string.IsNullOrWhiteSpace(i.Department) ? "Uncategorized" : i.Department)
                .Select(g => new { Department = g.Key, Amount = g.Sum(i => i.Price), Count = g.Count() })
                .OrderByDescending(g => g.Amount)
                .ToList();
            decimal totalDeptAmount = deptGroups.Sum(g => g.Amount);
            DepartmentRevenue = new ObservableCollection<DepartmentRevenueItem>(deptGroups.Select(g =>
                new DepartmentRevenueItem(
                    g.Department,
                    g.Amount,
                    g.Count,
                    totalDeptAmount > 0m ? Math.Round((double)(g.Amount / totalDeptAmount * 100m), 2) : 0d)));

            // Monthly Sales Trend (last 6 months)
            var sixMonthsAgo = DateTime.Today.AddMonths(-5);
            var monthlySales = context.Sales
                .AsNoTracking()
                .Where(s => s.CreatedAt >= sixMonthsAgo && s.RecordType == "Sale")
                .Select(s => new { s.CreatedAt, s.TotalAmount })
                .ToList();
            var monthLabels = Enumerable.Range(0, 6)
                .Select(i => DateTime.Today.AddMonths(-i))
                .OrderBy(d => d)
                .Select(d => d.ToString("MMM yyyy"))
                .ToList();
            var monthlyGrouped = monthlySales
                .GroupBy(s => new { s.CreatedAt.Year, s.CreatedAt.Month })
                .Select(g => new { Month = new DateTime(g.Key.Year, g.Key.Month, 1).ToString("MMM yyyy"), Amount = g.Sum(s => s.TotalAmount) })
                .ToList();
            decimal maxMonthlyAmount = monthlyGrouped.Any() ? monthlyGrouped.Max(g => g.Amount) : 0m;
            MonthlyTrend = new ObservableCollection<MonthlyTrendPoint>(monthLabels.Select(label =>
            {
                var data = monthlyGrouped.FirstOrDefault(g => g.Month == label);
                var amount = data?.Amount ?? 0m;
                return new MonthlyTrendPoint(
                    label,
                    amount,
                    maxMonthlyAmount > 0m ? Math.Round((double)(amount / maxMonthlyAmount * 100m), 2) : 0d);
            }));

            // Top Selling Products with Revenue & Progress
            var topItems = context.Products
                .Where(p => !p.IsDeleted && p.SalesCount > 0)
                .OrderByDescending(p => p.SalesCount)
                .Take(5)
                .ToList();

            if (topItems.Count == 0)
            {
                // Fallback to active catalog items if sales count is zero yet
                topItems = context.Products
                    .Where(p => !p.IsDeleted)
                    .OrderByDescending(p => p.SellingPrice)
                    .Take(5)
                    .ToList();
            }

            int maxSalesCount = topItems.Count > 0 ? Math.Max(1, topItems.Max(p => p.SalesCount)) : 1;
            TopSellingProducts = new ObservableCollection<TopProductItem>(topItems.Select(p =>
                new TopProductItem(
                    p.Name,
                    p.Category,
                    p.ImagePath,
                    p.SalesCount,
                    p.SalesCount > 0 ? p.SalesCount * p.SellingPrice : p.SellingPrice,
                    Math.Min(100.0, Math.Max(15.0, (double)p.SalesCount / maxSalesCount * 100.0))
                )));
            LastRefreshedLabel = $"Updated {DateTime.Now:hh:mm tt}";
        }
    }
}
