using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using POS.Models;

namespace POS.Services
{
    public class BackupService
    {
        private static readonly string BackupDir = Path.Combine(
            Path.GetDirectoryName(System.Environment.ProcessPath) ?? AppDomain.CurrentDomain.BaseDirectory, 
            "Data_Backups"
        );

        public BackupService()
        {
            if (!Directory.Exists(BackupDir))
            {
                Directory.CreateDirectory(BackupDir);
            }
        }

        public async Task BackupProductsAsync(IEnumerable<Product> products)
        {
            string filePath = Path.Combine(BackupDir, "Products_LastSync.json");
            await SaveToJsonAsync(filePath, products);
        }

        public async Task BackupCustomersAsync(IEnumerable<Customer> customers)
        {
            string filePath = Path.Combine(BackupDir, "Customers_LastSync.json");
            await SaveToJsonAsync(filePath, customers);
        }

        public async Task BackupSaleAsync(Sale sale)
        {
            // Individual sales are backed up separately to avoid huge files and prevent corruption of a single "AllSales" file
            string salesDir = Path.Combine(BackupDir, "Sales_Archived");
            if (!Directory.Exists(salesDir)) Directory.CreateDirectory(salesDir);

            string filename = $"Sale_{sale.Id}_{sale.CreatedAt:yyyyMMdd_HHmmss}.json";
            string filePath = Path.Combine(salesDir, filename);
            await SaveToJsonAsync(filePath, sale);
        }

        private async Task SaveToJsonAsync<T>(string filePath, T data)
        {
            try
            {
                var options = new JsonSerializerOptions 
                { 
                    WriteIndented = true,
                    ReferenceHandler = System.Text.Json.Serialization.ReferenceHandler.IgnoreCycles
                };
                string json = JsonSerializer.Serialize(data, options);
                await File.WriteAllTextAsync(filePath, json);
            }
            catch (Exception ex)
            {
                // In a production app, we would log this. 
                // For now, we fail silently to not interrupt the POS flow.
                System.Diagnostics.Debug.WriteLine($"Backup failed for {filePath}: {ex.Message}");
            }
        }
    }
}
