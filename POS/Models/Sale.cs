using System;
using System.Collections.Generic;

namespace POS.Models
{
    public class Sale
    {
        public int Id { get; set; }
        public string RecordType { get; set; } = "Sale"; // Sale, Estimate, Return, Payment
        public decimal TotalAmount { get; set; }
        public decimal Discount { get; set; }
        public string? CustomerName { get; set; }
        public int? CustomerId { get; set; }
        public Customer? Customer { get; set; }
        public decimal PreviousDues { get; set; }
        public decimal AmountPaid { get; set; }
        public decimal BalanceDue { get; set; }
        public decimal CashTendered { get; set; }
        public decimal ChangeReturned { get; set; }
        public string PaymentMethod { get; set; } = "Cash"; // Cash, Credit, Bank, JazzCash, EasyPaisa
        public bool IsPaid { get; set; } = true;
        public string ReceiptId { get; set; } = Guid.NewGuid().ToString();
        public bool Printed { get; set; } = false;
        public string? ReceiptNumber { get; set; }
        public string Notes { get; set; } = string.Empty;
        public int? OriginalSaleId { get; set; }
        public string CreatedBy { get; set; } = string.Empty;

        // FBR / Fiscal Tax Compliance Fields
        public string? FbrInvoiceNumber { get; set; }
        public string FbrStatus { get; set; } = "NotIntegrated"; // NotIntegrated, Synced, PendingOffline, Failed, Exempt
        public decimal FbrTaxRate { get; set; } = 0m;
        public decimal FbrTaxAmount { get; set; } = 0m;
        public string? FbrQrCodeData { get; set; }
        public DateTime? FbrSyncTimestamp { get; set; }
        public string? FbrErrorMessage { get; set; }
        public string? BuyerCnic { get; set; }
        public string? BuyerNtn { get; set; }

        public ICollection<SaleItem> SaleItems { get; set; } = new List<SaleItem>();

        public DateTime CreatedAt { get; set; } = DateTime.Now;
        public DateTime UpdatedAt { get; set; } = DateTime.Now;
    }
}
