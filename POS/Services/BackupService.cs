using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Security.Cryptography;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
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
                if (string.IsNullOrWhiteSpace(folder) || !Directory.Exists(folder))
                    return;

                string path = Path.Combine(folder, $"KarobarPOS_{DateTime.Now:yyyyMMdd}.kposbackup");
                ExportPortableBackup(path);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[AutoBackup] Failed on exit: {ex.Message}");
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
                Mode = SqliteOpenMode.ReadOnly
            }.ToString());
            using var destination = new SqliteConnection($"Data Source={destinationPath}");
            source.Open();
            destination.Open();
            source.BackupDatabase(destination);
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
                Mode = SqliteOpenMode.ReadOnly
            }.ToString());
            connection.Open();
            using var command = connection.CreateCommand();
            command.CommandText = "PRAGMA integrity_check;";
            if (!string.Equals(command.ExecuteScalar()?.ToString(), "ok", StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException("SQLite reported that this database backup is corrupt.");

            command.CommandText = "SELECT COUNT(*) FROM sqlite_master WHERE type = 'table' AND name IN ('Products', 'Customers', 'Sales', 'SaleItems', 'StoreSettings');";
            if (Convert.ToInt32(command.ExecuteScalar()) != 5)
                throw new InvalidDataException("This file does not contain a Karobar POS database.");
        }

        private static string HashFile(string path)
        {
            using var stream = File.OpenRead(path);
            return Convert.ToHexString(SHA256.HashData(stream));
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
