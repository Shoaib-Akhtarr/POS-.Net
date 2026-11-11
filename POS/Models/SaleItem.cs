using System.ComponentModel.DataAnnotations.Schema;

namespace POS.Models
{
    public class SaleItem
    {
        public int Id { get; set; }
        public int SaleId { get; set; }
        public Sale Sale { get; set; } = null!;
        public string Name { get; set; } = string.Empty;
        public decimal Quantity { get; set; }
        public decimal UnitPrice { get; set; }
        public decimal UnitCost { get; set; }
        public decimal Discount { get; set; }
        public decimal Price { get; set; } // Line total for backward compatibility
        public string Unit { get; set; } = "pcs";
        public string? SerialNumber { get; set; }
        public string? WarrantyPeriod { get; set; }

        public int? ProductId { get; set; }
        public Product? Product { get; set; }

        [NotMapped]
        public decimal Subtotal => Price;

        [NotMapped]
        public decimal EffectiveUnitPrice => UnitPrice > 0 ? UnitPrice : (Quantity > 0 ? (Price + Discount) / Quantity : Price);
    }
}
