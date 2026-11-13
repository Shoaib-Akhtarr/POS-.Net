using System;
using System.IO;
using Microsoft.EntityFrameworkCore;

namespace POS.Models
{
    public class AppDbContext : DbContext
    {
        public DbSet<Product> Products { get; set; }
        public DbSet<Customer> Customers { get; set; }
        public DbSet<Sale> Sales { get; set; }
        public DbSet<SaleItem> SaleItems { get; set; }
        public DbSet<Purchase> Purchases { get; set; }
        public DbSet<StoreSettings> StoreSettings { get; set; }
        public DbSet<StaffUser> StaffUsers { get; set; }
        public DbSet<KhataAccount> KhataAccounts { get; set; }
        public DbSet<KhataEntry> KhataEntries { get; set; }
        public DbSet<PaymentInstrument> PaymentInstruments { get; set; }
        public DbSet<InstallmentContract> InstallmentContracts { get; set; }
        public DbSet<InstallmentContractItem> InstallmentContractItems { get; set; }
        public DbSet<InstallmentGuarantor> InstallmentGuarantors { get; set; }
        public DbSet<CustomerDocument> CustomerDocuments { get; set; }
        public DbSet<InstallmentSchedule> InstallmentSchedules { get; set; }
        public DbSet<InstallmentPayment> InstallmentPayments { get; set; }
        public DbSet<InstallmentPaymentAllocation> InstallmentPaymentAllocations { get; set; }

        public static string GetAppDataDirectory()
        {
            string localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            if (string.IsNullOrWhiteSpace(localAppData))
            {
                localAppData = Path.GetDirectoryName(Environment.ProcessPath) ?? AppDomain.CurrentDomain.BaseDirectory;
            }

            string dataDir = Path.Combine(localAppData, "KarobarPOS");
            if (!Directory.Exists(dataDir))
            {
                Directory.CreateDirectory(dataDir);
            }
            return dataDir;
        }

        public static string GetLegacyAppDirectory()
        {
            return Path.GetDirectoryName(Environment.ProcessPath) ?? AppDomain.CurrentDomain.BaseDirectory;
        }

        public static string GetDatabasePath()
        {
            return Path.Combine(GetAppDataDirectory(), "pos.db");
        }

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            optionsBuilder.UseSqlite($"Data Source={GetDatabasePath()}");
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<KhataAccount>().HasIndex(a => a.CustomerId).IsUnique();
            modelBuilder.Entity<KhataEntry>().HasIndex(e => new { e.SourceType, e.SourceKey }).IsUnique();
            modelBuilder.Entity<KhataEntry>().HasIndex(e => new { e.AccountId, e.CreatedAt, e.Id });
            modelBuilder.Entity<InstallmentContract>().HasIndex(c => c.ContractNumber).IsUnique();
            modelBuilder.Entity<InstallmentSchedule>().HasIndex(s => new { s.ContractId, s.Sequence }).IsUnique();
            modelBuilder.Entity<InstallmentPayment>().HasIndex(p => p.ReceiptNumber).IsUnique();

            modelBuilder.Entity<KhataEntry>().HasOne<KhataAccount>().WithMany().HasForeignKey(e => e.AccountId).OnDelete(DeleteBehavior.Restrict);
            modelBuilder.Entity<PaymentInstrument>().HasOne<KhataAccount>().WithMany().HasForeignKey(p => p.AccountId).OnDelete(DeleteBehavior.Restrict);
            modelBuilder.Entity<InstallmentContract>().HasOne<Customer>().WithMany().HasForeignKey(c => c.CustomerId).OnDelete(DeleteBehavior.Restrict);
            modelBuilder.Entity<InstallmentContract>().HasOne<Sale>().WithMany().HasForeignKey(c => c.SaleId).OnDelete(DeleteBehavior.Restrict);
            modelBuilder.Entity<InstallmentContractItem>().HasOne<InstallmentContract>().WithMany().HasForeignKey(i => i.ContractId).OnDelete(DeleteBehavior.Cascade);
            modelBuilder.Entity<InstallmentContractItem>().HasOne<Product>().WithMany().HasForeignKey(i => i.ProductId).OnDelete(DeleteBehavior.Restrict);
            modelBuilder.Entity<InstallmentGuarantor>().HasOne<InstallmentContract>().WithMany().HasForeignKey(g => g.ContractId).OnDelete(DeleteBehavior.Cascade);
            modelBuilder.Entity<CustomerDocument>().HasOne<Customer>().WithMany().HasForeignKey(d => d.CustomerId).OnDelete(DeleteBehavior.Cascade);
            modelBuilder.Entity<InstallmentSchedule>().HasOne<InstallmentContract>().WithMany().HasForeignKey(s => s.ContractId).OnDelete(DeleteBehavior.Cascade);
            modelBuilder.Entity<InstallmentPayment>().HasOne<InstallmentContract>().WithMany().HasForeignKey(p => p.ContractId).OnDelete(DeleteBehavior.Restrict);
            modelBuilder.Entity<InstallmentPaymentAllocation>().HasOne<InstallmentPayment>().WithMany().HasForeignKey(a => a.PaymentId).OnDelete(DeleteBehavior.Cascade);
            modelBuilder.Entity<InstallmentPaymentAllocation>().HasOne<InstallmentSchedule>().WithMany().HasForeignKey(a => a.ScheduleId).OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<KhataAccount>().Property(a => a.CreditLimit).HasPrecision(18, 2);
            modelBuilder.Entity<KhataEntry>().Property(e => e.Debit).HasPrecision(18, 2);
            modelBuilder.Entity<KhataEntry>().Property(e => e.Credit).HasPrecision(18, 2);
            modelBuilder.Entity<PaymentInstrument>().Property(p => p.Amount).HasPrecision(18, 2);
            modelBuilder.Entity<InstallmentContract>().Property(c => c.CashPrice).HasPrecision(18, 2);
            modelBuilder.Entity<InstallmentContract>().Property(c => c.MarkupAmount).HasPrecision(18, 2);
            modelBuilder.Entity<InstallmentContract>().Property(c => c.ProcessingFee).HasPrecision(18, 2);
            modelBuilder.Entity<InstallmentContract>().Property(c => c.DownPayment).HasPrecision(18, 2);
            modelBuilder.Entity<InstallmentContract>().Property(c => c.TotalPayable).HasPrecision(18, 2);
            modelBuilder.Entity<InstallmentContract>().Property(c => c.LateFeePerInstallment).HasPrecision(18, 2);
            modelBuilder.Entity<InstallmentContractItem>().Property(i => i.Quantity).HasPrecision(18, 2);
            modelBuilder.Entity<InstallmentContractItem>().Property(i => i.UnitPrice).HasPrecision(18, 2);
            modelBuilder.Entity<InstallmentContractItem>().Property(i => i.UnitCost).HasPrecision(18, 2);
            modelBuilder.Entity<InstallmentSchedule>().Property(s => s.AmountDue).HasPrecision(18, 2);
            modelBuilder.Entity<InstallmentSchedule>().Property(s => s.AmountPaid).HasPrecision(18, 2);
            modelBuilder.Entity<InstallmentSchedule>().Property(s => s.LateFee).HasPrecision(18, 2);
            modelBuilder.Entity<InstallmentSchedule>().Property(s => s.LateFeePaid).HasPrecision(18, 2);
            modelBuilder.Entity<InstallmentPayment>().Property(p => p.Amount).HasPrecision(18, 2);
            modelBuilder.Entity<InstallmentPaymentAllocation>().Property(a => a.Amount).HasPrecision(18, 2);
            modelBuilder.Entity<InstallmentPaymentAllocation>().Property(a => a.PrincipalAmount).HasPrecision(18, 2);
            modelBuilder.Entity<InstallmentPaymentAllocation>().Property(a => a.LateFeeAmount).HasPrecision(18, 2);
        }
    }
}
