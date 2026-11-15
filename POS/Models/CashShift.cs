using System;
using System.ComponentModel.DataAnnotations.Schema;

namespace POS.Models
{
    public sealed class CashShift
    {
        public int Id { get; set; }
        public int CashierId { get; set; }
        public string CashierName { get; set; } = string.Empty;
        public string OpenedAt { get; set; } = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
        public string ClosedAt { get; set; } = string.Empty;
        public decimal OpeningCash { get; set; }
        public decimal ClosingCash { get; set; }
        public decimal ExpectedCash { get; set; }
        public decimal Variance { get; set; }
        public string Status { get; set; } = "Open"; // Open, Closed, Z-Reported
        public string Notes { get; set; } = string.Empty;
        public string CreatedAt { get; set; } = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");

        [NotMapped]
        public bool IsOpen => string.IsNullOrEmpty(ClosedAt);

        public int? ZReportId { get; set; }
    }
}