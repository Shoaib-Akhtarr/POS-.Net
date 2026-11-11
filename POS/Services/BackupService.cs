using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
using POS.Models;

namespace POS.Services
{
    public class BackupService
    {
        private static string BackupDir => Path.Combine(AppDbContext.GetAppDataDirectory(), "Data_Backups");

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
            string salesDir = Path.Combine(BackupDir, "Sales_Archived");
            if (!Directory.Exists(salesDir)) Directory.CreateDirectory(salesDir);

            string filename = $"Sale_{sale.Id}_{sale.CreatedAt:yyyyMMdd_HHmmss}.json";
            string filePath = Path.Combine(salesDir, filename);
            await SaveToJsonAsync(filePath, sale);
        }

        public Task<string> ExportDatabaseBackupAsync(string? destinationPath = null)
        {
            SqliteConnection.ClearAllPools();
            string dbPath = AppDbContext.GetDatabasePath();
            if (!File.Exists(dbPath))
            {
                throw new FileNotFoundException("Database file not found.", dbPath);
            }

            string target = string.IsNullOrWhiteSpace(destinationPath)
                ? Path.Combine(BackupDir, $"pos_backup_{DateTime.Now:yyyyMMdd_HHmmss}.db")
                : destinationPath;

            string? dir = Path.GetDirectoryName(target);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
            {
                Directory.CreateDirectory(dir);
            }

            File.Copy(dbPath, target, overwrite: true);
            return Task.FromResult(target);
        }

        public Task RestoreDatabaseBackupAsync(string sourcePath)
        {
            if (!File.Exists(sourcePath))
            {
                throw new FileNotFoundException("Backup file not found.", sourcePath);
            }

            SqliteConnection.ClearAllPools();
            string dbPath = AppDbContext.GetDatabasePath();
            string safetyCopy = Path.Combine(BackupDir, $"pos_pre_restore_{DateTime.Now:yyyyMMdd_HHmmss}.db");
            if (File.Exists(dbPath))
            {
                File.Copy(dbPath, safetyCopy, overwrite: true);
            }

            File.Copy(sourcePath, dbPath, overwrite: true);
            DatabaseInitializer.Initialize(force: true);
            return Task.CompletedTask;
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
                System.Diagnostics.Debug.WriteLine($"Backup failed for {filePath}: {ex.Message}");
            }
        }
    }
}
