using System;
using System.Collections.Generic;
using System.Data.Common;
using System.IO;
using System.Linq;
using Microsoft.EntityFrameworkCore;
using POS.Models;

namespace POS.Services
{
    public static class DatabaseInitializer
    {
        private static readonly object InitLock = new();
        private static bool _initialized;

        public static void Initialize(bool force = false)
        {
            lock (InitLock)
            {
                if (_initialized && !force) return;

                ImportLegacyDatabaseIfNeeded();

                using var context = new AppDbContext();
                context.Database.EnsureCreated();

                var conn = context.Database.GetDbConnection();
                conn.Open();
                try
                {
                    EnsureNewTables(conn);
                    EnsureColumns(conn, "Products", new Dictionary<string, string>
                    {
                        ["Barcode"] = "TEXT NOT NULL DEFAULT ''",
                        ["Brand"] = "TEXT NOT NULL DEFAULT ''",
                        ["Unit"] = "TEXT NOT NULL DEFAULT 'pcs'",
                        ["PackUnitName"] = "TEXT NOT NULL DEFAULT ''",
                        ["UnitsPerPack"] = "TEXT NOT NULL DEFAULT '1'",
                        ["LowStockThreshold"] = "TEXT NOT NULL DEFAULT '10'",
                        ["DefaultWarranty"] = "TEXT NOT NULL DEFAULT ''"
                    });

                    EnsureColumns(conn, "SaleItems", new Dictionary<string, string>
                    {
                        ["UnitPrice"] = "TEXT NOT NULL DEFAULT '0'",
                        ["UnitCost"] = "TEXT NOT NULL DEFAULT '0'",
                        ["Discount"] = "TEXT NOT NULL DEFAULT '0'",
                        ["Unit"] = "TEXT NOT NULL DEFAULT 'pcs'",
                        ["SerialNumber"] = "TEXT NULL",
                        ["WarrantyPeriod"] = "TEXT NULL"
                    });

                    EnsureColumns(conn, "Purchases", new Dictionary<string, string>
                    {
                        ["Unit"] = "TEXT NOT NULL DEFAULT 'pcs'",
                        ["CreatedBy"] = "TEXT NOT NULL DEFAULT ''"
                    });

                    EnsureColumns(conn, "Sales", new Dictionary<string, string>
                    {
                        ["RecordType"] = "TEXT NOT NULL DEFAULT 'Sale'",
                        ["Notes"] = "TEXT NOT NULL DEFAULT ''",
                        ["OriginalSaleId"] = "INTEGER NULL",
                        ["CreatedBy"] = "TEXT NOT NULL DEFAULT ''",
                        ["CashTendered"] = "TEXT NOT NULL DEFAULT '0'",
                        ["ChangeReturned"] = "TEXT NOT NULL DEFAULT '0'"
                    });

                    ExecuteNonQuery(conn, @"
                        UPDATE Sales
                        SET RecordType = 'Payment'
                        WHERE (RecordType IS NULL OR RecordType = '' OR RecordType = 'Sale')
                          AND CAST(TotalAmount AS REAL) = 0
                          AND ReceiptNumber LIKE 'PAY-%';
                    ");

                    ExecuteNonQuery(conn, @"
                        UPDATE SaleItems
                        SET UnitPrice = CASE WHEN CAST(Quantity AS REAL) > 0 THEN CAST(Price AS REAL) / CAST(Quantity AS REAL) ELSE CAST(Price AS REAL) END
                        WHERE CAST(UnitPrice AS REAL) = 0 AND CAST(Price AS REAL) > 0;
                    ");
                }
                finally
                {
                    conn.Close();
                }

                SeedDefaults(context);
                _initialized = true;
            }
        }

        private static void ImportLegacyDatabaseIfNeeded()
        {
            try
            {
                string targetDb = AppDbContext.GetDatabasePath();
                if (File.Exists(targetDb)) return;

                string legacyDb = Path.Combine(AppDbContext.GetLegacyAppDirectory(), "pos.db");
                if (File.Exists(legacyDb) && !string.Equals(legacyDb, targetDb, StringComparison.OrdinalIgnoreCase))
                {
                    File.Copy(legacyDb, targetDb, overwrite: false);
                }
            }
            catch
            {
                // Ignore copy errors and fall back to creating a fresh database
            }
        }

        private static void EnsureNewTables(DbConnection conn)
        {
            ExecuteNonQuery(conn, @"
                CREATE TABLE IF NOT EXISTS StoreSettings (
                    Id INTEGER PRIMARY KEY AUTOINCREMENT,
                    ShopName TEXT NOT NULL DEFAULT 'Aizaz Fareed Center & Al-Kuwait Electronics',
                    Subtitle TEXT NOT NULL DEFAULT 'Electric, Sanitary, Hardware, Tile, Paint & Solar System',
                    Address TEXT NOT NULL DEFAULT 'Opp. Major Shabbir Sharif Hospital, Kunjah',
                    Phone1 TEXT NOT NULL DEFAULT '0336-4994843',
                    Phone2 TEXT NOT NULL DEFAULT '0327-5898969',
                    ReceiptFooter TEXT NOT NULL DEFAULT 'Thank you for your business!',
                    PrinterName TEXT NOT NULL DEFAULT 'XP-80',
                    PreferredPrintFormat TEXT NOT NULL DEFAULT 'Thermal80mm',
                    UseBitmapThermalForUrdu INTEGER NOT NULL DEFAULT 0,
                    Language TEXT NOT NULL DEFAULT 'English',
                    Theme TEXT NOT NULL DEFAULT 'Light',
                    UpdatedAt TEXT NOT NULL DEFAULT ''
                );
            ");

            ExecuteNonQuery(conn, @"
                CREATE TABLE IF NOT EXISTS StaffUsers (
                    Id INTEGER PRIMARY KEY AUTOINCREMENT,
                    DisplayName TEXT NOT NULL DEFAULT '',
                    Role TEXT NOT NULL DEFAULT 'Cashier',
                    PinHash TEXT NOT NULL DEFAULT '',
                    IsActive INTEGER NOT NULL DEFAULT 1,
                    CreatedAt TEXT NOT NULL DEFAULT ''
                );
            ");
        }

        private static void EnsureColumns(DbConnection conn, string tableName, Dictionary<string, string> columns)
        {
            var existing = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            using (var cmd = conn.CreateCommand())
            {
                cmd.CommandText = $"PRAGMA table_info(\"{tableName}\");";
                using var reader = cmd.ExecuteReader();
                while (reader.Read())
                {
                    if (!reader.IsDBNull(1))
                    {
                        existing.Add(reader.GetString(1));
                    }
                }
            }

            foreach (var col in columns)
            {
                if (!existing.Contains(col.Key))
                {
                    ExecuteNonQuery(conn, $"ALTER TABLE \"{tableName}\" ADD COLUMN \"{col.Key}\" {col.Value};");
                }
            }
        }

        private static void ExecuteNonQuery(DbConnection conn, string sql)
        {
            using var cmd = conn.CreateCommand();
            cmd.CommandText = sql;
            cmd.ExecuteNonQuery();
        }

        private static void SeedDefaults(AppDbContext context)
        {
            if (!context.StoreSettings.Any())
            {
                context.StoreSettings.Add(new StoreSettings
                {
                    Id = 1,
                    ShopName = "Aizaz Fareed Center & Al-Kuwait Electronics",
                    Subtitle = "Electric, Sanitary, Hardware, Tile, Paint & Solar System",
                    Address = "Opp. Major Shabbir Sharif Hospital, Kunjah",
                    Phone1 = "0336-4994843",
                    Phone2 = "0327-5898969",
                    ReceiptFooter = "Thank you for your business!",
                    PrinterName = "XP-80",
                    PreferredPrintFormat = "Thermal80mm",
                    Language = "English",
                    Theme = "Light",
                    UpdatedAt = DateTime.Now
                });
                context.SaveChanges();
            }

            if (!context.StaffUsers.Any())
            {
                context.StaffUsers.AddRange(
                    new StaffUser
                    {
                        DisplayName = "Dr. Ehsan",
                        Role = "Owner",
                        PinHash = SessionService.HashPin("1234"),
                        IsActive = true,
                        CreatedAt = DateTime.Now
                    },
                    new StaffUser
                    {
                        DisplayName = "Sameer Younis",
                        Role = "Owner",
                        PinHash = SessionService.HashPin("1234"),
                        IsActive = true,
                        CreatedAt = DateTime.Now
                    },
                    new StaffUser
                    {
                        DisplayName = "Counter Cashier",
                        Role = "Cashier",
                        PinHash = SessionService.HashPin("0000"),
                        IsActive = true,
                        CreatedAt = DateTime.Now
                    }
                );
                context.SaveChanges();
            }
        }
    }
}
