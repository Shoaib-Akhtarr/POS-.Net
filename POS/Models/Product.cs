using System;

namespace POS.Models
{
    public class Product
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Barcode { get; set; } = string.Empty;
        public string Brand { get; set; } = string.Empty;
        public decimal CostPrice { get; set; }
        public decimal SellingPrice { get; set; }
        public decimal Quantity { get; set; }
        public string Unit { get; set; } = "pcs";
        public string PackUnitName { get; set; } = string.Empty;
        public decimal UnitsPerPack { get; set; } = 1m;
        public decimal LowStockThreshold { get; set; } = 10m;
        public string DefaultWarranty { get; set; } = string.Empty;
        public string Category { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public int SalesCount { get; set; }
        public bool IsDeleted { get; set; } = false;
        public DateTime CreatedAt { get; set; } = DateTime.Now;
        public DateTime UpdatedAt { get; set; } = DateTime.Now;
    }
}
