using System;

namespace POS.Models
{
    public class Purchase
    {
        public int Id { get; set; }
        public int ProductId { get; set; }
        public Product Product { get; set; }
        public string ProductName { get; set; } = string.Empty;
        public string SupplierName { get; set; } = string.Empty;
        public decimal CostPrice { get; set; }
        public int Quantity { get; set; }
        public decimal TotalCost { get; set; }
        public DateTime PurchaseDate { get; set; } = DateTime.Now;
    }
}
