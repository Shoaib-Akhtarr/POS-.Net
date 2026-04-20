using System;
using System.Collections.Generic;

namespace POS.Models
{
    public class Sale
    {
        public int Id { get; set; }
        public decimal TotalAmount { get; set; }
        public decimal Discount { get; set; }
        public string? CustomerName { get; set; }
        public int? CustomerId { get; set; }
        public Customer? Customer { get; set; }
        public decimal PreviousDues { get; set; }
        public decimal AmountPaid { get; set; }
        public decimal BalanceDue { get; set; }
        public string PaymentMethod { get; set; } = "Cash"; // Cash or Credit
        public bool IsPaid { get; set; } = true;
        public string ReceiptId { get; set; } = Guid.NewGuid().ToString();
        public bool Printed { get; set; } = false;
        public string? ReceiptNumber { get; set; }
        
        public ICollection<SaleItem> SaleItems { get; set; } = new List<SaleItem>();
        
        public DateTime CreatedAt { get; set; } = DateTime.Now;
        public DateTime UpdatedAt { get; set; } = DateTime.Now;
    }
}
