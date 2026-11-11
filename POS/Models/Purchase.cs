using System;

namespace POS.Models
{
    public class Purchase
    {
        public int Id { get; set; }
        public int ProductId { get; set; }
        public Product Product { get; set; } = null!;
        public string ProductName { get; set; } = string.Empty;
        public string SupplierName { get; set; } = string.Empty;
        public decimal CostPrice { get; set; }
        public decimal Quantity { get; set; }
        public string Unit { get; set; } = "pcs";
        public decimal TotalCost { get; set; }
        public string CreatedBy { get; set; } = string.Empty;
        public DateTime PurchaseDate { get; set; } = DateTime.Now;
    }
}
