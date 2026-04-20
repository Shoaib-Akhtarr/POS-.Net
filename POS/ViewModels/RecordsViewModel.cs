using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using Microsoft.EntityFrameworkCore;
using POS.Models;

namespace POS.ViewModels
{
    public partial class RecordsViewModel : ObservableObject, IRecipient<DataChangedMessage>
    {
        [ObservableProperty]
        private ObservableCollection<UnifiedRecord> _allRecords = new();

        [ObservableProperty]
        private ObservableCollection<UnifiedRecord> _filteredRecords = new();

        [ObservableProperty]
        private string _searchText = string.Empty;

        [ObservableProperty]
        private string _selectedFilter = "All"; // All, Sales, Purchases

        [ObservableProperty]
        private bool _isDetailModalOpen;

        [ObservableProperty]
        private UnifiedRecord? _selectedRecord;

        public RecordsViewModel()
        {
            RefreshData();
            WeakReferenceMessenger.Default.Register(this);
        }

        public void Receive(DataChangedMessage message)
        {
            if (message.Value == "Transaction")
            {
                App.Current.Dispatcher.Invoke(RefreshData);
            }
        }

        [RelayCommand]
        public void RefreshData()
        {
            using var context = new AppDbContext();
            
            var sales = context.Sales
                .Include(s => s.SaleItems)
                .OrderByDescending(s => s.CreatedAt)
                .ToList()
                .Select(s => new UnifiedRecord
                {
                    Id = s.Id,
                    Type = "SALE",
                    RefId = s.ReceiptNumber ?? s.Id.ToString(),
                    Name = s.CustomerName ?? "Walk-in Customer",
                    Amount = s.TotalAmount,
                    Date = s.CreatedAt,
                    Details = s.SaleItems.Select(si => $"{si.Quantity}x {si.Name}").ToList(),
                    OriginalObject = s
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
                    Amount = p.TotalCost,
                    Date = p.PurchaseDate,
                    Details = new List<string> { $"{p.Quantity}x {p.ProductName} (at Rs {p.CostPrice:N0})" },
                    OriginalObject = p
                });

            var combined = sales.Concat(purchases).OrderByDescending(r => r.Date).ToList();
            AllRecords = new ObservableCollection<UnifiedRecord>(combined);
            ApplyFilter();
        }

        partial void OnSearchTextChanged(string value) => ApplyFilter();
        partial void OnSelectedFilterChanged(string value) => ApplyFilter();

        private void ApplyFilter()
        {
            var query = AllRecords.AsEnumerable();

            if (SelectedFilter == "Sales")
                query = query.Where(r => r.Type == "SALE");
            else if (SelectedFilter == "Purchases")
                query = query.Where(r => r.Type == "PURCHASE");

            if (!string.IsNullOrWhiteSpace(SearchText))
            {
                var search = SearchText.ToLower();
                query = query.Where(r => 
                    r.RefId.ToLower().Contains(search) || 
                    r.Name.ToLower().Contains(search));
            }

            FilteredRecords = new ObservableCollection<UnifiedRecord>(query);
        }

        [RelayCommand]
        private void OpenDetail(UnifiedRecord record)
        {
            SelectedRecord = record;
            IsDetailModalOpen = true;
        }

        [RelayCommand]
        private void CloseDetail() => IsDetailModalOpen = false;
    }

    public class UnifiedRecord
    {
        public int Id { get; set; }
        public string Type { get; set; } = string.Empty;
        public string RefId { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public decimal Amount { get; set; }
        public DateTime Date { get; set; }
        public List<string> Details { get; set; } = new();
        public object? OriginalObject { get; set; }
    }
}
