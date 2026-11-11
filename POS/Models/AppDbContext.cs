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
    }
}
