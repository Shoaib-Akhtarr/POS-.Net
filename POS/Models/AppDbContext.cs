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

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            string appDir = System.IO.Path.GetDirectoryName(System.Environment.ProcessPath) ?? System.AppDomain.CurrentDomain.BaseDirectory;
            string dbPath = System.IO.Path.Combine(appDir, "pos.db");
            optionsBuilder.UseSqlite($"Data Source={dbPath}");
        }
    }
}
