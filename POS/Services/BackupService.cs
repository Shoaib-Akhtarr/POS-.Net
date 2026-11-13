using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using POS.Models;

namespace POS.Services
{
    public sealed class BackupService
    {
        private const string DatabaseEntryName = "pos.db";
        private const string ManifestEntryName = "manifest.json";
        private const int BackupFormatVersion = 1;
        private static string BackupDir => Path.Combine(AppDbContext.GetAppDataDirectory(), "Data_Backups");

        public BackupService() => Directory.CreateDirectory(BackupDir);

        public Task<string> ExportDatabaseBackupAsync(string? destinationPath = null)
        {
            string target = string.IsNullOrWhiteSpace(destinationPath)
                ? Path.Combine(BackupDir, $"KarobarPOS_{DateTime.Now:yyyyMMdd_HHmmss}.kposbackup")
                : destinationPath;

            ExportPortableBackup(target);
            return Task.FromResult(target);
        }

        public static void ExportPortableBackup(string destinationPath)
        {
            string sourcePath = AppDbContext.GetDatabasePath();
            if (!File.Exists(sourcePath))
                throw new FileNotFoundException("The POS database was not found.", sourcePath);

            string fullTarget = Path.GetFullPath(destinationPath);
            if (string.Equals(fullTarget, Path.GetFullPath(sourcePath), StringComparison.OrdinalIgnoreCase))
                throw new IOException("Backup destination cannot be the active POS database.");
            string? targetDirectory = Path.GetDirectoryName(fullTarget);
            if (string.IsNullOrWhiteSpace(targetDirectory))
                throw new IOException("Choose a valid backup folder.");

            Directory.CreateDirectory(targetDirectory);
            string workDirectory = Path.Combine(targetDirectory, $".karobar-backup-{Guid.NewGuid():N}");
            Directory.CreateDirectory(workDirectory);
            string snapshotPath = Path.Combine(workDirectory, DatabaseEntryName);
            string packagePath = Path.Combine(workDirectory, "backup.kposbackup");

            try
            {
                CreateSqliteSnapshot(sourcePath, snapshotPath);
                ValidateSqliteDatabase(snapshotPath);

                var manifest = new BackupManifest(
                    BackupFormatVersion,
                    DateTime.UtcNow,
                    new FileInfo(snapshotPath).Length,
                    HashFile(snapshotPath));

                using (var archive = ZipFile.Open(packagePath, ZipArchiveMode.Create))
                {
                    archive.CreateEntryFromFile(snapshotPath, DatabaseEntryName, CompressionLevel.Optimal);
                    var manifestEntry = archive.CreateEntry(ManifestEntryName, CompressionLevel.Optimal);
                    using var manifestStream = manifestEntry.Open();
                    JsonSerializer.Serialize(manifestStream, manifest);
                }

                File.Move(packagePath, fullTarget, overwrite: true);

                // Export human-readable spreadsheets (.csv) and auto-prune backups older than 30 days
                ExportSpreadsheets(targetDirectory);
                PruneOldBackups(targetDirectory, retentionDays: 30);
                if (!string.Equals(Path.GetFullPath(targetDirectory), Path.GetFullPath(BackupDir), StringComparison.OrdinalIgnoreCase))
                {
                    ExportSpreadsheets(BackupDir);
                    PruneOldBackups(BackupDir, retentionDays: 30);
                }
            }
            finally
            {
                try { Directory.Delete(workDirectory, recursive: true); }
                catch (IOException) { }
                catch (UnauthorizedAccessException) { }
            }
        }

        public static void RestorePortableBackup(string sourcePath)
        {
            if (!File.Exists(sourcePath))
                throw new FileNotFoundException("Backup file not found.", sourcePath);

            string dataDirectory = AppDbContext.GetAppDataDirectory();
            string livePath = AppDbContext.GetDatabasePath();
            string workDirectory = Path.Combine(dataDirectory, $".karobar-restore-{Guid.NewGuid():N}");
            Directory.CreateDirectory(workDirectory);
            string stagedPath = Path.Combine(workDirectory, DatabaseEntryName);
            string safetyPath = Path.Combine(BackupDir, $"KarobarPOS_before_restore_{DateTime.Now:yyyyMMdd_HHmmss}.db");
            bool movedCurrentDatabase = false;

            try
            {
                if (string.Equals(Path.GetExtension(sourcePath), ".kposbackup", StringComparison.OrdinalIgnoreCase))
                    ExtractAndValidatePackage(sourcePath, stagedPath);
                else if (string.Equals(Path.GetExtension(sourcePath), ".db", StringComparison.OrdinalIgnoreCase))
                {
                    File.Copy(sourcePath, stagedPath, overwrite: true);
                    ValidateSqliteDatabase(stagedPath);
                }
                else
                {
                    throw new InvalidDataException("Choose a Karobar POS backup (.kposbackup) or a legacy SQLite database (.db).");
                }

                SqliteConnection.ClearAllPools();
                DeleteSqliteSidecars(livePath);
                if (File.Exists(livePath))
                {
                    CreateSqliteSnapshot(livePath, safetyPath);
                    File.Move(livePath, Path.Combine(workDirectory, "previous.db"));
                    movedCurrentDatabase = true;
                }

                try
                {
                    File.Move(stagedPath, livePath);
                    DatabaseInitializer.Initialize(force: true);
                }
                catch
                {
                    if (movedCurrentDatabase)
                    {
                        SqliteConnection.ClearAllPools();
                        if (File.Exists(livePath)) File.Delete(livePath);
                        DeleteSqliteSidecars(livePath);
                        File.Move(Path.Combine(workDirectory, "previous.db"), livePath);
                        DatabaseInitializer.Initialize(force: true);
                    }
                    throw;
                }
            }
            finally
            {
                try { Directory.Delete(workDirectory, recursive: true); }
                catch (IOException) { }
                catch (UnauthorizedAccessException) { }
            }
        }

        public void RunAutoBackupOnExit()
        {
            try
            {
                string folder = GetAutoBackupFolder();
                if (!string.IsNullOrWhiteSpace(folder) && Directory.Exists(folder))
                {
                    string path = Path.Combine(folder, $"KarobarPOS_{DateTime.Now:yyyyMMdd}.kposbackup");
                    ExportPortableBackup(path);
                }
                else
                {
                    ExportSpreadsheets(BackupDir);
                    PruneOldBackups(BackupDir, retentionDays: 30);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[AutoBackup] Failed on exit: {ex.Message}");
            }
        }

        public void RunPeriodicBackup()
        {
            try
            {
                string folder = GetAutoBackupFolder();
                if (!string.IsNullOrWhiteSpace(folder) && Directory.Exists(folder))
                {
                    string path = Path.Combine(folder, $"KarobarPOS_{DateTime.Now:yyyyMMdd}.kposbackup");
                    ExportPortableBackup(path);
                }
                else
                {
                    ExportSpreadsheets(BackupDir);
                    PruneOldBackups(BackupDir, retentionDays: 30);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[PeriodicBackup] Failed: {ex.Message}");
            }
        }

        public static string GetAutoBackupFolder()
        {
            try
            {
                using var context = new AppDbContext();
                return context.StoreSettings.FirstOrDefault()?.AutoBackupFolder ?? string.Empty;
            }
            catch
            {
                return string.Empty;
            }
        }

        public Task RestoreDatabaseBackupAsync(string sourcePath)
        {
            RestorePortableBackup(sourcePath);
            return Task.CompletedTask;
        }

        public async Task BackupProductsAsync(IEnumerable<Product> products) =>
            await SaveToJsonAsync(Path.Combine(BackupDir, "Products_LastSync.json"), products);

        public async Task BackupCustomersAsync(IEnumerable<Customer> customers) =>
            await SaveToJsonAsync(Path.Combine(BackupDir, "Customers_LastSync.json"), customers);

        public async Task BackupSaleAsync(Sale sale)
        {
            string directory = Path.Combine(BackupDir, "Sales_Archived");
            Directory.CreateDirectory(directory);
            await SaveToJsonAsync(Path.Combine(directory, $"Sale_{sale.Id}_{sale.CreatedAt:yyyyMMdd_HHmmss}.json"), sale);
        }

        private static void CreateSqliteSnapshot(string sourcePath, string destinationPath)
        {
            using var source = new SqliteConnection(new SqliteConnectionStringBuilder
            {
                DataSource = sourcePath,
                Mode = SqliteOpenMode.ReadOnly,
                Pooling = false
            }.ToString());
            using var destination = new SqliteConnection(new SqliteConnectionStringBuilder
            {
                DataSource = destinationPath,
                Pooling = false
            }.ToString());
            source.Open();
            destination.Open();
            source.BackupDatabase(destination);
            source.Close();
            destination.Close();
            SqliteConnection.ClearAllPools();
        }

        private static void DeleteSqliteSidecars(string databasePath)
        {
            foreach (string suffix in new[] { "-wal", "-shm" })
            {
                string sidecarPath = databasePath + suffix;
                if (File.Exists(sidecarPath)) File.Delete(sidecarPath);
            }
        }

        private static void ExtractAndValidatePackage(string packagePath, string stagedPath)
        {
            using var archive = ZipFile.OpenRead(packagePath);
            var manifestEntry = archive.GetEntry(ManifestEntryName)
                ?? throw new InvalidDataException("This backup is missing its manifest.");
            var databaseEntry = archive.GetEntry(DatabaseEntryName)
                ?? throw new InvalidDataException("This backup is missing its database.");

            BackupManifest? manifest;
            using (var stream = manifestEntry.Open())
                manifest = JsonSerializer.Deserialize<BackupManifest>(stream);
            if (manifest == null || manifest.FormatVersion != BackupFormatVersion)
                throw new InvalidDataException("This backup format is not supported by this version of Karobar POS.");
            if (databaseEntry.Length != manifest.DatabaseLength)
                throw new InvalidDataException("The backup database size does not match its manifest.");

            using (var input = databaseEntry.Open())
            using (var output = File.Create(stagedPath))
                input.CopyTo(output);

            string hash = HashFile(stagedPath);
            if (!CryptographicOperations.FixedTimeEquals(Convert.FromHexString(hash), Convert.FromHexString(manifest.Sha256)))
                throw new InvalidDataException("The backup database failed its integrity check.");

            ValidateSqliteDatabase(stagedPath);
        }

        private static void ValidateSqliteDatabase(string path)
        {
            if (!File.Exists(path) || new FileInfo(path).Length == 0)
                throw new InvalidDataException("The backup database is empty.");

            using var connection = new SqliteConnection(new SqliteConnectionStringBuilder
            {
                DataSource = path,
                Mode = SqliteOpenMode.ReadOnly,
                Pooling = false
            }.ToString());
            connection.Open();
            using var command = connection.CreateCommand();
            command.CommandText = "PRAGMA integrity_check;";
            if (!string.Equals(command.ExecuteScalar()?.ToString(), "ok", StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException("SQLite reported that this database backup is corrupt.");

            command.CommandText = "SELECT COUNT(*) FROM sqlite_master WHERE type = 'table' AND name IN ('Products', 'Customers', 'Sales', 'SaleItems', 'StoreSettings');";
            if (Convert.ToInt32(command.ExecuteScalar()) != 5)
                throw new InvalidDataException("This file does not contain a Karobar POS database.");

            connection.Close();
            SqliteConnection.ClearAllPools();
        }

        private static string HashFile(string path)
        {
            using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            return Convert.ToHexString(SHA256.HashData(stream));
        }

        private static string EscapeCsv(string? value)
        {
            if (string.IsNullOrEmpty(value)) return "";
            bool needsQuotes = value.Contains(',') || value.Contains('"') || value.Contains('\n') || value.Contains('\r');
            if (needsQuotes)
            {
                return $"\"{value.Replace("\"", "\"\"")}\"";
            }
            return value;
        }

        public static void ExportSpreadsheets(string targetDirectory)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(targetDirectory))
                    return;

                Directory.CreateDirectory(targetDirectory);
                using var context = new AppDbContext();

                // 1. Products Stock CSV
                var products = context.Products
                    .AsNoTracking()
                    .Where(p => !p.IsDeleted)
                    .OrderBy(p => p.Category)
                    .ThenBy(p => p.Name)
                    .ToList();

                var productsSb = new StringBuilder();
                productsSb.AppendLine("Barcode,Product Name,Category,Brand,Current Stock,Unit,Cost Price,Sale Price,Total Cost Value,Total Retail Value,Low Stock Alert");

                foreach (var p in products)
                {
                    decimal costVal = Math.Round(p.Quantity * p.CostPrice, 2);
                    decimal retailVal = Math.Round(p.Quantity * p.SellingPrice, 2);
                    string lowAlert = p.Quantity <= p.LowStockThreshold ? "LOW" : "OK";

                    productsSb.AppendLine(string.Join(",",
                        EscapeCsv(p.Barcode),
                        EscapeCsv(p.Name),
                        EscapeCsv(p.Category),
                        EscapeCsv(p.Brand),
                        p.Quantity.ToString("0.##"),
                        EscapeCsv(p.Unit),
                        p.CostPrice.ToString("0.##"),
                        p.SellingPrice.ToString("0.##"),
                        costVal.ToString("0.##"),
                        retailVal.ToString("0.##"),
                        lowAlert
                    ));
                }

                string productsFile = Path.Combine(targetDirectory, "Latest_Products_Stock.csv");
                File.WriteAllText(productsFile, productsSb.ToString(), new UTF8Encoding(true));

                // 2. Khata Dues & Customers CSV
                var khataSb = new StringBuilder();
                khataSb.AppendLine("Party Type,Party Name,Phone,Address,CNIC,Credit Limit,Current Balance (Receivable/Payable),Status");

                var khataAccounts = context.KhataAccounts
                    .AsNoTracking()
                    .Where(a => a.IsActive)
                    .OrderBy(a => a.PartyType)
                    .ThenBy(a => a.Name)
                    .ToList();

                var existingCustomerIds = khataAccounts
                    .Where(a => a.CustomerId.HasValue)
                    .Select(a => a.CustomerId!.Value)
                    .ToHashSet();

                foreach (var acc in khataAccounts)
                {
                    decimal ledgerBalance = KhataService.GetBalance(context, acc.Id);
                    string status = ledgerBalance > 0 ? "Receivable" : (ledgerBalance < 0 ? "Payable" : "Clear");

                    khataSb.AppendLine(string.Join(",",
                        EscapeCsv(acc.PartyType),
                        EscapeCsv(acc.Name),
                        EscapeCsv(acc.Phone),
                        EscapeCsv(acc.Address),
                        EscapeCsv(acc.CNIC),
                        acc.CreditLimit.ToString("0.##"),
                        ledgerBalance.ToString("0.##"),
                        status
                    ));
                }

                foreach (var c in context.Customers.AsNoTracking().Where(c => !existingCustomerIds.Contains(c.Id)).OrderBy(c => c.Name).ToList())
                {
                    string status = c.TotalDues > 0 ? "Receivable" : (c.TotalDues < 0 ? "Payable" : "Clear");
                    khataSb.AppendLine(string.Join(",",
                        "Customer",
                        EscapeCsv(c.Name),
                        EscapeCsv(c.Phone),
                        EscapeCsv(c.Address),
                        "",
                        c.CreditLimit.ToString("0.##"),
                        c.TotalDues.ToString("0.##"),
                        status
                    ));
                }

                string khataFile = Path.Combine(targetDirectory, "Latest_Khata_Dues.csv");
                File.WriteAllText(khataFile, khataSb.ToString(), new UTF8Encoding(true));

                // 3. Recent Sales Summary CSV
                var cutoff = DateTime.Now.AddDays(-30);
                var recentSales = context.Sales
                    .AsNoTracking()
                    .Include(s => s.SaleItems)
                    .Where(s => s.CreatedAt >= cutoff)
                    .OrderByDescending(s => s.CreatedAt)
                    .Take(500)
                    .ToList();

                var salesSb = new StringBuilder();
                salesSb.AppendLine("Receipt #,Date Time,Record Type,Customer Name,Items Count,Subtotal,Discount,Final Amount,Amount Paid,Balance Due,Payment Method,Cashier");

                foreach (var s in recentSales)
                {
                    int itemCount = s.SaleItems?.Count ?? 0;
                    decimal subtotal = s.TotalAmount + s.Discount;
                    salesSb.AppendLine(string.Join(",",
                        EscapeCsv(s.ReceiptNumber ?? s.Id.ToString()),
                        s.CreatedAt.ToString("yyyy-MM-dd HH:mm:ss"),
                        EscapeCsv(s.RecordType),
                        EscapeCsv(s.CustomerName ?? "Walk-in"),
                        itemCount,
                        subtotal.ToString("0.##"),
                        s.Discount.ToString("0.##"),
                        s.TotalAmount.ToString("0.##"),
                        s.AmountPaid.ToString("0.##"),
                        s.BalanceDue.ToString("0.##"),
                        EscapeCsv(s.PaymentMethod),
                        EscapeCsv(s.CreatedBy)
                    ));
                }

                string salesFile = Path.Combine(targetDirectory, "Latest_Sales_Summary.csv");
                File.WriteAllText(salesFile, salesSb.ToString(), new UTF8Encoding(true));
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[CSV Export] Failed: {ex.Message}");
            }
        }

        public static void PruneOldBackups(string targetDirectory, int retentionDays = 30)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(targetDirectory) || !Directory.Exists(targetDirectory))
                    return;

                var dirInfo = new DirectoryInfo(targetDirectory);
                var backupFiles = dirInfo.GetFiles("KarobarPOS_*.kposbackup")
                    .Concat(dirInfo.GetFiles("KarobarPOS_*.db"))
                    .OrderByDescending(f => f.LastWriteTime)
                    .ToList();

                // Failsafe: Always preserve at least the 5 newest backups regardless of age
                const int minPreservedBackups = 5;
                if (backupFiles.Count <= minPreservedBackups)
                    return;

                var threshold = DateTime.Now.AddDays(-retentionDays);

                var eligibleForDeletion = backupFiles
                    .Skip(minPreservedBackups)
                    .Where(f => f.LastWriteTime < threshold)
                    .ToList();

                foreach (var file in eligibleForDeletion)
                {
                    try
                    {
                        file.Delete();
                        System.Diagnostics.Debug.WriteLine($"[Prune] Deleted old backup: {file.Name}");
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"[Prune] Could not delete {file.Name}: {ex.Message}");
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[Prune] Cleanup failed: {ex.Message}");
            }
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

        private sealed record BackupManifest(int FormatVersion, DateTime CreatedAtUtc, long DatabaseLength, string Sha256);
    }
}
