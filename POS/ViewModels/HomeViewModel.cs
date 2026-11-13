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

        [ObservableProperty] private ObservableCollection<SalesTrendPoint> _salesTrend = new();

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
            var installmentTerms = context.InstallmentContracts.AsNoTracking()
                .Where(contract => salesList.Select(sale => sale.Id).Contains(contract.SaleId))
                .ToDictionary(contract => contract.SaleId, contract => contract.MarkupAmount + contract.ProcessingFee);

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
        }
    }
}
