using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using LiveCharts;
using LiveCharts.Wpf;
using Microsoft.EntityFrameworkCore;
using POS.Models;
using POS.Services;

namespace POS.ViewModels
{
    public partial class HomeViewModel : ObservableObject, IRecipient<DataChangedMessage>
    {
        [ObservableProperty] private decimal _totalSalesRevenue;
        [ObservableProperty] private decimal _weeklyProfit;
        [ObservableProperty] private decimal _weeklyPurchasesSum;
        [ObservableProperty] private decimal _totalPendingDues;
        [ObservableProperty] private int _lowInStockCount;
        [ObservableProperty] private ObservableCollection<Product> _lowStockProducts = new();
        [ObservableProperty] private bool _isLowStockModalOpen;
        [ObservableProperty] private bool _isAlertDismissed;
        [ObservableProperty] private int _criticalAlertCount;
        [ObservableProperty] private string _lowestUnitInfo = "None";

        [ObservableProperty] private string _selectedPeriod = "Last 7 Days";
        public List<string> AvailablePeriods { get; } = new() { "Today", "Last 7 Days", "Last 30 Days", "All Time" };

        [ObservableProperty] private SeriesCollection _salesTrendSeries = new();
        [ObservableProperty] private string[] _trendLabels = Array.Empty<string>();
        public Func<double, string> YFormatter { get; set; }

        public bool IsOwner => SessionService.IsOwner;

        public HomeViewModel()
        {
            YFormatter = value => "Rs. " + value.ToString("N0");
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
        private void ToggleLowStockModal()
        {
            IsLowStockModalOpen = !IsLowStockModalOpen;
            if (IsLowStockModalOpen) IsAlertDismissed = false;
        }

        [RelayCommand]
        private void DismissAlert() => IsAlertDismissed = true;

        [RelayCommand]
        private void RestoreAlert() => IsAlertDismissed = false;

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
                .Include(s => s.SaleItems)
                .Where(s => s.CreatedAt >= startDate && s.RecordType != "Estimate" && s.RecordType != "Payment")
                .ToList();

            var salesList = periodTransactions.Where(s => s.RecordType == "Sale").ToList();
            var returnsList = periodTransactions.Where(s => s.RecordType == "Return").ToList();

            decimal grossSales = salesList.Sum(s => s.TotalAmount);
            decimal grossReturns = returnsList.Sum(s => s.TotalAmount);
            TotalSalesRevenue = Math.Max(0m, grossSales - grossReturns);

            // Load product cost dictionary once to avoid N+1 queries
            var productCosts = context.Products.ToDictionary(p => p.Id, p => p.CostPrice);

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
                netProfit += itemsProfit - sale.Discount;
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

            WeeklyPurchasesSum = context.Purchases
                .Where(p => p.PurchaseDate >= startDate)
                .ToList()
                .Sum(p => p.TotalCost);

            TotalPendingDues = context.Customers
                .ToList()
                .Where(c => c.TotalDues > 0m)
                .Sum(c => c.TotalDues);

            // Low Stock (< threshold)
            var activeProducts = context.Products.Where(p => !p.IsDeleted).ToList();
            var lowStock = activeProducts
                .Where(p => p.Quantity <= p.LowStockThreshold)
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
                .Where(s => s.CreatedAt >= chartStart && s.RecordType == "Sale")
                .ToList();
            var recentReturns = context.Sales
                .Where(s => s.CreatedAt >= chartStart && s.RecordType == "Return")
                .ToList();

            TrendLabels = last7Days.Select(d => d.ToString("ddd dd")).ToArray();
            var salesValues = new ChartValues<decimal>();

            foreach (var day in last7Days)
            {
                decimal daySale = recentSales.Where(s => s.CreatedAt.Date == day.Date).Sum(s => s.TotalAmount);
                decimal dayRet = recentReturns.Where(s => s.CreatedAt.Date == day.Date).Sum(s => s.TotalAmount);
                salesValues.Add(Math.Max(0m, daySale - dayRet));
            }

            SalesTrendSeries = new SeriesCollection
            {
                new LineSeries
                {
                    Title = "Net Sales",
                    Values = salesValues,
                    Stroke = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#0D9488")),
                    Fill = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#1A0D9488")),
                    PointGeometrySize = 8,
                    LineSmoothness = 0.35
                }
            };
        }
    }
}
