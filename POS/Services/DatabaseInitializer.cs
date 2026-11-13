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

                    EnsureColumns(conn, "StoreSettings", new Dictionary<string, string>
                    {
                        ["AutoBackupFolder"] = "TEXT NOT NULL DEFAULT ''"
                    });

                    EnsureColumns(conn, "Customers", new Dictionary<string, string>
                    {
                        ["CreditLimit"] = "TEXT NOT NULL DEFAULT '0'"
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

                    EnsureLedgerAccounts(conn);
                    EnsureColumns(conn, "PaymentInstruments", new Dictionary<string, string>
                    {
                        ["InstallmentContractId"] = "INTEGER NULL",
                        ["InstallmentPaymentId"] = "INTEGER NULL"
                    });
                    EnsureColumns(conn, "InstallmentPayments", new Dictionary<string, string>
                    {
                        ["IsReversed"] = "INTEGER NOT NULL DEFAULT 0",
                        ["ReversedReason"] = "TEXT NOT NULL DEFAULT ''"
                    });
                    EnsureColumns(conn, "InstallmentContracts", new Dictionary<string, string>
                    {
                        ["IsReversed"] = "INTEGER NOT NULL DEFAULT 0",
                        ["ReversedReason"] = "TEXT NOT NULL DEFAULT ''"
                    });
                    EnsureColumns(conn, "InstallmentPaymentAllocations", new Dictionary<string, string>
                    {
                        ["PrincipalAmount"] = "TEXT NOT NULL DEFAULT '0'",
                        ["LateFeeAmount"] = "TEXT NOT NULL DEFAULT '0'"
                    });
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

            ExecuteNonQuery(conn, @"
                CREATE TABLE IF NOT EXISTS KhataAccounts (
                    Id INTEGER PRIMARY KEY AUTOINCREMENT,
                    CustomerId INTEGER NULL,
                    PartyType TEXT NOT NULL DEFAULT 'Customer',
                    Name TEXT NOT NULL DEFAULT '',
                    Phone TEXT NOT NULL DEFAULT '',
                    CNIC TEXT NOT NULL DEFAULT '',
                    FatherOrHusbandName TEXT NOT NULL DEFAULT '',
                    Address TEXT NOT NULL DEFAULT '',
                    WorkAddress TEXT NOT NULL DEFAULT '',
                    CreditLimit TEXT NOT NULL DEFAULT '0',
                    IsActive INTEGER NOT NULL DEFAULT 1,
                    CreatedBy TEXT NOT NULL DEFAULT '',
                    CreatedAt TEXT NOT NULL DEFAULT '',
                    UpdatedAt TEXT NOT NULL DEFAULT ''
                );
                CREATE UNIQUE INDEX IF NOT EXISTS IX_KhataAccounts_CustomerId ON KhataAccounts(CustomerId) WHERE CustomerId IS NOT NULL;
            ");

            ExecuteNonQuery(conn, @"
                CREATE TABLE IF NOT EXISTS KhataEntries (
                    Id INTEGER PRIMARY KEY AUTOINCREMENT,
                    AccountId INTEGER NOT NULL REFERENCES KhataAccounts(Id) ON DELETE RESTRICT,
                    VoucherNumber TEXT NOT NULL DEFAULT '',
                    Description TEXT NOT NULL DEFAULT '',
                    PaymentMode TEXT NOT NULL DEFAULT 'Journal',
                    Debit TEXT NOT NULL DEFAULT '0',
                    Credit TEXT NOT NULL DEFAULT '0',
                    SourceType TEXT NOT NULL DEFAULT '',
                    SourceKey TEXT NOT NULL DEFAULT '',
                    ReversesEntryId INTEGER NULL REFERENCES KhataEntries(Id) ON DELETE RESTRICT,
                    CreatedBy TEXT NOT NULL DEFAULT '',
                    CreatedAt TEXT NOT NULL DEFAULT ''
                );
                CREATE UNIQUE INDEX IF NOT EXISTS IX_KhataEntries_Source ON KhataEntries(SourceType, SourceKey);
                CREATE INDEX IF NOT EXISTS IX_KhataEntries_AccountDate ON KhataEntries(AccountId, CreatedAt, Id);
            ");

            ExecuteNonQuery(conn, @"
                CREATE TABLE IF NOT EXISTS PaymentInstruments (
                    Id INTEGER PRIMARY KEY AUTOINCREMENT,
                    AccountId INTEGER NOT NULL REFERENCES KhataAccounts(Id) ON DELETE RESTRICT,
                    Direction TEXT NOT NULL DEFAULT 'In',
                    Amount TEXT NOT NULL DEFAULT '0',
                    PaymentMode TEXT NOT NULL DEFAULT 'PDC',
                    ChequeNumber TEXT NOT NULL DEFAULT '',
                    BankName TEXT NOT NULL DEFAULT '',
                    ClearingDate TEXT NOT NULL DEFAULT '',
                    Status TEXT NOT NULL DEFAULT 'Pending',
                    Notes TEXT NOT NULL DEFAULT '',
                    KhataEntryId INTEGER NULL REFERENCES KhataEntries(Id) ON DELETE RESTRICT,
                    InstallmentContractId INTEGER NULL REFERENCES InstallmentContracts(Id) ON DELETE RESTRICT,
                    InstallmentPaymentId INTEGER NULL REFERENCES InstallmentPayments(Id) ON DELETE RESTRICT,
                    CreatedBy TEXT NOT NULL DEFAULT '',
                    CreatedAt TEXT NOT NULL DEFAULT '',
                    ModifiedAt TEXT NULL,
                    ModifiedReason TEXT NOT NULL DEFAULT ''
                );
                CREATE INDEX IF NOT EXISTS IX_PaymentInstruments_StatusDate ON PaymentInstruments(Status, ClearingDate);
            ");

            ExecuteNonQuery(conn, @"
                CREATE TABLE IF NOT EXISTS InstallmentContracts (
                    Id INTEGER PRIMARY KEY AUTOINCREMENT,
                    ContractNumber TEXT NOT NULL,
                    CustomerId INTEGER NOT NULL REFERENCES Customers(Id) ON DELETE RESTRICT,
                    SaleId INTEGER NOT NULL REFERENCES Sales(Id) ON DELETE RESTRICT,
                    CashPrice TEXT NOT NULL DEFAULT '0',
                    MarkupAmount TEXT NOT NULL DEFAULT '0',
                    ProcessingFee TEXT NOT NULL DEFAULT '0',
                    DownPayment TEXT NOT NULL DEFAULT '0',
                    TotalPayable TEXT NOT NULL DEFAULT '0',
                    Frequency TEXT NOT NULL DEFAULT 'Monthly',
                    IntervalDays INTEGER NOT NULL DEFAULT 30,
                    InstallmentsCount INTEGER NOT NULL DEFAULT 0,
                    FirstDueDate TEXT NOT NULL DEFAULT '',
                    GraceDays INTEGER NOT NULL DEFAULT 3,
                    LateFeePerInstallment TEXT NOT NULL DEFAULT '0',
                    RecoveryOfficer TEXT NOT NULL DEFAULT '',
                    Status TEXT NOT NULL DEFAULT 'Active',
                    CreatedBy TEXT NOT NULL DEFAULT '',
                    CreatedAt TEXT NOT NULL DEFAULT '',
                    IsReversed INTEGER NOT NULL DEFAULT 0,
                    ReversedReason TEXT NOT NULL DEFAULT '',
                    ModifiedAt TEXT NULL,
                    ModifiedReason TEXT NOT NULL DEFAULT ''
                );
                CREATE UNIQUE INDEX IF NOT EXISTS IX_InstallmentContracts_Number ON InstallmentContracts(ContractNumber);
                CREATE INDEX IF NOT EXISTS IX_InstallmentContracts_Status ON InstallmentContracts(Status, CustomerId);
            ");

            ExecuteNonQuery(conn, @"
                CREATE TABLE IF NOT EXISTS InstallmentContractItems (
                    Id INTEGER PRIMARY KEY AUTOINCREMENT,
                    ContractId INTEGER NOT NULL REFERENCES InstallmentContracts(Id) ON DELETE CASCADE,
                    ProductId INTEGER NOT NULL REFERENCES Products(Id) ON DELETE RESTRICT,
                    ProductName TEXT NOT NULL DEFAULT '',
                    Quantity TEXT NOT NULL DEFAULT '0',
                    Unit TEXT NOT NULL DEFAULT 'pcs',
                    UnitPrice TEXT NOT NULL DEFAULT '0',
                    UnitCost TEXT NOT NULL DEFAULT '0'
                );
                CREATE TABLE IF NOT EXISTS InstallmentGuarantors (
                    Id INTEGER PRIMARY KEY AUTOINCREMENT,
                    ContractId INTEGER NOT NULL REFERENCES InstallmentContracts(Id) ON DELETE CASCADE,
                    FullName TEXT NOT NULL DEFAULT '',
                    CNIC TEXT NOT NULL DEFAULT '',
                    Phone TEXT NOT NULL DEFAULT '',
                    Address TEXT NOT NULL DEFAULT '',
                    Relationship TEXT NOT NULL DEFAULT ''
                );
                CREATE TABLE IF NOT EXISTS CustomerDocuments (
                    Id INTEGER PRIMARY KEY AUTOINCREMENT,
                    CustomerId INTEGER NOT NULL REFERENCES Customers(Id) ON DELETE CASCADE,
                    DocumentType TEXT NOT NULL DEFAULT '',
                    FileName TEXT NOT NULL DEFAULT '',
                    ContentType TEXT NOT NULL DEFAULT 'application/octet-stream',
                    Content BLOB NOT NULL,
                    UploadedBy TEXT NOT NULL DEFAULT '',
                    UploadedAt TEXT NOT NULL DEFAULT ''
                );
                CREATE TABLE IF NOT EXISTS InstallmentSchedules (
                    Id INTEGER PRIMARY KEY AUTOINCREMENT,
                    ContractId INTEGER NOT NULL REFERENCES InstallmentContracts(Id) ON DELETE CASCADE,
                    Sequence INTEGER NOT NULL,
                    DueDate TEXT NOT NULL,
                    AmountDue TEXT NOT NULL DEFAULT '0',
                    AmountPaid TEXT NOT NULL DEFAULT '0',
                    LateFee TEXT NOT NULL DEFAULT '0',
                    LateFeePaid TEXT NOT NULL DEFAULT '0',
                    LateFeeWaived INTEGER NOT NULL DEFAULT 0,
                    WaiverReason TEXT NOT NULL DEFAULT '',
                    Status TEXT NOT NULL DEFAULT 'Due',
                    UNIQUE(ContractId, Sequence)
                );
                CREATE INDEX IF NOT EXISTS IX_InstallmentSchedules_DueDate ON InstallmentSchedules(Status, DueDate);
                CREATE TABLE IF NOT EXISTS InstallmentPayments (
                    Id INTEGER PRIMARY KEY AUTOINCREMENT,
                    ContractId INTEGER NOT NULL REFERENCES InstallmentContracts(Id) ON DELETE RESTRICT,
                    ReceiptNumber TEXT NOT NULL,
                    Amount TEXT NOT NULL DEFAULT '0',
                    PaymentMode TEXT NOT NULL DEFAULT 'Cash',
                    Notes TEXT NOT NULL DEFAULT '',
                    CreatedBy TEXT NOT NULL DEFAULT '',
                    CreatedAt TEXT NOT NULL DEFAULT '',
                    IsReversed INTEGER NOT NULL DEFAULT 0,
                    ReversedReason TEXT NOT NULL DEFAULT ''
                );
                CREATE UNIQUE INDEX IF NOT EXISTS IX_InstallmentPayments_Receipt ON InstallmentPayments(ReceiptNumber);
                CREATE TABLE IF NOT EXISTS InstallmentPaymentAllocations (
                    Id INTEGER PRIMARY KEY AUTOINCREMENT,
                    PaymentId INTEGER NOT NULL REFERENCES InstallmentPayments(Id) ON DELETE CASCADE,
                    ScheduleId INTEGER NOT NULL REFERENCES InstallmentSchedules(Id) ON DELETE RESTRICT,
                    Amount TEXT NOT NULL DEFAULT '0',
                    PrincipalAmount TEXT NOT NULL DEFAULT '0',
                    LateFeeAmount TEXT NOT NULL DEFAULT '0',
                    UNIQUE(PaymentId, ScheduleId)
                );
            ");
        }

        private static void EnsureLedgerAccounts(DbConnection conn)
        {
            ExecuteNonQuery(conn, @"
                INSERT OR IGNORE INTO KhataAccounts(CustomerId, PartyType, Name, Phone, Address, CreditLimit, IsActive, CreatedAt, UpdatedAt)
                SELECT Id, 'Customer', Name, Phone, Address, CreditLimit, 1, CreatedAt, UpdatedAt FROM Customers;
            ");

            var accounts = new Dictionary<int, (int AccountId, decimal CurrentBalance)>();
            using (var customers = conn.CreateCommand())
            {
                customers.CommandText = "SELECT c.Id, a.Id, c.TotalDues FROM Customers c JOIN KhataAccounts a ON a.CustomerId = c.Id;";
                using var reader = customers.ExecuteReader();
                while (reader.Read())
                    accounts[reader.GetInt32(0)] = (reader.GetInt32(1), ParseMoney(reader.GetValue(2)));
            }

            var legacyTransactions = new List<(int CustomerId, int SaleId, string? Receipt, string Type, string Method, decimal Previous, decimal Current, string Actor, string CreatedAt)>();
            using (var sales = conn.CreateCommand())
            {
                sales.CommandText = @"SELECT CustomerId, Id, ReceiptNumber, RecordType, PaymentMethod, PreviousDues, BalanceDue, CreatedBy, CreatedAt
                    FROM Sales WHERE CustomerId IS NOT NULL AND RecordType IN ('Sale', 'Return', 'Payment');";
                using var reader = sales.ExecuteReader();
                while (reader.Read())
                    legacyTransactions.Add((reader.GetInt32(0), reader.GetInt32(1), reader.IsDBNull(2) ? null : reader.GetString(2), reader.GetString(3), reader.GetString(4), ParseMoney(reader.GetValue(5)), ParseMoney(reader.GetValue(6)), reader.IsDBNull(7) ? string.Empty : reader.GetString(7), reader.GetString(8)));
            }

            foreach (var sale in legacyTransactions)
            {
                if (!accounts.TryGetValue(sale.CustomerId, out var account)) continue;
                decimal delta = sale.Current - sale.Previous;
                if (delta == 0m) continue;
                using var insert = conn.CreateCommand();
                insert.CommandText = @"INSERT OR IGNORE INTO KhataEntries(AccountId, VoucherNumber, Description, PaymentMode, Debit, Credit, SourceType, SourceKey, CreatedBy, CreatedAt)
                    VALUES($accountId, $voucher, $description, $method, $debit, $credit, 'LegacySale', $key, $actor, $createdAt);";
                AddParameter(insert, "$accountId", account.AccountId);
                AddParameter(insert, "$voucher", sale.Receipt ?? $"LEG-{sale.SaleId}");
                AddParameter(insert, "$description", sale.Type switch { "Payment" => "Legacy khata receipt", "Return" => "Legacy sale return", _ => "Legacy sale posting" });
                AddParameter(insert, "$method", sale.Method);
                AddParameter(insert, "$debit", Math.Max(0m, delta).ToString(System.Globalization.CultureInfo.InvariantCulture));
                AddParameter(insert, "$credit", Math.Max(0m, -delta).ToString(System.Globalization.CultureInfo.InvariantCulture));
                AddParameter(insert, "$key", sale.SaleId.ToString(System.Globalization.CultureInfo.InvariantCulture));
                AddParameter(insert, "$actor", sale.Actor);
                AddParameter(insert, "$createdAt", sale.CreatedAt);
                insert.ExecuteNonQuery();
            }

            foreach (var (customerId, account) in accounts)
            {
                string key = customerId.ToString(System.Globalization.CultureInfo.InvariantCulture);
                using var existing = conn.CreateCommand();
                existing.CommandText = "SELECT COUNT(*) FROM KhataEntries WHERE SourceType = 'LegacyOpening' AND SourceKey = $key;";
                AddParameter(existing, "$key", key);
                if (Convert.ToInt32(existing.ExecuteScalar()) > 0) continue;

                decimal ledgerBalance = 0m;
                using (var entries = conn.CreateCommand())
                {
                    entries.CommandText = "SELECT Debit, Credit FROM KhataEntries WHERE AccountId = $accountId;";
                    AddParameter(entries, "$accountId", account.AccountId);
                    using var reader = entries.ExecuteReader();
                    while (reader.Read())
                        ledgerBalance += ParseMoney(reader.GetValue(0)) - ParseMoney(reader.GetValue(1));
                }

                decimal opening = account.CurrentBalance - ledgerBalance;
                if (opening == 0m) continue;
                using var insert = conn.CreateCommand();
                insert.CommandText = @"INSERT INTO KhataEntries(AccountId, VoucherNumber, Description, PaymentMode, Debit, Credit, SourceType, SourceKey, CreatedBy, CreatedAt)
                    VALUES($accountId, $voucher, 'Opening balance brought forward', 'Opening', $debit, $credit, 'LegacyOpening', $key, 'System', CURRENT_TIMESTAMP);";
                AddParameter(insert, "$accountId", account.AccountId);
                AddParameter(insert, "$voucher", $"OB-{customerId:000000}");
                AddParameter(insert, "$debit", Math.Max(0m, opening).ToString(System.Globalization.CultureInfo.InvariantCulture));
                AddParameter(insert, "$credit", Math.Max(0m, -opening).ToString(System.Globalization.CultureInfo.InvariantCulture));
                AddParameter(insert, "$key", key);
                insert.ExecuteNonQuery();
            }
        }

        private static decimal ParseMoney(object value) => decimal.TryParse(value?.ToString(), System.Globalization.NumberStyles.Number, System.Globalization.CultureInfo.InvariantCulture, out var amount) ? amount : 0m;

        private static void AddParameter(DbCommand command, string name, object value)
        {
            var parameter = command.CreateParameter();
            parameter.ParameterName = name;
            parameter.Value = value;
            command.Parameters.Add(parameter);
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
