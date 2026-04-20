using System;
using System.Linq;
using System.Windows.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using LiveCharts;
using LiveCharts.Wpf;
using POS.Models;

namespace POS.ViewModels
{
    public partial class HomeViewModel : ObservableObject
    {
        [ObservableProperty]
        private decimal _totalSalesRevenue;

        [ObservableProperty]
        private decimal _weeklyProfit;

        [ObservableProperty]
        private decimal _weeklyPurchasesSum;

        [ObservableProperty]
        private int _lowInStockCount;

        [ObservableProperty]
        private System.Collections.ObjectModel.ObservableCollection<Product> _lowStockProducts = new();

        [ObservableProperty]
        private bool _isLowStockModalOpen;

        [ObservableProperty]
        private bool _isAlertDismissed;

        [ObservableProperty]
        private int _criticalAlertCount;

        [ObservableProperty]
        private string _lowestUnitInfo = "None";

        [ObservableProperty]
        private SeriesCollection _salesTrendSeries;

        [ObservableProperty]
        private string[] _trendLabels;

        public Func<double, string> YFormatter { get; set; }

        public HomeViewModel()
        {
            SalesTrendSeries = new SeriesCollection();
            TrendLabels = new string[] { };
            YFormatter = value => "Rs. " + value.ToString("N0");
            
            RefreshData();
        }

        [CommunityToolkit.Mvvm.Input.RelayCommand]
        private void ToggleLowStockModal() 
        {
            IsLowStockModalOpen = !IsLowStockModalOpen;
            if (IsLowStockModalOpen) IsAlertDismissed = false; // Reset when opening fresh
        }

        [CommunityToolkit.Mvvm.Input.RelayCommand]
        private void DismissAlert() => IsAlertDismissed = true;

        [CommunityToolkit.Mvvm.Input.RelayCommand]
        private void RestoreAlert() => IsAlertDismissed = false;

        public void RefreshData()
        {
            using (var context = new AppDbContext())
            {
                var startDate = DateTime.Today.AddDays(-7);

                // 1. Total Sales Revenue (Overall)
                TotalSalesRevenue = context.Sales.Any() ? context.Sales.Sum(s => s.TotalAmount) : 0;

                // 2. Weekly Profit Logic
                // New Profit = Σ (SaleItems.Price - (Product.CostPrice * SaleItems.Quantity)) for last 7 days
                var weeklySaleItems = context.SaleItems
                    .Where(si => context.Sales.Any(s => s.Id == si.SaleId && s.CreatedAt >= startDate))
                    .ToList();
                
                decimal profit = 0;
                foreach(var item in weeklySaleItems)
                {
                    var product = context.Products.FirstOrDefault(p => p.Id == item.ProductId);
                    if (product != null)
                    {
                        profit += (item.Price - (product.CostPrice * item.Quantity));
                    }
                }
                WeeklyProfit = profit;

                // 3. Weekly Purchases Logic
                WeeklyPurchasesSum = context.Purchases
                    .Where(p => p.PurchaseDate >= startDate)
                    .Any() ? context.Purchases.Where(p => p.PurchaseDate >= startDate).Sum(p => p.TotalCost) : 0;

                // 4. Low Stock List (< 10)
                var lowStock = context.Products.Where(p => p.Quantity < 20 && !p.IsDeleted).ToList();
                LowStockProducts = new System.Collections.ObjectModel.ObservableCollection<Product>(lowStock);
                LowInStockCount = lowStock.Count;
                CriticalAlertCount = lowStock.Count(p => p.Quantity < 10);

                var lowest = lowStock.OrderBy(p => p.Quantity).FirstOrDefault();
                LowestUnitInfo = lowest != null ? $"{lowest.Name} ({lowest.Quantity})" : "All units high";

                // Sales Trend Chart (Real data for the last 7 days)
                var last7Days = Enumerable.Range(0, 7)
                    .Select(i => DateTime.Today.AddDays(-i))
                    .Reverse()
                    .ToList();
                
                TrendLabels = last7Days.Select(d => d.ToString("ddd")).ToArray();
                var salesValues = new ChartValues<decimal>();
                
                var recentSales = context.Sales.Where(s => s.CreatedAt >= startDate).ToList();

                foreach(var day in last7Days)
                {
                    var daySale = recentSales
                        .Where(s => s.CreatedAt.Date == day.Date)
                        .Sum(s => s.TotalAmount);
                    
                    salesValues.Add(daySale);
                }

                SalesTrendSeries.Clear();
                SalesTrendSeries.Add(new LineSeries
                {
                    Title = "Sales Revenue",
                    Values = salesValues,
                    Stroke = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#10A574")), // Teal theme
                    Fill = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#1A10A574")), 
                    PointGeometrySize = 8,
                    LineSmoothness = 0.4
                });
            }
        }
    }
}
