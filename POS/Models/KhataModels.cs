using System;

namespace POS.Models
{
    public sealed class KhataAccount
    {
        public int Id { get; set; }
        public int? CustomerId { get; set; }
        public string PartyType { get; set; } = "Customer";
        public string Name { get; set; } = string.Empty;
        public string Phone { get; set; } = string.Empty;
        public string CNIC { get; set; } = string.Empty;
        public string FatherOrHusbandName { get; set; } = string.Empty;
        public string Address { get; set; } = string.Empty;
        public string WorkAddress { get; set; } = string.Empty;
        public decimal CreditLimit { get; set; }
        public bool IsActive { get; set; } = true;
        public string CreatedBy { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; } = DateTime.Now;
        public DateTime UpdatedAt { get; set; } = DateTime.Now;
    }

    public sealed class KhataEntry
    {
        public long Id { get; set; }
        public int AccountId { get; set; }
        public string VoucherNumber { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string PaymentMode { get; set; } = "Journal";
        public decimal Debit { get; set; }
        public decimal Credit { get; set; }
        public string SourceType { get; set; } = string.Empty;
        public string SourceKey { get; set; } = string.Empty;
        public long? ReversesEntryId { get; set; }
        public string CreatedBy { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; } = DateTime.Now;
    }

    public sealed class PaymentInstrument
    {
        public long Id { get; set; }
        public int AccountId { get; set; }
        public string Direction { get; set; } = "In";
        public decimal Amount { get; set; }
        public string PaymentMode { get; set; } = "PDC";
        public string ChequeNumber { get; set; } = string.Empty;
        public string BankName { get; set; } = string.Empty;
        public DateTime ClearingDate { get; set; }
        public string Status { get; set; } = "Pending";
        public string Notes { get; set; } = string.Empty;
        public long? KhataEntryId { get; set; }
        public int? InstallmentContractId { get; set; }
        public long? InstallmentPaymentId { get; set; }
        public string CreatedBy { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; } = DateTime.Now;
        public DateTime? ModifiedAt { get; set; }
        public string ModifiedReason { get; set; } = string.Empty;
    }

    public sealed class InstallmentContract
    {
        public int Id { get; set; }
        public string ContractNumber { get; set; } = string.Empty;
        public int CustomerId { get; set; }
        public int SaleId { get; set; }
        public decimal CashPrice { get; set; }
        public decimal MarkupAmount { get; set; }
        public decimal ProcessingFee { get; set; }
        public decimal DownPayment { get; set; }
        public decimal TotalPayable { get; set; }
        public string Frequency { get; set; } = "Monthly";
        public int IntervalDays { get; set; } = 30;
        public int InstallmentsCount { get; set; }
        public DateTime FirstDueDate { get; set; }
        public int GraceDays { get; set; } = 3;
        public decimal LateFeePerInstallment { get; set; }
        public string RecoveryOfficer { get; set; } = string.Empty;
        public string Status { get; set; } = "Active";
        public string CreatedBy { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; } = DateTime.Now;
        public bool IsReversed { get; set; }
        public string ReversedReason { get; set; } = string.Empty;
        public DateTime? ModifiedAt { get; set; }
        public string ModifiedReason { get; set; } = string.Empty;
    }

    public sealed class InstallmentContractItem
    {
        public int Id { get; set; }
        public int ContractId { get; set; }
        public int ProductId { get; set; }
        public string ProductName { get; set; } = string.Empty;
        public decimal Quantity { get; set; }
        public string Unit { get; set; } = "pcs";
        public decimal UnitPrice { get; set; }
        public decimal UnitCost { get; set; }
    }

    public sealed record InstallmentContractChoice(int Id, string Label);

    public sealed class InstallmentGuarantor
    {
        public int Id { get; set; }
        public int ContractId { get; set; }
        public string FullName { get; set; } = string.Empty;
        public string CNIC { get; set; } = string.Empty;
        public string Phone { get; set; } = string.Empty;
        public string Address { get; set; } = string.Empty;
        public string Relationship { get; set; } = string.Empty;
    }

    public sealed class CustomerDocument
    {
        public long Id { get; set; }
        public int CustomerId { get; set; }
        public string DocumentType { get; set; } = string.Empty;
        public string FileName { get; set; } = string.Empty;
        public string ContentType { get; set; } = "application/octet-stream";
        public byte[] Content { get; set; } = Array.Empty<byte>();
        public string UploadedBy { get; set; } = string.Empty;
        public DateTime UploadedAt { get; set; } = DateTime.Now;
    }

    public sealed class InstallmentSchedule
    {
        public long Id { get; set; }
        public int ContractId { get; set; }
        public int Sequence { get; set; }
        public DateTime DueDate { get; set; }
        public decimal AmountDue { get; set; }
        public decimal AmountPaid { get; set; }
        public decimal LateFee { get; set; }
        public decimal LateFeePaid { get; set; }
        public bool LateFeeWaived { get; set; }
        public string WaiverReason { get; set; } = string.Empty;
        public string Status { get; set; } = "Due";
    }

    public sealed class InstallmentPayment
    {
        public long Id { get; set; }
        public int ContractId { get; set; }
        public string ReceiptNumber { get; set; } = string.Empty;
        public decimal Amount { get; set; }
        public string PaymentMode { get; set; } = "Cash";
        public string Notes { get; set; } = string.Empty;
        public string CreatedBy { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; } = DateTime.Now;
        public bool IsReversed { get; set; }
        public string ReversedReason { get; set; } = string.Empty;
    }

    public sealed class InstallmentPaymentAllocation
    {
        public long Id { get; set; }
        public long PaymentId { get; set; }
        public long ScheduleId { get; set; }
        public decimal Amount { get; set; }
        public decimal PrincipalAmount { get; set; }
        public decimal LateFeeAmount { get; set; }
    }
}
