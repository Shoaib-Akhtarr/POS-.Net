using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using POS.Models;

namespace POS.Services
{
    public sealed record DailyCashBookLine(DateTime Date, string Voucher, string Party, string Description, string Mode, decimal Inflow, decimal Outflow);

    public sealed record DailyZReportSummary(
        DateTime Date,
        string CashierName,
        int TotalInvoicesCount,
        decimal CashSales,
        decimal CardSales,
        decimal BankSales,
        decimal CreditSales,
        decimal TotalSalesRevenue,
        decimal KhataCashIn,
        decimal InstallmentRecoveries,
        decimal TotalCashInflow,
        decimal CashOutExpenses,
        decimal ExpectedDrawerCash,
        decimal ActualDrawerCash,
        decimal Variance,
        int FbrOfflinePendingCount);

    public class PrintService
    {
        public static List<string> GetInstalledPrinters()
        {
            var printers = new List<string>();
            if (!OperatingSystem.IsWindows()) return printers;

            try
            {
                foreach (string printer in System.Drawing.Printing.PrinterSettings.InstalledPrinters)
                {
                    printers.Add(printer);
                }
            }
            catch
            {
                // Ignore printer enumeration issues on systems without print spooler
            }
            return printers;
        }

        public static FlowDocument CloneDocument(FlowDocument source)
        {
            string xaml = System.Windows.Markup.XamlWriter.Save(source);
            using var sr = new System.IO.StringReader(xaml);
            using var xr = System.Xml.XmlReader.Create(sr);
            return (FlowDocument)System.Windows.Markup.XamlReader.Load(xr);
        }

        public (bool success, string message) PrintSaleAuto(Sale sale)
        {
            var settings = ThemeService.GetSettings();
            return PrintA4Invoice(sale, settings);
        }

        public void PrintReceipt(Sale sale)
        {
            var result = PrintA4Invoice(sale);
            if (!result.success && !string.IsNullOrWhiteSpace(result.message))
            {
                MessageBox.Show(result.message, "Printing Notice", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        public (bool success, string message) PrintPurchaseSlipAuto(Purchase purchase)
        {
            var settings = ThemeService.GetSettings();
            return PrintPurchaseA4Slip(purchase, settings);
        }

        public (bool success, string message) PrintPurchaseThermalSlip(Purchase purchase, StoreSettings? settings = null)
        {
            settings ??= ThemeService.GetSettings();
            return PrintPurchaseA4Slip(purchase, settings);
        }

        public (bool success, string message) PrintPurchaseA4Slip(Purchase purchase, StoreSettings? settings = null)
        {
            settings ??= ThemeService.GetSettings();
            try
            {
                var doc = BuildA4PurchaseSlipDocument(purchase, settings);
                return PrintFlowDocument(doc, $"Stock-Slip-PUR-{purchase.Id}");
            }
            catch (Exception ex)
            {
                return (false, $"A4 Slip Print Error: {ex.Message}");
            }
        }

        private FlowDocument BuildA4PurchaseSlipDocument(Purchase purchase, StoreSettings settings)
        {
            var doc = new FlowDocument
            {
                FontFamily = new FontFamily("Segoe UI"),
                FontSize = 11,
                Foreground = new SolidColorBrush(Color.FromRgb(15, 23, 42)),
                PagePadding = new Thickness(40, 36, 40, 36)
            };

            // 1. Official Letterhead Header (matching pad)
            AddLetterheadHeader(doc, settings);

            // 2. Title Banner
            var titleTable = new Table { CellSpacing = 0, Margin = new Thickness(0, 0, 0, 10) };
            titleTable.Columns.Add(new TableColumn { Width = new GridLength(1.4, GridUnitType.Star) });
            titleTable.Columns.Add(new TableColumn { Width = new GridLength(0.6, GridUnitType.Star) });
            var titleRg = new TableRowGroup();
            var titleRow = new TableRow();

            var tLeftPara = new Paragraph(new Run("GOODS RECEIVED NOTE (GRN) / STOCK INWARD VOUCHER") { FontSize = 12, FontWeight = FontWeights.Black, Foreground = Brushes.White })
            {
                Margin = new Thickness(12, 8, 12, 8)
            };
            var tLeft = new TableCell(tLeftPara)
            {
                Background = new SolidColorBrush(Color.FromRgb(13, 148, 136)),
                BorderBrush = new SolidColorBrush(Color.FromRgb(13, 148, 136)),
                BorderThickness = new Thickness(1)
            };

            var tRightPara = new Paragraph(new Run($"#PUR-{purchase.Id:D5}") { FontSize = 12, FontWeight = FontWeights.Black, Foreground = Brushes.White })
            {
                Margin = new Thickness(12, 8, 12, 8),
                TextAlignment = TextAlignment.Right
            };
            var tRight = new TableCell(tRightPara)
            {
                Background = new SolidColorBrush(Color.FromRgb(15, 118, 110)),
                BorderBrush = new SolidColorBrush(Color.FromRgb(13, 148, 136)),
                BorderThickness = new Thickness(1)
            };

            titleRow.Cells.Add(tLeft);
            titleRow.Cells.Add(tRight);
            titleRg.Rows.Add(titleRow);
            titleTable.RowGroups.Add(titleRg);
            doc.Blocks.Add(titleTable);

            // 3. Info Cards (Supplier left, Voucher Meta right)
            var infoTable = new Table { CellSpacing = 12, Margin = new Thickness(0, 0, 0, 14) };
            infoTable.Columns.Add(new TableColumn { Width = new GridLength(1.1, GridUnitType.Star) });
            infoTable.Columns.Add(new TableColumn { Width = new GridLength(0.9, GridUnitType.Star) });
            var infoRg = new TableRowGroup();
            var infoRow = new TableRow();

            // Left card - Supplier
            var supCardPara = new Paragraph { Margin = new Thickness(12, 10, 12, 10), LineHeight = 17 };
            supCardPara.Inlines.Add(new Run("SUPPLIER / VENDOR DETAILS\n") { FontSize = 9, FontWeight = FontWeights.Bold, Foreground = new SolidColorBrush(Color.FromRgb(100, 116, 139)) });
            string supName = string.IsNullOrWhiteSpace(purchase.SupplierName) ? "General Supplier / Open Market" : purchase.SupplierName;
            supCardPara.Inlines.Add(new Run(supName + "\n") { FontSize = 13, FontWeight = FontWeights.Bold, Foreground = new SolidColorBrush(Color.FromRgb(15, 23, 42)) });
            supCardPara.Inlines.Add(new Run("Status: Verified Inward Record") { FontSize = 9.5, Foreground = new SolidColorBrush(Color.FromRgb(100, 116, 139)) });
            var supCell = new TableCell(supCardPara)
            {
                Background = new SolidColorBrush(Color.FromRgb(248, 250, 252)),
                BorderBrush = new SolidColorBrush(Color.FromRgb(226, 232, 240)),
                BorderThickness = new Thickness(1)
            };

            // Right card - Inward Meta
            var metaCardPara = new Paragraph { Margin = new Thickness(12, 10, 12, 10), LineHeight = 17 };
            metaCardPara.Inlines.Add(new Run("VOUCHER SPECIFICATIONS\n") { FontSize = 9, FontWeight = FontWeights.Bold, Foreground = new SolidColorBrush(Color.FromRgb(100, 116, 139)) });
            metaCardPara.Inlines.Add(new Run($"Date & Time: ") { FontSize = 9.5, FontWeight = FontWeights.SemiBold });
            metaCardPara.Inlines.Add(new Run($"{purchase.PurchaseDate:dd MMMM yyyy, hh:mm tt}\n") { FontSize = 9.5 });
            metaCardPara.Inlines.Add(new Run($"Received By: ") { FontSize = 9.5, FontWeight = FontWeights.SemiBold });
            metaCardPara.Inlines.Add(new Run($"{(string.IsNullOrWhiteSpace(purchase.CreatedBy) ? "Store Incharge" : purchase.CreatedBy)}\n") { FontSize = 9.5 });
            metaCardPara.Inlines.Add(new Run($"Payment Status: ") { FontSize = 9.5, FontWeight = FontWeights.SemiBold });
            metaCardPara.Inlines.Add(new Run("Settled / Cash\n") { FontSize = 9.5, FontWeight = FontWeights.Bold, Foreground = new SolidColorBrush(Color.FromRgb(16, 185, 129)) });
            var metaCell = new TableCell(metaCardPara)
            {
                Background = new SolidColorBrush(Color.FromRgb(248, 250, 252)),
                BorderBrush = new SolidColorBrush(Color.FromRgb(226, 232, 240)),
                BorderThickness = new Thickness(1)
            };

            infoRow.Cells.Add(supCell);
            infoRow.Cells.Add(metaCell);
            infoRg.Rows.Add(infoRow);
            infoTable.RowGroups.Add(infoRg);
            doc.Blocks.Add(infoTable);

            // 4. Inward Item Table
            var table = new Table { CellSpacing = 0, BorderBrush = new SolidColorBrush(Color.FromRgb(203, 213, 225)), BorderThickness = new Thickness(1), Margin = new Thickness(0, 0, 0, 14) };
            table.Columns.Add(new TableColumn { Width = new GridLength(45, GridUnitType.Pixel) }); // SR
            table.Columns.Add(new TableColumn { Width = new GridLength(3, GridUnitType.Star) });   // Product
            table.Columns.Add(new TableColumn { Width = new GridLength(1.6, GridUnitType.Star) }); // Dept / Brand
            table.Columns.Add(new TableColumn { Width = new GridLength(1.2, GridUnitType.Star) }); // Unit Cost
            table.Columns.Add(new TableColumn { Width = new GridLength(1.2, GridUnitType.Star) }); // Qty
            table.Columns.Add(new TableColumn { Width = new GridLength(1.5, GridUnitType.Star) }); // Total

            var rg = new TableRowGroup();
            var hdrRow = new TableRow { Background = new SolidColorBrush(Color.FromRgb(11, 79, 156)) };
            hdrRow.Cells.Add(CreateCell("SR#", FontWeights.Bold, TextAlignment.Center, Brushes.White, null, null, 9.5));
            hdrRow.Cells.Add(CreateCell("PRODUCT DESCRIPTION", FontWeights.Bold, TextAlignment.Left, Brushes.White, null, null, 9.5));
            hdrRow.Cells.Add(CreateCell("CATEGORY / BRAND", FontWeights.Bold, TextAlignment.Left, Brushes.White, null, null, 9.5));
            hdrRow.Cells.Add(CreateCell("UNIT COST", FontWeights.Bold, TextAlignment.Right, Brushes.White, null, null, 9.5));
            hdrRow.Cells.Add(CreateCell("QTY ADDED", FontWeights.Bold, TextAlignment.Right, Brushes.White, null, null, 9.5));
            hdrRow.Cells.Add(CreateCell("TOTAL VALUE", FontWeights.Bold, TextAlignment.Right, Brushes.White, null, null, 9.5));
            rg.Rows.Add(hdrRow);

            var row = new TableRow { Background = new SolidColorBrush(Color.FromRgb(248, 250, 252)) };
            var desc = new StringBuilder(purchase.ProductName);
            if (!string.IsNullOrWhiteSpace(purchase.Product?.Barcode)) desc.Append($"\nBarcode: {purchase.Product.Barcode}");
            if (!string.IsNullOrWhiteSpace(purchase.Product?.DefaultWarranty)) desc.Append($"  |  Warranty: {purchase.Product.DefaultWarranty}");

            var deptBrand = new StringBuilder();
            if (!string.IsNullOrWhiteSpace(purchase.Product?.Category)) deptBrand.Append(purchase.Product.Category);
            if (!string.IsNullOrWhiteSpace(purchase.Product?.Brand))
            {
                if (deptBrand.Length > 0) deptBrand.Append(" · ");
                deptBrand.Append(purchase.Product.Brand);
            }
            if (deptBrand.Length == 0) deptBrand.Append("General");

            row.Cells.Add(CreateCell("1", FontWeights.Normal, TextAlignment.Center));
            row.Cells.Add(CreateCell(desc.ToString(), FontWeights.SemiBold, TextAlignment.Left));
            row.Cells.Add(CreateCell(deptBrand.ToString(), FontWeights.Normal, TextAlignment.Left));
            row.Cells.Add(CreateCell($"Rs {purchase.CostPrice:N0}", FontWeights.Normal, TextAlignment.Right));
            row.Cells.Add(CreateCell($"+{FormatQty(purchase.Quantity)} {purchase.Unit}", FontWeights.Bold, TextAlignment.Right, new SolidColorBrush(Color.FromRgb(16, 185, 129))));
            row.Cells.Add(CreateCell($"Rs {purchase.TotalCost:N0}", FontWeights.Bold, TextAlignment.Right));
            rg.Rows.Add(row);

            table.RowGroups.Add(rg);
            doc.Blocks.Add(table);

            // 5. Total & Words
            var totalTable = new Table { CellSpacing = 0, Margin = new Thickness(0, 0, 0, 16) };
            totalTable.Columns.Add(new TableColumn { Width = new GridLength(1.2, GridUnitType.Star) });
            totalTable.Columns.Add(new TableColumn { Width = new GridLength(0.8, GridUnitType.Star) });
            var totalRg = new TableRowGroup();
            var totalRow = new TableRow();

            var wordsPara = new Paragraph { Margin = new Thickness(12, 10, 12, 10), LineHeight = 17 };
            wordsPara.Inlines.Add(new Run("AMOUNT IN WORDS\n") { FontSize = 8.5, FontWeight = FontWeights.Bold, Foreground = new SolidColorBrush(Color.FromRgb(100, 116, 139)) });
            wordsPara.Inlines.Add(new Run(NumberToWords(purchase.TotalCost)) { FontSize = 10, FontWeight = FontWeights.SemiBold, Foreground = new SolidColorBrush(Color.FromRgb(15, 23, 42)) });
            var wordsCell = new TableCell(wordsPara)
            {
                Background = new SolidColorBrush(Color.FromRgb(241, 245, 249)),
                BorderBrush = new SolidColorBrush(Color.FromRgb(203, 213, 225)),
                BorderThickness = new Thickness(1)
            };

            var sumPara = new Paragraph { Margin = new Thickness(12, 8, 12, 8), TextAlignment = TextAlignment.Right };
            sumPara.Inlines.Add(new Run("TOTAL INWARD VALUE\n") { FontSize = 9, FontWeight = FontWeights.Bold, Foreground = new SolidColorBrush(Color.FromRgb(100, 116, 139)) });
            sumPara.Inlines.Add(new Run($"Rs {purchase.TotalCost:N0}\n") { FontSize = 16, FontWeight = FontWeights.Black, Foreground = new SolidColorBrush(Color.FromRgb(11, 79, 156)) });
            if (purchase.Product?.SellingPrice > 0m)
            {
                sumPara.Inlines.Add(new Run($"Retail Selling Rate: Rs {purchase.Product.SellingPrice:N0} / {purchase.Unit}") { FontSize = 9, Foreground = new SolidColorBrush(Color.FromRgb(100, 116, 139)) });
            }
            var sumCell = new TableCell(sumPara)
            {
                Background = new SolidColorBrush(Color.FromRgb(241, 245, 249)),
                BorderBrush = new SolidColorBrush(Color.FromRgb(203, 213, 225)),
                BorderThickness = new Thickness(0, 1, 1, 1)
            };

            totalRow.Cells.Add(wordsCell);
            totalRow.Cells.Add(sumCell);
            totalRg.Rows.Add(totalRow);
            totalTable.RowGroups.Add(totalRg);
            doc.Blocks.Add(totalTable);

            // 6. Letterhead Footer with Signatures and Decorative Branding
            AddLetterheadFooter(doc, settings);

            return doc;
        }

        public (bool success, string message) PrintThermalReceipt(Sale sale, StoreSettings? settings = null)
        {
            settings ??= ThemeService.GetSettings();
            return PrintA4Invoice(sale, settings);
        }

        public (bool success, string message) TestPrint(string? printerNameOverride = null)
        {
            var settings = ThemeService.GetSettings();
            if (!string.IsNullOrWhiteSpace(printerNameOverride))
            {
                settings.PrinterName = printerNameOverride;
            }

            var testSale = new Sale
            {
                ReceiptNumber = "TEST-001",
                RecordType = "Sale",
                CustomerName = "A4 Test Customer",
                Notes = "A4 Layout Verification",
                CreatedBy = SessionService.CurrentUserName,
                TotalAmount = 1500,
                AmountPaid = 1500,
                CashTendered = 2000,
                ChangeReturned = 500,
                SaleItems = new List<SaleItem>
                {
                    new SaleItem { Name = "GM 7/29 Electric Cable (Copper)", Quantity = 12.5m, Unit = "m", UnitPrice = 120, Price = 1500 }
                }
            };

            return PrintA4Invoice(testSale, settings);
        }

        public (bool success, string message) PrintA4Invoice(Sale sale, StoreSettings? settings = null)
        {
            settings ??= ThemeService.GetSettings();

            try
            {
                var doc = BuildA4InvoiceDocument(sale, settings);
                return PrintFlowDocument(doc, $"Invoice #{sale.ReceiptNumber ?? sale.Id.ToString()}");
            }
            catch (Exception ex)
            {
                return (false, $"A4 Print Error: {ex.Message}");
            }
        }

        public (bool success, string message) PrintCustomerLedger(Customer customer, IEnumerable<Sale> history)
        {
            var settings = ThemeService.GetSettings();
            try
            {
                var doc = BuildCustomerLedgerDocument(customer, history, settings);
                return PrintFlowDocument(doc, $"Khata Ledger - {customer.Name}");
            }
            catch (Exception ex)
            {
                return (false, $"Ledger Print Error: {ex.Message}");
            }
        }

        public (bool success, string message) PrintPartyLedger(KhataAccount account, IEnumerable<(KhataEntry Entry, decimal RunningBalance)> history)
        {
            var settings = ThemeService.GetSettings();
            try
            {
                var document = new FlowDocument
                {
                    FontFamily = new FontFamily("Segoe UI"),
                    FontSize = 10,
                    Foreground = Brushes.Black
                };
                document.Blocks.Add(new Paragraph(new Run(settings.ShopName))
                {
                    FontSize = 20,
                    FontWeight = FontWeights.Bold,
                    TextAlignment = TextAlignment.Center,
                    Margin = new Thickness(0, 0, 0, 4)
                });
                document.Blocks.Add(new Paragraph(new Run($"Party account statement · {account.PartyType}"))
                {
                    FontSize = 14,
                    FontWeight = FontWeights.Bold,
                    Margin = new Thickness(0, 10, 0, 2)
                });
                document.Blocks.Add(new Paragraph(new Run($"{account.Name} · {account.Phone} · Printed {DateTime.Now:dd MMM yyyy HH:mm}"))
                {
                    Margin = new Thickness(0, 0, 0, 10)
                });

                var table = new Table { CellSpacing = 0 };
                foreach (double width in new[] { 68d, 105d, 208d, 75d, 75d, 85d })
                    table.Columns.Add(new TableColumn { Width = new GridLength(width) });
                var header = new TableRow { Background = new SolidColorBrush(Color.FromRgb(226, 232, 240)), FontWeight = FontWeights.Bold };
                foreach (string label in new[] { "Date", "Voucher", "Description", "Debit", "Credit", "Balance" })
                    header.Cells.Add(new TableCell(new Paragraph(new Run(label)) { Margin = new Thickness(4, 3, 4, 3) }));
                var rows = new TableRowGroup();
                rows.Rows.Add(header);

                foreach (var row in history)
                {
                    var tableRow = new TableRow();
                    string[] values =
                    {
                        row.Entry.CreatedAt.ToString("dd MMM yy"),
                        row.Entry.VoucherNumber,
                        row.Entry.Description,
                        row.Entry.Debit == 0m ? "—" : row.Entry.Debit.ToString("N2"),
                        row.Entry.Credit == 0m ? "—" : row.Entry.Credit.ToString("N2"),
                        row.RunningBalance.ToString("N2")
                    };
                    foreach (string value in values)
                        tableRow.Cells.Add(new TableCell(new Paragraph(new Run(value)) { Margin = new Thickness(4, 3, 4, 3) }));
                    rows.Rows.Add(tableRow);
                }

                table.RowGroups.Add(rows);
                document.Blocks.Add(table);
                document.Blocks.Add(new Paragraph(new Run($"Closing balance: PKR {history.LastOrDefault().RunningBalance:N2}"))
                {
                    FontSize = 13,
                    FontWeight = FontWeights.Bold,
                    TextAlignment = TextAlignment.Right,
                    Margin = new Thickness(0, 10, 0, 0)
                });
                return PrintFlowDocument(document, $"Khata Statement - {account.Name}");
            }
            catch (Exception ex)
            {
                return (false, $"Statement print error: {ex.Message}");
            }
        }

        public (bool success, string message) PrintDailyCashBook(DateTime day, IEnumerable<DailyCashBookLine> history)
        {
            try
            {
                var document = new FlowDocument
                {
                    FontFamily = new FontFamily("Segoe UI"),
                    FontSize = 10,
                    Foreground = Brushes.Black
                };
                document.Blocks.Add(new Paragraph(new Run($"Daily Cash Book · {day:dd MMM yyyy}"))
                {
                    FontSize = 18,
                    FontWeight = FontWeights.Bold,
                    TextAlignment = TextAlignment.Center
                });
                var table = new Table { CellSpacing = 0 };
                foreach (double width in new[] { 70d, 105d, 140d, 190d, 80d, 80d })
                    table.Columns.Add(new TableColumn { Width = new GridLength(width) });
                var rows = new TableRowGroup();
                var header = new TableRow { Background = new SolidColorBrush(Color.FromRgb(226, 232, 240)), FontWeight = FontWeights.Bold };
                foreach (string label in new[] { "Time", "Voucher", "Party", "Description", "Inflow", "Outflow" })
                    header.Cells.Add(new TableCell(new Paragraph(new Run(label)) { Margin = new Thickness(4, 3, 4, 3) }));
                rows.Rows.Add(header);
                foreach (var row in history)
                {
                    string[] values = { row.Date.ToString("HH:mm"), row.Voucher, row.Party, $"{row.Description} · {row.Mode}",
                        row.Inflow == 0m ? "—" : row.Inflow.ToString("N2"), row.Outflow == 0m ? "—" : row.Outflow.ToString("N2") };
                    var tableRow = new TableRow();
                    foreach (string value in values)
                        tableRow.Cells.Add(new TableCell(new Paragraph(new Run(value)) { Margin = new Thickness(4, 3, 4, 3) }));
                    rows.Rows.Add(tableRow);
                }
                table.RowGroups.Add(rows);
                document.Blocks.Add(table);
                return PrintFlowDocument(document, $"Roznamcha - {day:yyyyMMdd}");
            }
            catch (Exception ex)
            {
                return (false, $"Cash book print error: {ex.Message}");
            }
        }

        public (bool success, string message) PrintInstallmentAgreement(
            InstallmentContract contract,
            Customer customer,
            IReadOnlyList<InstallmentGuarantor> guarantors,
            IReadOnlyList<SaleItem> items,
            StoreSettings? settings = null)
        {
            settings ??= ThemeService.GetSettings();
            try
            {
                var doc = BuildInstallmentAgreementDocument(contract, customer, guarantors, items, settings);
                return PrintFlowDocument(doc, $"Installment Agreement - {contract.ContractNumber}");
            }
            catch (Exception ex)
            {
                return (false, $"Agreement Print Error: {ex.Message}");
            }
        }

        public (bool success, string message) PrintInstallmentPassbook(
            InstallmentContract contract,
            Customer customer,
            IReadOnlyList<InstallmentSchedule> schedules,
            StoreSettings? settings = null)
        {
            settings ??= ThemeService.GetSettings();
            try
            {
                var doc = BuildInstallmentPassbookDocument(contract, customer, schedules, settings);
                return PrintFlowDocument(doc, $"Installment Passbook - {contract.ContractNumber}");
            }
            catch (Exception ex)
            {
                return (false, $"Passbook Print Error: {ex.Message}");
            }
        }

        public (bool success, string message) PrintDailyZReport(
            DateTime date,
            DailyZReportSummary summary,
            StoreSettings? settings = null)
        {
            settings ??= ThemeService.GetSettings();
            try
            {
                var doc = BuildDailyZReportDocument(date, summary, settings);
                return PrintFlowDocument(doc, $"Z-Report {date:yyyyMMdd}");
            }
            catch (Exception ex)
            {
                return (false, $"Z-Report Print Error: {ex.Message}");
            }
        }

        private (bool success, string message) PrintFlowDocument(FlowDocument doc, string jobTitle)
        {
            var dlg = new PrintDialog();
            if (dlg.ShowDialog() != true)
            {
                return (false, string.Empty);
            }

            try
            {
                double width = dlg.PrintableAreaWidth > 0 ? dlg.PrintableAreaWidth : 794;
                double height = dlg.PrintableAreaHeight > 0 ? dlg.PrintableAreaHeight : 1123;

                doc.PageWidth = width;
                doc.PageHeight = height;
                doc.PagePadding = new Thickness(40);
                doc.ColumnWidth = width - 80;

                var paginator = ((IDocumentPaginatorSource)doc).DocumentPaginator;
                paginator.PageSize = new Size(width, height);

                dlg.PrintDocument(paginator, jobTitle);
                return (true, "Document sent to printer successfully.");
            }
            catch (Exception ex)
            {
                return (false, $"Print Error: {ex.Message}");
            }
        }

        public void ShowPrintPreview(FlowDocument doc, string jobTitle)
        {
            try
            {
                var previewWin = new POS.Views.PrintPreviewWindow(doc, jobTitle);
                previewWin.Owner = Application.Current?.MainWindow;
                previewWin.ShowDialog();
            }
            catch
            {
                // Fallback to standard print
                PrintFlowDocument(doc, jobTitle);
            }
        }

        private string? AutoDetectPrinter()
        {
            foreach (string printer in GetInstalledPrinters())
            {
                string p = printer.ToLowerInvariant();
                if (p.Contains("xp") || p.Contains("pos") || p.Contains("speed") || p.Contains("80mm") || p.Contains("thermal"))
                {
                    return printer;
                }
            }
            return null;
        }

        private static string FormatQty(decimal qty) =>
            qty % 1m == 0 ? qty.ToString("N0") : qty.ToString("0.##");

        private byte[] FormatAsciiReceipt(Sale sale, StoreSettings settings)
        {
            var bytes = new List<byte>();

            // Initialize
            bytes.AddRange(new byte[] { 0x1B, 0x40 });

            // Header
            bytes.AddRange(new byte[] { 0x1B, 0x61, 0x01 }); // Center
            bytes.AddRange(new byte[] { 0x1B, 0x21, 0x18 }); // Emphasized
            bytes.AddRange(Encoding.ASCII.GetBytes($"{ToSafeAscii(settings.ShopName)}\n"));
            bytes.AddRange(new byte[] { 0x1B, 0x21, 0x00 });

            if (!string.IsNullOrWhiteSpace(settings.Subtitle))
                bytes.AddRange(Encoding.ASCII.GetBytes($"{ToSafeAscii(settings.Subtitle)}\n"));
            if (!string.IsNullOrWhiteSpace(settings.Address))
                bytes.AddRange(Encoding.ASCII.GetBytes($"{ToSafeAscii(settings.Address)}\n"));

            string phones = string.Join(" / ", new[] { settings.Phone1, settings.Phone2 }.Where(p => !string.IsNullOrWhiteSpace(p)));
            if (!string.IsNullOrWhiteSpace(phones))
                bytes.AddRange(Encoding.ASCII.GetBytes($"Tel: {ToSafeAscii(phones)}\n"));

            bytes.AddRange(Encoding.ASCII.GetBytes("------------------------------------------\n"));
            string banner = sale.RecordType.ToUpperInvariant() switch
            {
                "ESTIMATE" => "*** QUOTATION / KACHA BILL ***",
                "RETURN" => "*** SALES RETURN SLIP ***",
                "PAYMENT" => "*** KHATA PAYMENT RECEIPT ***",
                _ => "*** SALE RECEIPT ***"
            };
            bytes.AddRange(new byte[] { 0x1B, 0x45, 0x01 });
            bytes.AddRange(Encoding.ASCII.GetBytes($"{banner}\n"));
            bytes.AddRange(new byte[] { 0x1B, 0x45, 0x00 });

            // Metadata
            bytes.AddRange(new byte[] { 0x1B, 0x61, 0x00 }); // Left
            bytes.AddRange(Encoding.ASCII.GetBytes($"Date:     {sale.CreatedAt:dd/MM/yyyy HH:mm}\n"));
            bytes.AddRange(Encoding.ASCII.GetBytes($"Receipt:  #{ToSafeAscii(sale.ReceiptNumber ?? sale.Id.ToString())}\n"));
            bytes.AddRange(Encoding.ASCII.GetBytes($"Customer: {ToSafeAscii(sale.CustomerName ?? "Walk-in")}\n"));
            if (!string.IsNullOrWhiteSpace(sale.Notes))
                bytes.AddRange(Encoding.ASCII.GetBytes($"Site/Ref: {ToSafeAscii(sale.Notes)}\n"));
            if (!string.IsNullOrWhiteSpace(sale.CreatedBy))
                bytes.AddRange(Encoding.ASCII.GetBytes($"Cashier:  {ToSafeAscii(sale.CreatedBy)}\n"));

            bytes.AddRange(Encoding.ASCII.GetBytes("------------------------------------------\n"));

            if (sale.SaleItems.Any())
            {
                bytes.AddRange(new byte[] { 0x1B, 0x45, 0x01 });
                bytes.AddRange(Encoding.ASCII.GetBytes("Qty        Item                  Total\n"));
                bytes.AddRange(new byte[] { 0x1B, 0x45, 0x00 });
                bytes.AddRange(Encoding.ASCII.GetBytes("------------------------------------------\n"));

                foreach (var item in sale.SaleItems)
                {
                    string unit = string.IsNullOrWhiteSpace(item.Unit) ? "" : $" {item.Unit}";
                    string qtyCol = $"{FormatQty(item.Quantity)}{unit}";
                    if (qtyCol.Length > 10) qtyCol = qtyCol[..10];
                    qtyCol = qtyCol.PadRight(11);

                    string safeName = ToSafeAscii(item.Name);
                    if (safeName.Length > 20) safeName = safeName[..20];
                    safeName = safeName.PadRight(21);

                    string totalCol = item.Price.ToString("N0").PadLeft(10);
                    bytes.AddRange(Encoding.ASCII.GetBytes($"{qtyCol}{safeName}{totalCol}\n"));

                    if (item.EffectiveUnitPrice > 0)
                    {
                        bytes.AddRange(Encoding.ASCII.GetBytes($"  @ Rs {item.EffectiveUnitPrice:N0}/{ToSafeAscii(item.Unit)}"));
                        if (item.Discount > 0)
                            bytes.AddRange(Encoding.ASCII.GetBytes($"  (Disc: {item.Discount:N0})"));
                        bytes.AddRange(Encoding.ASCII.GetBytes("\n"));
                    }

                    if (!string.IsNullOrWhiteSpace(item.SerialNumber) || !string.IsNullOrWhiteSpace(item.WarrantyPeriod))
                    {
                        string extra = string.Join(" | ", new[]
                        {
                            string.IsNullOrWhiteSpace(item.SerialNumber) ? null : $"S/N:{ToSafeAscii(item.SerialNumber)}",
                            string.IsNullOrWhiteSpace(item.WarrantyPeriod) ? null : $"War:{ToSafeAscii(item.WarrantyPeriod)}"
                        }.Where(x => x != null));
                        bytes.AddRange(Encoding.ASCII.GetBytes($"  [{extra}]\n"));
                    }
                }
                bytes.AddRange(Encoding.ASCII.GetBytes("------------------------------------------\n"));
            }

            // Summary
            bytes.AddRange(new byte[] { 0x1B, 0x61, 0x02 }); // Right
            if (sale.Discount > 0)
            {
                bytes.AddRange(Encoding.ASCII.GetBytes($"Bill Discount:   Rs {sale.Discount:N0}\n"));
            }

            if (sale.FbrTaxAmount > 0)
            {
                decimal exclTax = Math.Max(0m, sale.TotalAmount - sale.FbrTaxAmount);
                bytes.AddRange(Encoding.ASCII.GetBytes($"Excl. Tax Total: Rs {exclTax:N0}\n"));
                bytes.AddRange(Encoding.ASCII.GetBytes($"GST ({sale.FbrTaxRate:0.#}%):        Rs {sale.FbrTaxAmount:N0}\n"));
            }

            if (sale.RecordType != "Payment")
            {
                bytes.AddRange(Encoding.ASCII.GetBytes($"Bill Total:      Rs {sale.TotalAmount:N0}\n"));
            }

            if (sale.CustomerId != null)
            {
                bytes.AddRange(Encoding.ASCII.GetBytes($"Previous Dues:   Rs {sale.PreviousDues:N0}\n"));
                decimal netTotal = sale.RecordType == "Return"
                    ? sale.PreviousDues - sale.TotalAmount
                    : sale.PreviousDues + sale.TotalAmount;
                bytes.AddRange(Encoding.ASCII.GetBytes($"Net Balance:     Rs {netTotal:N0}\n"));
            }

            bytes.AddRange(new byte[] { 0x1B, 0x45, 0x01 });
            bytes.AddRange(Encoding.ASCII.GetBytes($"Paid ({ToSafeAscii(sale.PaymentMethod)}): Rs {sale.AmountPaid:N0}\n"));
            bytes.AddRange(new byte[] { 0x1B, 0x45, 0x00 });

            if (sale.ChangeReturned > 0)
            {
                bytes.AddRange(Encoding.ASCII.GetBytes($"Cash Tendered:   Rs {sale.CashTendered:N0}\n"));
                bytes.AddRange(Encoding.ASCII.GetBytes($"Change Return:   Rs {sale.ChangeReturned:N0}\n"));
            }

            if (sale.CustomerId != null)
            {
                bytes.AddRange(Encoding.ASCII.GetBytes($"Remaining Dues:  Rs {sale.BalanceDue:N0}\n"));
            }

            // FBR Fiscal Verification Section for ASCII Thermal
            if (!string.IsNullOrWhiteSpace(sale.FbrInvoiceNumber) || settings.IsFbrTaxEnabled)
            {
                bytes.AddRange(new byte[] { 0x1B, 0x61, 0x01 }); // Center
                bytes.AddRange(Encoding.ASCII.GetBytes("------------------------------------------\n"));
                bytes.AddRange(new byte[] { 0x1B, 0x45, 0x01 });
                bytes.AddRange(Encoding.ASCII.GetBytes("FBR VERIFIED FISCAL INVOICE\n"));
                bytes.AddRange(new byte[] { 0x1B, 0x45, 0x00 });
                bytes.AddRange(Encoding.ASCII.GetBytes($"POS ID: {ToSafeAscii(settings.FbrPosId)} | STRN: {ToSafeAscii(settings.FbrStrn)}\n"));
                if (!string.IsNullOrWhiteSpace(sale.FbrInvoiceNumber))
                {
                    bytes.AddRange(Encoding.ASCII.GetBytes($"FBR Inv #: {ToSafeAscii(sale.FbrInvoiceNumber)}\n"));
                    bytes.AddRange(Encoding.ASCII.GetBytes($"Status: {ToSafeAscii(sale.FbrStatus.ToUpperInvariant())}\n"));
                }
                bytes.AddRange(Encoding.ASCII.GetBytes(">> Verify via FBR Tax Asaan App <<\n"));
            }

            bytes.AddRange(Encoding.ASCII.GetBytes("\n"));

            // Footer
            bytes.AddRange(new byte[] { 0x1B, 0x61, 0x01 });
            bytes.AddRange(Encoding.ASCII.GetBytes("------------------------------------------\n"));
            if (!string.IsNullOrWhiteSpace(settings.ReceiptFooter))
                bytes.AddRange(Encoding.ASCII.GetBytes($"{ToSafeAscii(settings.ReceiptFooter)}\n"));

            bytes.AddRange(new byte[] { 0x0A, 0x0A, 0x0A, 0x0A, 0x0A });
            bytes.AddRange(new byte[] { 0x1D, 0x56, 0x00 });
            return bytes.ToArray();
        }

        private byte[] FormatBitmapReceipt(Sale sale, StoreSettings settings)
        {
            // Render crisp 576px (80mm) monochrome raster using WPF DrawingVisual so Unicode/Urdu shapes accurately
            const int width = 576;
            var lines = new List<(string text, double size, bool bold, TextAlignment align)>
            {
                (settings.ShopName, 24, true, TextAlignment.Center),
                (settings.Subtitle, 15, false, TextAlignment.Center),
                (settings.Address, 15, false, TextAlignment.Center),
                ($"Tel: {settings.Phone1} / {settings.Phone2}", 15, false, TextAlignment.Center),
                ("------------------------------------------------", 14, false, TextAlignment.Center),
                ($"#{sale.ReceiptNumber ?? sale.Id.ToString()}  |  {sale.RecordType.ToUpperInvariant()}  |  {sale.CreatedAt:dd/MM/yyyy HH:mm}", 14, true, TextAlignment.Center),
                ($"Customer: {sale.CustomerName ?? "Walk-in"}", 16, false, TextAlignment.Left)
            };

            if (!string.IsNullOrWhiteSpace(sale.Notes))
                lines.Add(($"Site/Ref: {sale.Notes}", 14, false, TextAlignment.Left));

            lines.Add(("------------------------------------------------", 14, false, TextAlignment.Center));

            foreach (var item in sale.SaleItems)
            {
                lines.Add(($"{item.Name}  ({FormatQty(item.Quantity)} {item.Unit} x {item.EffectiveUnitPrice:N0}) = Rs {item.Price:N0}", 15, false, TextAlignment.Left));
                if (!string.IsNullOrWhiteSpace(item.SerialNumber) || !string.IsNullOrWhiteSpace(item.WarrantyPeriod))
                {
                    lines.Add(($"   S/N: {item.SerialNumber ?? "-"} | Warranty: {item.WarrantyPeriod ?? "-"}", 13, false, TextAlignment.Left));
                }
            }

            lines.Add(("------------------------------------------------", 14, false, TextAlignment.Center));
            if (sale.Discount > 0) lines.Add(($"Discount: Rs {sale.Discount:N0}", 16, false, TextAlignment.Right));
            if (sale.FbrTaxAmount > 0)
            {
                decimal exclTax = Math.Max(0m, sale.TotalAmount - sale.FbrTaxAmount);
                lines.Add(($"Total Excl. Tax: Rs {exclTax:N0}", 15, false, TextAlignment.Right));
                lines.Add(($"GST ({sale.FbrTaxRate:0.#}%): Rs {sale.FbrTaxAmount:N0}", 15, false, TextAlignment.Right));
            }
            if (sale.RecordType != "Payment") lines.Add(($"Bill Total: Rs {sale.TotalAmount:N0}", 18, true, TextAlignment.Right));
            if (sale.CustomerId != null) lines.Add(($"Previous Dues: Rs {sale.PreviousDues:N0}", 16, false, TextAlignment.Right));
            lines.Add(($"Paid ({sale.PaymentMethod}): Rs {sale.AmountPaid:N0}", 18, true, TextAlignment.Right));
            if (sale.ChangeReturned > 0) lines.Add(($"Change: Rs {sale.ChangeReturned:N0}", 16, false, TextAlignment.Right));
            if (sale.CustomerId != null) lines.Add(($"Remaining Dues: Rs {sale.BalanceDue:N0}", 18, true, TextAlignment.Right));

            if (!string.IsNullOrWhiteSpace(sale.FbrInvoiceNumber) || settings.IsFbrTaxEnabled)
            {
                lines.Add(("------------------------------------------------", 14, false, TextAlignment.Center));
                lines.Add(("FBR POS VERIFIED FISCAL INVOICE", 15, true, TextAlignment.Center));
                lines.Add(($"POS ID: {settings.FbrPosId}   STRN: {settings.FbrStrn}", 13, false, TextAlignment.Center));
                if (!string.IsNullOrWhiteSpace(sale.FbrInvoiceNumber))
                {
                    lines.Add(($"FBR Inv #: {sale.FbrInvoiceNumber}", 14, true, TextAlignment.Center));
                    lines.Add(($"Fiscal Status: {sale.FbrStatus.ToUpperInvariant()}", 13, false, TextAlignment.Center));
                }
            }

            lines.Add(("", 12, false, TextAlignment.Center));
            lines.Add((settings.ReceiptFooter, 15, true, TextAlignment.Center));

            var formatted = new List<(FormattedText ft, TextAlignment align)>();
            double totalHeight = 16;
            var family = new FontFamily("Segoe UI, Urdu Typesetting, Arial");

            foreach (var l in lines)
            {
                var typeface = new Typeface(family, FontStyles.Normal, l.bold ? FontWeights.Bold : FontWeights.Normal, FontStretches.Normal);
                var ft = new FormattedText(
                    l.text ?? string.Empty,
                    CultureInfo.CurrentCulture,
                    FlowDirection.LeftToRight,
                    typeface,
                    l.size,
                    Brushes.Black,
                    1.0)
                {
                    MaxTextWidth = width - 24,
                    TextAlignment = l.align
                };
                formatted.Add((ft, l.align));
                totalHeight += ft.Height + 6;
            }

            var qrBmp = FbrTaxService.GenerateQrBitmap(sale.FbrQrCodeData, 3);
            if (qrBmp != null)
            {
                totalHeight += 165; // Extra space for QR code and verification label
            }

            int pixelHeight = Math.Max(64, (int)Math.Ceiling(totalHeight + 24));
            var dv = new DrawingVisual();
            using (var dc = dv.RenderOpen())
            {
                dc.DrawRectangle(Brushes.White, null, new Rect(0, 0, width, pixelHeight));
                double y = 12;
                foreach (var (ft, _) in formatted)
                {
                    dc.DrawText(ft, new Point(12, y));
                    y += ft.Height + 6;
                }

                if (qrBmp != null)
                {
                    double qrSize = 130;
                    double qrX = (width - qrSize) / 2.0;
                    dc.DrawImage(qrBmp, new Rect(qrX, y + 4, qrSize, qrSize));
                    y += qrSize + 8;

                    var verifyFt = new FormattedText(
                        "Verify via FBR Tax Asaan Mobile App",
                        CultureInfo.CurrentCulture,
                        FlowDirection.LeftToRight,
                        new Typeface(family, FontStyles.Italic, FontWeights.SemiBold, FontStretches.Normal),
                        13,
                        Brushes.Black,
                        1.0)
                    {
                        MaxTextWidth = width - 24,
                        TextAlignment = TextAlignment.Center
                    };
                    dc.DrawText(verifyFt, new Point(12, y));
                    y += verifyFt.Height + 8;
                }
            }

            var rtb = new RenderTargetBitmap(width, pixelHeight, 96, 96, PixelFormats.Pbgra32);
            rtb.Render(dv);

            int stride = width * 4;
            byte[] pixels = new byte[stride * pixelHeight];
            rtb.CopyPixels(pixels, stride, 0);

            int widthBytes = (width + 7) / 8;
            var escpos = new List<byte>();
            escpos.AddRange(new byte[] { 0x1B, 0x40 }); // Initialize
            // GS v 0 m xL xH yL yH d1..dk
            escpos.AddRange(new byte[]
            {
                0x1D, 0x76, 0x30, 0x00,
                (byte)(widthBytes & 0xFF), (byte)((widthBytes >> 8) & 0xFF),
                (byte)(pixelHeight & 0xFF), (byte)((pixelHeight >> 8) & 0xFF)
            });

            for (int y = 0; y < pixelHeight; y++)
            {
                for (int xByte = 0; xByte < widthBytes; xByte++)
                {
                    byte b = 0;
                    for (int bit = 0; bit < 8; bit++)
                    {
                        int x = xByte * 8 + bit;
                        if (x < width)
                        {
                            int idx = y * stride + x * 4;
                            int luma = (pixels[idx + 2] * 30 + pixels[idx + 1] * 59 + pixels[idx] * 11) / 100;
                            if (luma < 140)
                            {
                                b |= (byte)(1 << (7 - bit));
                            }
                        }
                    }
                    escpos.Add(b);
                }
            }

            escpos.AddRange(new byte[] { 0x0A, 0x0A, 0x0A, 0x0A });
            escpos.AddRange(new byte[] { 0x1D, 0x56, 0x00 });
            return escpos.ToArray();
        }

        private void AddLetterheadHeader(FlowDocument doc, StoreSettings settings)
        {
            // Outer Table for 2-column Letterhead matching the physical pad
            var hdrTable = new Table { CellSpacing = 0, Margin = new Thickness(0, 0, 0, 6) };
            hdrTable.Columns.Add(new TableColumn { Width = new GridLength(3.2, GridUnitType.Star) }); // Left branding
            hdrTable.Columns.Add(new TableColumn { Width = new GridLength(2.2, GridUnitType.Star) }); // Right contact details

            var rowGroup = new TableRowGroup();
            var row = new TableRow();

            // Left Cell: Brand Name + Tagline matching pad
            var leftPara = new Paragraph { Margin = new Thickness(0) };

            // Brand Title (Large Deep Navy/Royal Blue matching pad)
            leftPara.Inlines.Add(new Run("Al Kuwait Electronics")
            {
                FontSize = 24,
                FontWeight = FontWeights.Black,
                Foreground = new SolidColorBrush(Color.FromRgb(11, 79, 156))
            });
            leftPara.Inlines.Add(new LineBreak());

            // Center / Sub-brand
            if (!string.IsNullOrWhiteSpace(settings.ShopName) && settings.ShopName.Contains("Aizaz Fareed", StringComparison.OrdinalIgnoreCase))
            {
                leftPara.Inlines.Add(new Run("Aizaz Fareed Center")
                {
                    FontSize = 12.5,
                    FontWeight = FontWeights.Bold,
                    Foreground = new SolidColorBrush(Color.FromRgb(30, 41, 59))
                });
                leftPara.Inlines.Add(new LineBreak());
            }

            // Tagline matching pad: "— QUALITY PRODUCTS | BETTER LIVING —"
            leftPara.Inlines.Add(new Run("— QUALITY PRODUCTS | BETTER LIVING —")
            {
                FontSize = 9.5,
                FontWeight = FontWeights.Bold,
                Foreground = new SolidColorBrush(Color.FromRgb(2, 132, 199)) // Cyan blue
            });

            if (!string.IsNullOrWhiteSpace(settings.Subtitle))
            {
                leftPara.Inlines.Add(new LineBreak());
                leftPara.Inlines.Add(new Run(settings.Subtitle)
                {
                    FontSize = 8.5,
                    Foreground = new SolidColorBrush(Color.FromRgb(100, 116, 139))
                });
            }

            var leftCell = new TableCell(leftPara);
            row.Cells.Add(leftCell);

            // Right Cell: Location, Proprietor, Phones, Email (matching pad)
            var rightPara = new Paragraph { TextAlignment = TextAlignment.Right, Margin = new Thickness(0), LineHeight = 16 };

            rightPara.Inlines.Add(new Run("📍 Opposite Shabbir Shaheed Hospital, Kunjah, Gujrat")
            {
                FontSize = 9.5,
                FontWeight = FontWeights.SemiBold,
                Foreground = new SolidColorBrush(Color.FromRgb(51, 65, 85))
            });
            rightPara.Inlines.Add(new LineBreak());

            rightPara.Inlines.Add(new Run("👤 Proprietor: Muhammad Ihsan")
            {
                FontSize = 9.5,
                FontWeight = FontWeights.Bold,
                Foreground = new SolidColorBrush(Color.FromRgb(15, 23, 42))
            });
            rightPara.Inlines.Add(new LineBreak());

            string phoneDisplay = !string.IsNullOrWhiteSpace(settings.Phone2)
                ? $"{settings.Phone2}  /  {settings.Phone1}"
                : "0327-5898969  /  0336-4994843";

            rightPara.Inlines.Add(new Run($"📞 {phoneDisplay}")
            {
                FontSize = 9.5,
                FontWeight = FontWeights.Bold,
                Foreground = new SolidColorBrush(Color.FromRgb(11, 79, 156))
            });
            rightPara.Inlines.Add(new LineBreak());

            rightPara.Inlines.Add(new Run("✉️ muhammadihsan19852020@gmail.com")
            {
                FontSize = 9,
                FontWeight = FontWeights.Normal,
                Foreground = new SolidColorBrush(Color.FromRgb(37, 99, 235))
            });

            var rightCell = new TableCell(rightPara);
            row.Cells.Add(rightCell);

            rowGroup.Rows.Add(row);
            hdrTable.RowGroups.Add(rowGroup);
            doc.Blocks.Add(hdrTable);

            // Decorative Ribbon Bar matching the curved waves on the pad
            var ribbonTable = new Table { CellSpacing = 0, Margin = new Thickness(0, 0, 0, 14) };
            ribbonTable.Columns.Add(new TableColumn { Width = new GridLength(1, GridUnitType.Star) });
            var ribbonRg = new TableRowGroup();

            // Primary deep blue ribbon
            var ribbonRow1 = new TableRow { Background = new SolidColorBrush(Color.FromRgb(11, 79, 156)) };
            ribbonRow1.Cells.Add(new TableCell(new Paragraph()) { LineHeight = 4, Padding = new Thickness(0) });
            ribbonRg.Rows.Add(ribbonRow1);

            // Cyan accent ribbon
            var ribbonRow2 = new TableRow { Background = new SolidColorBrush(Color.FromRgb(2, 132, 199)) };
            ribbonRow2.Cells.Add(new TableCell(new Paragraph()) { LineHeight = 2, Padding = new Thickness(0) });
            ribbonRg.Rows.Add(ribbonRow2);

            ribbonTable.RowGroups.Add(ribbonRg);
            doc.Blocks.Add(ribbonTable);
        }

        private void AddLetterheadFooter(FlowDocument doc, StoreSettings settings)
        {
            // Bottom Signatures Block
            var sigTable = new Table { CellSpacing = 0, Margin = new Thickness(0, 32, 0, 16) };
            sigTable.Columns.Add(new TableColumn { Width = new GridLength(1, GridUnitType.Star) });
            sigTable.Columns.Add(new TableColumn { Width = new GridLength(1, GridUnitType.Star) });
            var sigGroup = new TableRowGroup();
            var sigRow = new TableRow();

            var leftCell = new TableCell(new Paragraph(new Run("Customer / Depositor Signature:\n\n____________________________________\nDate: ________________________"))
            {
                FontSize = 9.5,
                Foreground = new SolidColorBrush(Color.FromRgb(71, 85, 105))
            });

            var rightCell = new TableCell(new Paragraph(new Run("For Al-Kuwait Electronics:\n\n____________________________________\nAuthorized Signature & Stamp"))
            {
                FontSize = 9.5,
                TextAlignment = TextAlignment.Right,
                Foreground = new SolidColorBrush(Color.FromRgb(71, 85, 105))
            });

            sigRow.Cells.Add(leftCell);
            sigRow.Cells.Add(rightCell);
            sigGroup.Rows.Add(sigRow);
            sigTable.RowGroups.Add(sigGroup);
            doc.Blocks.Add(sigTable);

            // Bottom decorative ribbon
            var bottomRibbon = new Table { CellSpacing = 0, Margin = new Thickness(0, 0, 0, 8) };
            bottomRibbon.Columns.Add(new TableColumn { Width = new GridLength(1, GridUnitType.Star) });
            var bRg = new TableRowGroup();
            var bRow = new TableRow { Background = new SolidColorBrush(Color.FromRgb(11, 79, 156)) };
            bRow.Cells.Add(new TableCell(new Paragraph()) { LineHeight = 2, Padding = new Thickness(0) });
            bRg.Rows.Add(bRow);
            bottomRibbon.RowGroups.Add(bRg);
            doc.Blocks.Add(bottomRibbon);

            // Bottom Footer Row matching pad:
            // Left: Notice / Verification. Right: "Your Trusted Electronics Partner" in italic script
            var ftrTable = new Table { CellSpacing = 0, Margin = new Thickness(0, 4, 0, 0) };
            ftrTable.Columns.Add(new TableColumn { Width = new GridLength(1, GridUnitType.Star) });
            ftrTable.Columns.Add(new TableColumn { Width = new GridLength(1, GridUnitType.Star) });
            var ftrGroup = new TableRowGroup();
            var ftrRow = new TableRow();

            var ftrLeft = new TableCell(new Paragraph(new Run("Thank you for your business! · Computer generated receipt."))
            {
                FontSize = 9,
                Foreground = new SolidColorBrush(Color.FromRgb(100, 116, 139))
            });

            var ftrRight = new TableCell(new Paragraph(new Run("Your Trusted Electronics Partner"))
            {
                FontSize = 11,
                FontWeight = FontWeights.Bold,
                FontStyle = FontStyles.Italic,
                TextAlignment = TextAlignment.Right,
                Foreground = new SolidColorBrush(Color.FromRgb(11, 79, 156)) // Matching exact blue on pad!
            });

            ftrRow.Cells.Add(ftrLeft);
            ftrRow.Cells.Add(ftrRight);
            ftrGroup.Rows.Add(ftrRow);
            ftrTable.RowGroups.Add(ftrGroup);
            doc.Blocks.Add(ftrTable);
        }

        private FlowDocument BuildA4InvoiceDocument(Sale sale, StoreSettings settings)
        {
            var doc = new FlowDocument
            {
                FontFamily = new FontFamily("Segoe UI"),
                FontSize = 11.5,
                Foreground = new SolidColorBrush(Color.FromRgb(15, 23, 42))
            };

            // 1. Store Letterhead Header matching the physical pad
            AddLetterheadHeader(doc, settings);

            // 2. Document Title Banner Table
            var titleTable = new Table { CellSpacing = 0, Margin = new Thickness(0, 0, 0, 12) };
            titleTable.Columns.Add(new TableColumn { Width = new GridLength(2.2, GridUnitType.Star) });
            titleTable.Columns.Add(new TableColumn { Width = new GridLength(0.8, GridUnitType.Star) });

            var titleRg = new TableRowGroup();
            var titleRow = new TableRow();

            var (titleText, titleSub, bgBrush, fgBrush, borderBrush) = sale.RecordType.ToUpperInvariant() switch
            {
                "PAYMENT" => ("OFFICIAL PAYMENT RECEIPT / WASOOLI VOUCHER", "ACKNOWLEDGEMENT OF PAYMENT & KHATA RECOVERY",
                              Color.FromRgb(236, 253, 245), Color.FromRgb(6, 95, 70), Color.FromRgb(16, 185, 129)),
                "ESTIMATE" => ("QUOTATION / ESTIMATE BILL", "PRICE ESTIMATE · NOT AN OFFICIAL FISCAL INVOICE",
                               Color.FromRgb(255, 251, 235), Color.FromRgb(146, 64, 14), Color.FromRgb(245, 158, 11)),
                "RETURN" => ("SALES RETURN CREDIT NOTE", "ITEM RETURN / REFUND VOUCHER",
                             Color.FromRgb(255, 241, 242), Color.FromRgb(159, 18, 57), Color.FromRgb(244, 63, 94)),
                _ => ("SALE TAX INVOICE / CASH MEMO", "ORIGINAL CUSTOMER TAX INVOICE",
                      Color.FromRgb(239, 246, 255), Color.FromRgb(30, 64, 175), Color.FromRgb(59, 130, 246))
            };

            var tLeftPara = new Paragraph { Margin = new Thickness(12, 7, 6, 6) };
            tLeftPara.Inlines.Add(new Run(titleText) { FontSize = 13, FontWeight = FontWeights.Black, Foreground = new SolidColorBrush(fgBrush) });
            tLeftPara.Inlines.Add(new LineBreak());
            tLeftPara.Inlines.Add(new Run(titleSub) { FontSize = 8.5, FontWeight = FontWeights.SemiBold, Foreground = new SolidColorBrush(fgBrush) });

            var tLeft = new TableCell(tLeftPara)
            {
                Background = new SolidColorBrush(bgBrush),
                BorderBrush = new SolidColorBrush(borderBrush),
                BorderThickness = new Thickness(1, 1, 0, 1)
            };

            var tRightPara = new Paragraph(new Run("ORIGINAL") { FontSize = 11, FontWeight = FontWeights.Black, Foreground = new SolidColorBrush(fgBrush) })
            {
                Margin = new Thickness(6, 10, 12, 10),
                TextAlignment = TextAlignment.Right
            };

            var tRight = new TableCell(tRightPara)
            {
                Background = new SolidColorBrush(bgBrush),
                BorderBrush = new SolidColorBrush(borderBrush),
                BorderThickness = new Thickness(0, 1, 1, 1)
            };

            titleRow.Cells.Add(tLeft);
            titleRow.Cells.Add(tRight);
            titleRg.Rows.Add(titleRow);
            titleTable.RowGroups.Add(titleRg);
            doc.Blocks.Add(titleTable);

            // 3. Structured 2-Column Information Cards (Customer on Left, Voucher details on Right)
            var infoTable = new Table { CellSpacing = 12, Margin = new Thickness(0, 0, 0, 14) };
            infoTable.Columns.Add(new TableColumn { Width = new GridLength(1.15, GridUnitType.Star) }); // Payer card
            infoTable.Columns.Add(new TableColumn { Width = new GridLength(0.85, GridUnitType.Star) }); // Voucher card

            var infoGroup = new TableRowGroup();
            var infoRow = new TableRow();

            // Payer Card
            string customerName = sale.CustomerName ?? "Walk-in Customer";
            var payerPara = new Paragraph { Margin = new Thickness(12, 10, 12, 10) };
            payerPara.Inlines.Add(new Run("CUSTOMER / PAYER DETAILS\n")
            {
                FontSize = 9,
                FontWeight = FontWeights.Bold,
                Foreground = new SolidColorBrush(Color.FromRgb(100, 116, 139))
            });
            payerPara.Inlines.Add(new Run(customerName)
            {
                FontSize = 14,
                FontWeight = FontWeights.Black,
                Foreground = new SolidColorBrush(Color.FromRgb(15, 23, 42))
            });
            payerPara.Inlines.Add(new LineBreak());

            if (!string.IsNullOrWhiteSpace(sale.Customer?.Phone))
            {
                payerPara.Inlines.Add(new Run($"Contact Phone: {sale.Customer.Phone}\n") { FontSize = 10, Foreground = new SolidColorBrush(Color.FromRgb(51, 65, 85)) });
            }
            if (!string.IsNullOrWhiteSpace(sale.Notes))
            {
                payerPara.Inlines.Add(new Run($"Reference / Contract: {sale.Notes}\n") { FontSize = 10, FontWeight = FontWeights.SemiBold, Foreground = new SolidColorBrush(Color.FromRgb(2, 132, 199)) });
            }
            if (!string.IsNullOrWhiteSpace(sale.Customer?.Address))
            {
                payerPara.Inlines.Add(new Run($"Address: {sale.Customer.Address}\n") { FontSize = 9.5, Foreground = new SolidColorBrush(Color.FromRgb(100, 116, 139)) });
            }

            var payerCell = new TableCell(payerPara)
            {
                Background = new SolidColorBrush(Color.FromRgb(248, 250, 252)),
                BorderBrush = new SolidColorBrush(Color.FromRgb(226, 232, 240)),
                BorderThickness = new Thickness(1)
            };
            infoRow.Cells.Add(payerCell);

            // Voucher Details Card
            var vchPara = new Paragraph { Margin = new Thickness(12, 10, 12, 10) };
            vchPara.Inlines.Add(new Run("VOUCHER SPECIFICATIONS\n")
            {
                FontSize = 9,
                FontWeight = FontWeights.Bold,
                Foreground = new SolidColorBrush(Color.FromRgb(100, 116, 139))
            });

            vchPara.Inlines.Add(new Run($"Receipt #: #{sale.ReceiptNumber ?? sale.Id.ToString()}\n")
            {
                FontSize = 11,
                FontWeight = FontWeights.Bold,
                Foreground = new SolidColorBrush(Color.FromRgb(11, 79, 156))
            });
            vchPara.Inlines.Add(new Run($"Issue Date: {sale.CreatedAt:dd MMM yyyy, hh:mm tt}\n")
            {
                FontSize = 10,
                Foreground = new SolidColorBrush(Color.FromRgb(51, 65, 85))
            });
            vchPara.Inlines.Add(new Run($"Payment Method: {sale.PaymentMethod}\n")
            {
                FontSize = 10,
                FontWeight = FontWeights.SemiBold,
                Foreground = new SolidColorBrush(Color.FromRgb(5, 150, 105))
            });
            if (!string.IsNullOrWhiteSpace(sale.CreatedBy))
            {
                vchPara.Inlines.Add(new Run($"Received / Handled By: {sale.CreatedBy}")
                {
                    FontSize = 10,
                    Foreground = new SolidColorBrush(Color.FromRgb(71, 85, 105))
                });
            }

            var vchCell = new TableCell(vchPara)
            {
                Background = new SolidColorBrush(Color.FromRgb(248, 250, 252)),
                BorderBrush = new SolidColorBrush(Color.FromRgb(226, 232, 240)),
                BorderThickness = new Thickness(1)
            };
            infoRow.Cells.Add(vchCell);

            infoGroup.Rows.Add(infoRow);
            infoTable.RowGroups.Add(infoGroup);
            doc.Blocks.Add(infoTable);

            // 4. FBR Fiscal Strip (if enabled or fiscalized)
            if (!string.IsNullOrWhiteSpace(sale.FbrInvoiceNumber) || settings.IsFbrTaxEnabled)
            {
                var fbrBanner = new Paragraph { Margin = new Thickness(0, 0, 0, 10), Background = new SolidColorBrush(Color.FromRgb(240, 253, 244)), Padding = new Thickness(10, 6, 10, 6) };
                fbrBanner.Inlines.Add(new Run("FBR TIER-1 FISCALIZED INVOICE   ") { FontWeight = FontWeights.Bold, Foreground = new SolidColorBrush(Color.FromRgb(16, 185, 129)), FontSize = 12 });
                fbrBanner.Inlines.Add(new Run($"POS ID: {settings.FbrPosId}   |   STRN: {settings.FbrStrn}   |   NTN: {settings.FbrNtn}"));
                if (!string.IsNullOrWhiteSpace(sale.FbrInvoiceNumber))
                {
                    fbrBanner.Inlines.Add(new LineBreak());
                    fbrBanner.Inlines.Add(new Run($"FBR Invoice #: {sale.FbrInvoiceNumber}   |   Fiscal Status: {sale.FbrStatus}") { FontWeight = FontWeights.SemiBold });
                }
                doc.Blocks.Add(fbrBanner);
            }

            // 5. Main Body Presentation:
            // Case A: Payment Receipts (Wasooli)
            if (string.Equals(sale.RecordType, "Payment", StringComparison.OrdinalIgnoreCase))
            {
                // Professional Payment Settlement Table
                var payTable = new Table { CellSpacing = 0, BorderBrush = new SolidColorBrush(Color.FromRgb(203, 213, 225)), BorderThickness = new Thickness(1), Margin = new Thickness(0, 0, 0, 14) };
                payTable.Columns.Add(new TableColumn { Width = new GridLength(3.2, GridUnitType.Star) }); // Description
                payTable.Columns.Add(new TableColumn { Width = new GridLength(1.3, GridUnitType.Star) }); // Mode
                payTable.Columns.Add(new TableColumn { Width = new GridLength(1.6, GridUnitType.Star) }); // Prev Dues
                payTable.Columns.Add(new TableColumn { Width = new GridLength(1.8, GridUnitType.Star) }); // Amount Paid
                payTable.Columns.Add(new TableColumn { Width = new GridLength(1.8, GridUnitType.Star) }); // Remaining Balance

                var payRg = new TableRowGroup();
                var hdrRow = new TableRow { Background = new SolidColorBrush(Color.FromRgb(11, 79, 156)) };
                foreach (var h in new[] { "PARTICULARS / TRANSACTION DESCRIPTION", "PAYMENT MODE", "PREVIOUS DUES", "AMOUNT RECEIVED", "REMAINING BALANCE" })
                {
                    hdrRow.Cells.Add(CreateCell(h, FontWeights.Bold, h == "PARTICULARS / TRANSACTION DESCRIPTION" || h == "PAYMENT MODE" ? TextAlignment.Left : TextAlignment.Right, Brushes.White, null, null, 10));
                }
                payRg.Rows.Add(hdrRow);

                var pRow = new TableRow();
                string noteDesc = string.IsNullOrWhiteSpace(sale.Notes) ? "Installment Payment Wasooli / Khata Credit" : $"Payment Received · {sale.Notes}";
                pRow.Cells.Add(CreateCell(noteDesc, FontWeights.SemiBold, TextAlignment.Left));
                pRow.Cells.Add(CreateCell(sale.PaymentMethod, FontWeights.Normal, TextAlignment.Left));
                pRow.Cells.Add(CreateCell($"Rs {sale.PreviousDues:N0}", FontWeights.Normal, TextAlignment.Right));
                pRow.Cells.Add(CreateCell($"Rs {sale.AmountPaid:N0}", FontWeights.Black, TextAlignment.Right, new SolidColorBrush(Color.FromRgb(5, 150, 105)), new SolidColorBrush(Color.FromRgb(236, 253, 245)), null, 13));
                pRow.Cells.Add(CreateCell($"Rs {sale.BalanceDue:N0}", FontWeights.Black, TextAlignment.Right, new SolidColorBrush(Color.FromRgb(11, 79, 156)), null, null, 13));
                payRg.Rows.Add(pRow);

                payTable.RowGroups.Add(payRg);
                doc.Blocks.Add(payTable);

                // Settlement & Amount In Words Cards
                var setTable = new Table { CellSpacing = 12, Margin = new Thickness(0, 0, 0, 16) };
                setTable.Columns.Add(new TableColumn { Width = new GridLength(1.2, GridUnitType.Star) });
                setTable.Columns.Add(new TableColumn { Width = new GridLength(1, GridUnitType.Star) });

                var setRg = new TableRowGroup();
                var setRow = new TableRow();

                // Left: Amount in Words + Verification Badge
                var wPara = new Paragraph { Margin = new Thickness(12, 10, 12, 10) };
                wPara.Inlines.Add(new Run("AMOUNT RECEIVED IN WORDS:\n") { FontSize = 9, FontWeight = FontWeights.Bold, Foreground = new SolidColorBrush(Color.FromRgb(100, 116, 139)) });
                wPara.Inlines.Add(new Run(NumberToWords(sale.AmountPaid) + "\n\n") { FontSize = 12, FontWeight = FontWeights.Black, Foreground = new SolidColorBrush(Color.FromRgb(15, 23, 42)) });
                wPara.Inlines.Add(new Run("● PAYMENT RECEIVED AND CREDITED TO CUSTOMER KHATA\nOfficial verification stamp of Al-Kuwait Electronics")
                {
                    FontSize = 9.5,
                    FontWeight = FontWeights.SemiBold,
                    Foreground = new SolidColorBrush(Color.FromRgb(5, 150, 105))
                });

                var wCell = new TableCell(wPara)
                {
                    Background = new SolidColorBrush(Color.FromRgb(240, 253, 244)),
                    BorderBrush = new SolidColorBrush(Color.FromRgb(187, 247, 208)),
                    BorderThickness = new Thickness(1)
                };
                setRow.Cells.Add(wCell);

                // Right: Financial Ledger Snapshot Box
                var snapPara = new Paragraph { Margin = new Thickness(12, 10, 12, 10), TextAlignment = TextAlignment.Right };
                snapPara.Inlines.Add(new Run($"Previous Ledger Dues : Rs {sale.PreviousDues:N0}\n") { FontSize = 11, Foreground = new SolidColorBrush(Color.FromRgb(71, 85, 105)) });
                snapPara.Inlines.Add(new Run($"Amount Paid (Received): - Rs {sale.AmountPaid:N0}\n") { FontSize = 12, FontWeight = FontWeights.Bold, Foreground = new SolidColorBrush(Color.FromRgb(5, 150, 105)) });
                snapPara.Inlines.Add(new Run("───────────────────────────────\n") { Foreground = new SolidColorBrush(Color.FromRgb(203, 213, 225)) });
                snapPara.Inlines.Add(new Run("Remaining Khata Balance:\n") { FontSize = 9.5, FontWeight = FontWeights.Bold, Foreground = new SolidColorBrush(Color.FromRgb(100, 116, 139)) });
                snapPara.Inlines.Add(new Run($"Rs {sale.BalanceDue:N0}") { FontSize = 17, FontWeight = FontWeights.Black, Foreground = new SolidColorBrush(Color.FromRgb(11, 79, 156)) });

                var snapCell = new TableCell(snapPara)
                {
                    Background = new SolidColorBrush(Color.FromRgb(248, 250, 252)),
                    BorderBrush = new SolidColorBrush(Color.FromRgb(226, 232, 240)),
                    BorderThickness = new Thickness(1)
                };
                setRow.Cells.Add(snapCell);

                setRg.Rows.Add(setRow);
                setTable.RowGroups.Add(setRg);
                doc.Blocks.Add(setTable);
            }
            // Case B: Regular Sales, Quotations, and Returns
            else
            {
                if (sale.SaleItems.Any())
                {
                    var table = new Table { CellSpacing = 0, BorderBrush = new SolidColorBrush(Color.FromRgb(203, 213, 225)), BorderThickness = new Thickness(1), Margin = new Thickness(0, 0, 0, 14) };
                    table.Columns.Add(new TableColumn { Width = new GridLength(0.5, GridUnitType.Star) }); // #
                    table.Columns.Add(new TableColumn { Width = new GridLength(3.2, GridUnitType.Star) }); // Description
                    table.Columns.Add(new TableColumn { Width = new GridLength(1.1, GridUnitType.Star) }); // Qty
                    table.Columns.Add(new TableColumn { Width = new GridLength(1.2, GridUnitType.Star) }); // Rate
                    table.Columns.Add(new TableColumn { Width = new GridLength(1.0, GridUnitType.Star) }); // Disc
                    table.Columns.Add(new TableColumn { Width = new GridLength(1.4, GridUnitType.Star) }); // Amount

                    var rg = new TableRowGroup();
                    var hdrRow = new TableRow { Background = new SolidColorBrush(Color.FromRgb(11, 79, 156)) };
                    hdrRow.Cells.Add(CreateCell("#", FontWeights.Bold, TextAlignment.Center, Brushes.White, null, null, 10));
                    hdrRow.Cells.Add(CreateCell("ITEM DESCRIPTION / SERIAL / WARRANTY", FontWeights.Bold, TextAlignment.Left, Brushes.White, null, null, 10));
                    hdrRow.Cells.Add(CreateCell("QTY", FontWeights.Bold, TextAlignment.Right, Brushes.White, null, null, 10));
                    hdrRow.Cells.Add(CreateCell("RATE", FontWeights.Bold, TextAlignment.Right, Brushes.White, null, null, 10));
                    hdrRow.Cells.Add(CreateCell("DISC", FontWeights.Bold, TextAlignment.Right, Brushes.White, null, null, 10));
                    hdrRow.Cells.Add(CreateCell("TOTAL (PKR)", FontWeights.Bold, TextAlignment.Right, Brushes.White, null, null, 10));
                    rg.Rows.Add(hdrRow);

                    int itemIndex = 1;
                    foreach (var item in sale.SaleItems)
                    {
                        var row = new TableRow();
                        var desc = new StringBuilder(item.Name);
                        if (!string.IsNullOrWhiteSpace(item.SerialNumber)) desc.Append($"\nS/N: {item.SerialNumber}");
                        if (!string.IsNullOrWhiteSpace(item.WarrantyPeriod)) desc.Append($"  |  Warranty: {item.WarrantyPeriod}");

                        row.Cells.Add(CreateCell((itemIndex++).ToString(), FontWeights.Normal, TextAlignment.Center));
                        row.Cells.Add(CreateCell(desc.ToString(), FontWeights.Normal, TextAlignment.Left));
                        row.Cells.Add(CreateCell($"{FormatQty(item.Quantity)} {item.Unit}", FontWeights.Normal, TextAlignment.Right));
                        row.Cells.Add(CreateCell($"Rs {item.EffectiveUnitPrice:N0}", FontWeights.Normal, TextAlignment.Right));
                        row.Cells.Add(CreateCell(item.Discount > 0 ? $"Rs {item.Discount:N0}" : "-", FontWeights.Normal, TextAlignment.Right));
                        row.Cells.Add(CreateCell($"Rs {item.Price:N0}", FontWeights.SemiBold, TextAlignment.Right));
                        rg.Rows.Add(row);
                    }

                    table.RowGroups.Add(rg);
                    doc.Blocks.Add(table);
                }

                // Summary Block
                var sumTable = new Table { CellSpacing = 12, Margin = new Thickness(0, 0, 0, 16) };
                sumTable.Columns.Add(new TableColumn { Width = new GridLength(1.2, GridUnitType.Star) });
                sumTable.Columns.Add(new TableColumn { Width = new GridLength(1, GridUnitType.Star) });

                var sumRg = new TableRowGroup();
                var sumRow = new TableRow();

                // Left: Amount in Words
                var leftSumPara = new Paragraph { Margin = new Thickness(12, 10, 12, 10) };
                leftSumPara.Inlines.Add(new Run("TOTAL AMOUNT IN WORDS:\n") { FontSize = 9, FontWeight = FontWeights.Bold, Foreground = new SolidColorBrush(Color.FromRgb(100, 116, 139)) });
                leftSumPara.Inlines.Add(new Run(NumberToWords(sale.TotalAmount) + "\n\n") { FontSize = 11.5, FontWeight = FontWeights.Black, Foreground = new SolidColorBrush(Color.FromRgb(15, 23, 42)) });
                leftSumPara.Inlines.Add(new Run("Thank you for shopping at Al-Kuwait Electronics!\nGoods once sold will not be returned without warranty card.") { FontSize = 9, Foreground = new SolidColorBrush(Color.FromRgb(100, 116, 139)) });

                var leftSumCell = new TableCell(leftSumPara)
                {
                    Background = new SolidColorBrush(Color.FromRgb(248, 250, 252)),
                    BorderBrush = new SolidColorBrush(Color.FromRgb(226, 232, 240)),
                    BorderThickness = new Thickness(1)
                };
                sumRow.Cells.Add(leftSumCell);

                // Right: Calculation Summary Box
                var rightSumPara = new Paragraph { Margin = new Thickness(12, 10, 12, 10), TextAlignment = TextAlignment.Right };
                if (sale.Discount > 0)
                {
                    rightSumPara.Inlines.Add(new Run($"Bill Discount: Rs {sale.Discount:N0}\n") { FontSize = 10.5, Foreground = new SolidColorBrush(Color.FromRgb(220, 38, 38)) });
                }
                if (sale.FbrTaxAmount > 0)
                {
                    decimal exclTax = Math.Max(0m, sale.TotalAmount - sale.FbrTaxAmount);
                    rightSumPara.Inlines.Add(new Run($"Taxable Amount: Rs {exclTax:N0}\n") { FontSize = 10, Foreground = new SolidColorBrush(Color.FromRgb(71, 85, 105)) });
                    rightSumPara.Inlines.Add(new Run($"Sales Tax ({sale.FbrTaxRate:0.#}%): Rs {sale.FbrTaxAmount:N0}\n") { FontSize = 10, Foreground = new SolidColorBrush(Color.FromRgb(71, 85, 105)) });
                }
                rightSumPara.Inlines.Add(new Run($"Bill Total: Rs {sale.TotalAmount:N0}\n") { FontSize = 14, FontWeight = FontWeights.Black, Foreground = new SolidColorBrush(Color.FromRgb(15, 23, 42)) });
                if (sale.CustomerId != null)
                {
                    rightSumPara.Inlines.Add(new Run($"Previous Dues: Rs {sale.PreviousDues:N0}\n") { FontSize = 10, Foreground = new SolidColorBrush(Color.FromRgb(71, 85, 105)) });
                }
                rightSumPara.Inlines.Add(new Run($"Amount Paid: Rs {sale.AmountPaid:N0}\n") { FontSize = 12, FontWeight = FontWeights.Bold, Foreground = new SolidColorBrush(Color.FromRgb(5, 150, 105)) });
                if (sale.ChangeReturned > 0)
                {
                    rightSumPara.Inlines.Add(new Run($"Cash Received: Rs {sale.CashTendered:N0}  |  Change: Rs {sale.ChangeReturned:N0}\n") { FontSize = 9.5, Foreground = new SolidColorBrush(Color.FromRgb(100, 116, 139)) });
                }
                if (sale.CustomerId != null)
                {
                    rightSumPara.Inlines.Add(new Run("───────────────────────────────\n") { Foreground = new SolidColorBrush(Color.FromRgb(203, 213, 225)) });
                    rightSumPara.Inlines.Add(new Run($"Remaining Khata Balance: Rs {sale.BalanceDue:N0}") { FontSize = 13, FontWeight = FontWeights.Black, Foreground = new SolidColorBrush(Color.FromRgb(11, 79, 156)) });
                }

                var rightSumCell = new TableCell(rightSumPara)
                {
                    Background = new SolidColorBrush(Color.FromRgb(248, 250, 252)),
                    BorderBrush = new SolidColorBrush(Color.FromRgb(226, 232, 240)),
                    BorderThickness = new Thickness(1)
                };
                sumRow.Cells.Add(rightSumCell);

                sumRg.Rows.Add(sumRow);
                sumTable.RowGroups.Add(sumRg);
                doc.Blocks.Add(sumTable);
            }

            // 6. Signatures and Pad Footer
            AddLetterheadFooter(doc, settings);

            if (!string.IsNullOrWhiteSpace(sale.FbrQrCodeData))
            {
                var qrBmp = FbrTaxService.GenerateQrBitmap(sale.FbrQrCodeData, 4);
                if (qrBmp != null)
                {
                    var img = new Image { Source = qrBmp, Width = 110, Height = 110, HorizontalAlignment = HorizontalAlignment.Center };
                    doc.Blocks.Add(new BlockUIContainer(img) { Margin = new Thickness(0, 14, 0, 4) });
                    doc.Blocks.Add(new Paragraph(new Run("Scan QR code via the FBR Tax Asaan mobile app for official fiscal verification"))
                    {
                        FontSize = 9,
                        FontStyle = FontStyles.Italic,
                        TextAlignment = TextAlignment.Center,
                        Foreground = new SolidColorBrush(Color.FromRgb(100, 116, 139)),
                        Margin = new Thickness(0, 0, 0, 8)
                    });
                }
            }

            return doc;
        }

        private FlowDocument BuildCustomerLedgerDocument(Customer customer, IEnumerable<Sale> history, StoreSettings settings)
        {
            var doc = new FlowDocument
            {
                FontFamily = new FontFamily("Segoe UI"),
                FontSize = 11.5,
                Foreground = new SolidColorBrush(Color.FromRgb(15, 23, 42))
            };

            AddLetterheadHeader(doc, settings);

            // Statement Title Banner
            var titlePara = new Paragraph { Margin = new Thickness(0, 0, 0, 12), Background = new SolidColorBrush(Color.FromRgb(239, 246, 255)), Padding = new Thickness(12, 8, 12, 8) };
            titlePara.Inlines.Add(new Run("CUSTOMER KHATA STATEMENT · LEDGER ACCOUNT") { FontSize = 13, FontWeight = FontWeights.Black, Foreground = new SolidColorBrush(Color.FromRgb(11, 79, 156)) });
            titlePara.Inlines.Add(new LineBreak());
            titlePara.Inlines.Add(new Run($"Generated Date: {DateTime.Now:dd MMMM yyyy HH:mm}") { FontSize = 9, FontWeight = FontWeights.SemiBold, Foreground = new SolidColorBrush(Color.FromRgb(71, 85, 105)) });
            doc.Blocks.Add(titlePara);

            // Customer Summary Card
            var custTable = new Table { CellSpacing = 0, Margin = new Thickness(0, 0, 0, 14), BorderBrush = new SolidColorBrush(Color.FromRgb(226, 232, 240)), BorderThickness = new Thickness(1) };
            custTable.Columns.Add(new TableColumn { Width = new GridLength(2, GridUnitType.Star) });
            custTable.Columns.Add(new TableColumn { Width = new GridLength(1.2, GridUnitType.Star) });

            var cRg = new TableRowGroup();
            var cRow = new TableRow { Background = new SolidColorBrush(Color.FromRgb(248, 250, 252)) };

            var cLeft = new Paragraph { Margin = new Thickness(12, 8, 12, 8) };
            cLeft.Inlines.Add(new Run("CUSTOMER ACCOUNT\n") { FontSize = 9, FontWeight = FontWeights.Bold, Foreground = new SolidColorBrush(Color.FromRgb(100, 116, 139)) });
            cLeft.Inlines.Add(new Run(customer.Name) { FontSize = 14, FontWeight = FontWeights.Black, Foreground = new SolidColorBrush(Color.FromRgb(15, 23, 42)) });
            if (!string.IsNullOrWhiteSpace(customer.Phone))
            {
                cLeft.Inlines.Add(new Run($"  ·  {customer.Phone}") { FontSize = 10, Foreground = new SolidColorBrush(Color.FromRgb(71, 85, 105)) });
            }

            var cRight = new Paragraph { Margin = new Thickness(12, 8, 12, 8), TextAlignment = TextAlignment.Right };
            cRight.Inlines.Add(new Run("CURRENT OUTSTANDING DUES:\n") { FontSize = 9, FontWeight = FontWeights.Bold, Foreground = new SolidColorBrush(Color.FromRgb(100, 116, 139)) });
            cRight.Inlines.Add(new Run($"Rs {customer.TotalDues:N0}") { FontSize = 15, FontWeight = FontWeights.Black, Foreground = new SolidColorBrush(Color.FromRgb(11, 79, 156)) });

            cRow.Cells.Add(new TableCell(cLeft));
            cRow.Cells.Add(new TableCell(cRight));
            cRg.Rows.Add(cRow);
            custTable.RowGroups.Add(cRg);
            doc.Blocks.Add(custTable);

            // Ledger History Table
            var table = new Table { CellSpacing = 0, BorderBrush = new SolidColorBrush(Color.FromRgb(203, 213, 225)), BorderThickness = new Thickness(1) };
            table.Columns.Add(new TableColumn { Width = new GridLength(1.1, GridUnitType.Star) });
            table.Columns.Add(new TableColumn { Width = new GridLength(1.4, GridUnitType.Star) });
            table.Columns.Add(new TableColumn { Width = new GridLength(1.0, GridUnitType.Star) });
            table.Columns.Add(new TableColumn { Width = new GridLength(2.2, GridUnitType.Star) });
            table.Columns.Add(new TableColumn { Width = new GridLength(1.1, GridUnitType.Star) });
            table.Columns.Add(new TableColumn { Width = new GridLength(1.1, GridUnitType.Star) });

            var rg = new TableRowGroup();
            var hdrRow = new TableRow { Background = new SolidColorBrush(Color.FromRgb(11, 79, 156)) };
            foreach (var h in new[] { "Date", "Voucher #", "Type", "Notes / Description", "Debit (+)", "Credit (-)" })
            {
                hdrRow.Cells.Add(CreateCell(h, FontWeights.Bold, h is "Debit (+)" or "Credit (-)" ? TextAlignment.Right : TextAlignment.Left, Brushes.White, null, null, 10));
            }
            rg.Rows.Add(hdrRow);

            foreach (var entry in history.Where(h => h.RecordType != "Estimate"))
            {
                var row = new TableRow();
                row.Cells.Add(CreateCell(entry.CreatedAt.ToString("dd MMM yyyy"), FontWeights.Normal, TextAlignment.Left));
                row.Cells.Add(CreateCell(entry.ReceiptNumber ?? entry.Id.ToString(), FontWeights.Normal, TextAlignment.Left));
                row.Cells.Add(CreateCell(entry.RecordType.ToUpperInvariant(), FontWeights.SemiBold, TextAlignment.Left));
                row.Cells.Add(CreateCell(string.IsNullOrWhiteSpace(entry.Notes) ? "-" : entry.Notes, FontWeights.Normal, TextAlignment.Left));
                string debit = entry.RecordType == "Sale" ? $"Rs {entry.TotalAmount:N0}" : "-";
                string credit = entry.RecordType == "Return"
                    ? $"Rs {entry.TotalAmount:N0}"
                    : (entry.AmountPaid > 0 ? $"Rs {entry.AmountPaid:N0}" : "-");
                row.Cells.Add(CreateCell(debit, FontWeights.Normal, TextAlignment.Right));
                row.Cells.Add(CreateCell(credit, FontWeights.Normal, TextAlignment.Right));
                rg.Rows.Add(row);
            }

            table.RowGroups.Add(rg);
            doc.Blocks.Add(table);

            AddLetterheadFooter(doc, settings);
            return doc;
        }

        private FlowDocument BuildInstallmentAgreementDocument(
            InstallmentContract contract,
            Customer customer,
            IReadOnlyList<InstallmentGuarantor> guarantors,
            IReadOnlyList<SaleItem> items,
            StoreSettings settings)
        {
            var doc = new FlowDocument
            {
                FontFamily = new FontFamily("Segoe UI"),
                FontSize = 10,
                Foreground = Brushes.Black,
                PagePadding = new Thickness(36)
            };

            // Shop Header
            var header = new Paragraph { TextAlignment = TextAlignment.Center, Margin = new Thickness(0, 0, 0, 8) };
            header.Inlines.Add(new Run(settings.ShopName) { FontSize = 18, FontWeight = FontWeights.Bold });
            header.Inlines.Add(new LineBreak());
            if (!string.IsNullOrWhiteSpace(settings.Subtitle))
            {
                header.Inlines.Add(new Run(settings.Subtitle) { FontSize = 10, Foreground = Brushes.DimGray });
                header.Inlines.Add(new LineBreak());
            }
            header.Inlines.Add(new Run($"{settings.Address}   |   Tel: {settings.Phone1} / {settings.Phone2}") { FontSize = 9 });
            doc.Blocks.Add(header);

            // Document Title
            var titleBox = new Paragraph
            {
                TextAlignment = TextAlignment.Center,
                Background = new SolidColorBrush(Color.FromRgb(241, 245, 249)),
                Margin = new Thickness(0, 4, 0, 10),
                Padding = new Thickness(6)
            };
            titleBox.Inlines.Add(new Run("INSTALLMENT FINANCING AGREEMENT") { FontSize = 13, FontWeight = FontWeights.Bold });
            titleBox.Inlines.Add(new LineBreak());
            titleBox.Inlines.Add(new Run($"Contract #: {contract.ContractNumber}   |   Booking Date: {contract.CreatedAt:dd-MMM-yyyy}   |   Officer: {contract.RecoveryOfficer ?? "Office"}") { FontSize = 9, FontWeight = FontWeights.SemiBold });
            doc.Blocks.Add(titleBox);

            // Section 1: Customer Details
            var custPara = new Paragraph { Margin = new Thickness(0, 4, 0, 6) };
            custPara.Inlines.Add(new Run("1. BUYER PARTICULARS:") { FontWeight = FontWeights.Bold, FontSize = 10.5 });
            custPara.Inlines.Add(new LineBreak());
            custPara.Inlines.Add(new Run($"Name: {customer.Name}     Father/Husband Name: {(string.IsNullOrWhiteSpace(customer.FatherOrHusbandName) ? "—" : customer.FatherOrHusbandName)}") { FontWeight = FontWeights.SemiBold });
            custPara.Inlines.Add(new LineBreak());
            custPara.Inlines.Add(new Run($"CNIC #: {(string.IsNullOrWhiteSpace(customer.Cnic) ? "—" : customer.Cnic)}     Mobile: {customer.Phone}"));
            custPara.Inlines.Add(new LineBreak());
            custPara.Inlines.Add(new Run($"Home Address: {(string.IsNullOrWhiteSpace(customer.Address) ? "—" : customer.Address)}"));
            if (!string.IsNullOrWhiteSpace(customer.WorkAddress))
            {
                custPara.Inlines.Add(new LineBreak());
                custPara.Inlines.Add(new Run($"Workplace / Business: {customer.WorkAddress}"));
            }
            doc.Blocks.Add(custPara);

            // Section 2: Guarantor (Only 1 Guarantor)
            var guarPara = new Paragraph { Margin = new Thickness(0, 4, 0, 6) };
            guarPara.Inlines.Add(new Run("2. GUARANTOR PARTICULARS:") { FontWeight = FontWeights.Bold, FontSize = 10.5 });
            guarPara.Inlines.Add(new LineBreak());

            if (guarantors.Any())
            {
                var g = guarantors.First();
                guarPara.Inlines.Add(new Run($"Guarantor ({g.Relationship}): {g.FullName}  |  CNIC: {g.CNIC}  |  Phone: {g.Phone}") { FontWeight = FontWeights.SemiBold });
                guarPara.Inlines.Add(new LineBreak());
                guarPara.Inlines.Add(new Run($"Address: {g.Address}"));
            }
            else
            {
                guarPara.Inlines.Add(new Run("No guarantor recorded. Primary buyer under direct security."));
            }
            doc.Blocks.Add(guarPara);

            // Section 3: Financed Items Table
            var itemSection = new Paragraph { Margin = new Thickness(0, 4, 0, 4) };
            itemSection.Inlines.Add(new Run("3. FINANCED GOODS / ASSETS:") { FontWeight = FontWeights.Bold, FontSize = 10.5 });
            doc.Blocks.Add(itemSection);

            var itemTable = new Table { CellSpacing = 0, BorderBrush = new SolidColorBrush(Color.FromRgb(203, 213, 225)), BorderThickness = new Thickness(1) };
            itemTable.Columns.Add(new TableColumn { Width = new GridLength(3, GridUnitType.Star) });
            itemTable.Columns.Add(new TableColumn { Width = new GridLength(2, GridUnitType.Star) });
            itemTable.Columns.Add(new TableColumn { Width = new GridLength(1, GridUnitType.Star) });
            itemTable.Columns.Add(new TableColumn { Width = new GridLength(1.5, GridUnitType.Star) });

            var itemGroup = new TableRowGroup();
            var itemHdr = new TableRow { Background = new SolidColorBrush(Color.FromRgb(241, 245, 249)) };
            itemHdr.Cells.Add(CreateCell("Product Description", FontWeights.Bold, TextAlignment.Left));
            itemHdr.Cells.Add(CreateCell("Serial / IMEI / Warranty", FontWeights.Bold, TextAlignment.Left));
            itemHdr.Cells.Add(CreateCell("Qty", FontWeights.Bold, TextAlignment.Right));
            itemHdr.Cells.Add(CreateCell("Agreed Price (Rs)", FontWeights.Bold, TextAlignment.Right));
            itemGroup.Rows.Add(itemHdr);

            if (items.Any())
            {
                foreach (var it in items)
                {
                    var r = new TableRow();
                    r.Cells.Add(CreateCell(it.Name, FontWeights.Normal, TextAlignment.Left));
                    string serial = !string.IsNullOrWhiteSpace(it.SerialNumber) ? $"S/N: {it.SerialNumber}" : "-";
                    if (!string.IsNullOrWhiteSpace(it.WarrantyPeriod)) serial += $" ({it.WarrantyPeriod})";
                    r.Cells.Add(CreateCell(serial, FontWeights.Normal, TextAlignment.Left));
                    r.Cells.Add(CreateCell($"{FormatQty(it.Quantity)} {it.Unit}", FontWeights.Normal, TextAlignment.Right));
                    r.Cells.Add(CreateCell($"Rs {it.Price:N0}", FontWeights.SemiBold, TextAlignment.Right));
                    itemGroup.Rows.Add(r);
                }
            }
            else
            {
                var r = new TableRow();
                r.Cells.Add(CreateCell("Installment Contract Asset", FontWeights.Normal, TextAlignment.Left));
                r.Cells.Add(CreateCell("General Contract", FontWeights.Normal, TextAlignment.Left));
                r.Cells.Add(CreateCell("1 pkg", FontWeights.Normal, TextAlignment.Right));
                r.Cells.Add(CreateCell($"Rs {contract.TotalPayable:N0}", FontWeights.SemiBold, TextAlignment.Right));
                itemGroup.Rows.Add(r);
            }
            itemTable.RowGroups.Add(itemGroup);
            doc.Blocks.Add(itemTable);

            // Section 4: Financial Matrix Table (No Cash Price, No Markup/Profit, No Grace/Late Fee row, All in English)
            var finSection = new Paragraph { Margin = new Thickness(0, 8, 0, 4) };
            finSection.Inlines.Add(new Run("4. PAYMENT & INSTALLMENT SCHEDULE:") { FontWeight = FontWeights.Bold, FontSize = 10.5 });
            doc.Blocks.Add(finSection);

            var finTable = new Table { CellSpacing = 0, BorderBrush = new SolidColorBrush(Color.FromRgb(203, 213, 225)), BorderThickness = new Thickness(1) };
            finTable.Columns.Add(new TableColumn { Width = new GridLength(1.5, GridUnitType.Star) });
            finTable.Columns.Add(new TableColumn { Width = new GridLength(1.5, GridUnitType.Star) });
            finTable.Columns.Add(new TableColumn { Width = new GridLength(1.5, GridUnitType.Star) });
            finTable.Columns.Add(new TableColumn { Width = new GridLength(1.5, GridUnitType.Star) });

            var finGroup = new TableRowGroup();

            // Row 1: Total Price & Down Payment
            var finRow1 = new TableRow();
            finRow1.Cells.Add(CreateCell("Total Installment Price:", FontWeights.Bold, TextAlignment.Left));
            finRow1.Cells.Add(CreateCell($"Rs {contract.TotalPayable:N0}", FontWeights.Bold, TextAlignment.Right));
            finRow1.Cells.Add(CreateCell("Down Payment / Advance:", FontWeights.Bold, TextAlignment.Left));
            finRow1.Cells.Add(CreateCell($"Rs {contract.DownPayment:N0}", FontWeights.SemiBold, TextAlignment.Right));
            finGroup.Rows.Add(finRow1);

            // Row 2: Remaining Balance & Total Installments
            decimal remainingBalance = Math.Max(0m, contract.TotalPayable - contract.DownPayment);
            var finRow2 = new TableRow();
            finRow2.Cells.Add(CreateCell("Remaining Balance:", FontWeights.Bold, TextAlignment.Left));
            finRow2.Cells.Add(CreateCell($"Rs {remainingBalance:N0}", FontWeights.SemiBold, TextAlignment.Right));
            finRow2.Cells.Add(CreateCell("Total Installments:", FontWeights.Bold, TextAlignment.Left));
            finRow2.Cells.Add(CreateCell($"{contract.InstallmentsCount} Installments", FontWeights.SemiBold, TextAlignment.Right));
            finGroup.Rows.Add(finRow2);

            // Row 3: Per-Installment Amount & Payment Plan Frequency
            decimal monthlyQist = contract.InstallmentsCount > 0
                ? decimal.Round(remainingBalance / contract.InstallmentsCount, 2, MidpointRounding.AwayFromZero)
                : 0m;

            var finRow3 = new TableRow { Background = new SolidColorBrush(Color.FromRgb(241, 245, 249)) };
            finRow3.Cells.Add(CreateCell($"Installment Amount ({contract.Frequency}):", FontWeights.Bold, TextAlignment.Left));
            finRow3.Cells.Add(CreateCell($"Rs {monthlyQist:N0}", FontWeights.Bold, TextAlignment.Right));
            finRow3.Cells.Add(CreateCell("Payment Frequency:", FontWeights.Bold, TextAlignment.Left));
            finRow3.Cells.Add(CreateCell($"{contract.Frequency} Plan", FontWeights.SemiBold, TextAlignment.Right));
            finGroup.Rows.Add(finRow3);

            finTable.RowGroups.Add(finGroup);
            doc.Blocks.Add(finTable);

            // Section 5: Legal Undertaking (100% English)
            var terms = new Paragraph { Margin = new Thickness(0, 10, 0, 16), LineHeight = 16 };
            terms.Inlines.Add(new Run("5. TERMS & CONDITIONS OF AGREEMENT:") { FontWeight = FontWeights.Bold, FontSize = 10.5 });
            terms.Inlines.Add(new LineBreak());
            terms.Inlines.Add(new Run("1. The Buyer agrees and undertakes to pay each scheduled installment on or before its due date at the store office.\n"));
            terms.Inlines.Add(new Run("2. The legal ownership and title of the financed goods shall strictly remain with the store until all installments and dues are settled in full.\n"));
            terms.Inlines.Add(new Run("3. The Buyer shall not sell, gift, pledge, mortgage, or transfer the goods to any third party before full payment of all installments.\n"));
            terms.Inlines.Add(new Run("4. In the event of default or non-payment, the store reserves the legal right to repossess the financed goods and recover outstanding dues from the Buyer and Guarantor."));
            doc.Blocks.Add(terms);

            // Section 6: Formal Signatures & Thumb Impressions (1 Guarantor only - 3 columns total)
            var sigTable = new Table { CellSpacing = 8, Margin = new Thickness(0, 10, 0, 0) };
            sigTable.Columns.Add(new TableColumn { Width = new GridLength(1, GridUnitType.Star) });
            sigTable.Columns.Add(new TableColumn { Width = new GridLength(1, GridUnitType.Star) });
            sigTable.Columns.Add(new TableColumn { Width = new GridLength(1, GridUnitType.Star) });

            var sigGroup = new TableRowGroup();
            var sigRow = new TableRow();

            foreach (var label in new[]
            {
                "Buyer Signature & Thumb",
                "Guarantor Signature & Thumb",
                "Authorized Store Stamp & Signature"
            })
            {
                var p = new Paragraph { TextAlignment = TextAlignment.Center, Margin = new Thickness(4) };
                p.Inlines.Add(new Run("\n\n\n______________________\n"));
                p.Inlines.Add(new Run(label) { FontSize = 9, FontWeight = FontWeights.SemiBold });
                var cell = new TableCell(p)
                {
                    BorderBrush = new SolidColorBrush(Color.FromRgb(203, 213, 225)),
                    BorderThickness = new Thickness(1),
                    Background = new SolidColorBrush(Color.FromRgb(248, 250, 252))
                };
                sigRow.Cells.Add(cell);
            }
            sigGroup.Rows.Add(sigRow);
            sigTable.RowGroups.Add(sigGroup);
            doc.Blocks.Add(sigTable);

            return doc;
        }

        private FlowDocument BuildInstallmentPassbookDocument(
            InstallmentContract contract,
            Customer customer,
            IReadOnlyList<InstallmentSchedule> schedules,
            StoreSettings settings)
        {
            var doc = new FlowDocument
            {
                FontFamily = new FontFamily("Segoe UI"),
                FontSize = 9.5,
                Foreground = Brushes.Black,
                PagePadding = new Thickness(36)
            };

            // Shop Header
            var header = new Paragraph { TextAlignment = TextAlignment.Center, Margin = new Thickness(0, 0, 0, 6) };
            header.Inlines.Add(new Run(settings.ShopName) { FontSize = 18, FontWeight = FontWeights.Bold });
            header.Inlines.Add(new LineBreak());
            header.Inlines.Add(new Run($"INSTALLMENT PASSBOOK & RECOVERY SCHEDULE  ·  {settings.Phone1}") { FontSize = 10, FontWeight = FontWeights.SemiBold });
            doc.Blocks.Add(header);

            // Card Header Box
            var cardBox = new Paragraph
            {
                Background = new SolidColorBrush(Color.FromRgb(241, 245, 249)),
                Padding = new Thickness(8),
                Margin = new Thickness(0, 0, 0, 10)
            };
            cardBox.Inlines.Add(new Run($"Customer: {customer.Name}  |  CNIC: {customer.Cnic ?? "—"}  |  Mobile: {customer.Phone}") { FontWeight = FontWeights.Bold });
            cardBox.Inlines.Add(new LineBreak());
            cardBox.Inlines.Add(new Run($"Contract #: {contract.ContractNumber}   |   Total Payable: Rs {contract.TotalPayable:N0}   |   Advance: Rs {contract.DownPayment:N0}   |   Tenure: {contract.InstallmentsCount} Installments"));
            doc.Blocks.Add(cardBox);

            // Schedule Table
            var table = new Table { CellSpacing = 0, BorderBrush = new SolidColorBrush(Color.FromRgb(203, 213, 225)), BorderThickness = new Thickness(1) };
            table.Columns.Add(new TableColumn { Width = new GridLength(45d) }); // Inst #
            table.Columns.Add(new TableColumn { Width = new GridLength(85d) }); // Due Date
            table.Columns.Add(new TableColumn { Width = new GridLength(85d) }); // Due Amount
            table.Columns.Add(new TableColumn { Width = new GridLength(85d) }); // Paid Date
            table.Columns.Add(new TableColumn { Width = new GridLength(85d) }); // Paid Amount
            table.Columns.Add(new TableColumn { Width = new GridLength(90d) }); // Status
            table.Columns.Add(new TableColumn { Width = new GridLength(110d) }); // Sign / Stamp

            var rg = new TableRowGroup();
            var hdr = new TableRow { Background = new SolidColorBrush(Color.FromRgb(226, 232, 240)), FontWeight = FontWeights.Bold };
            foreach (var h in new[] { "Inst #", "Due Date", "Due (Rs)", "Paid Date", "Paid (Rs)", "Status", "Sign / Stamp" })
            {
                hdr.Cells.Add(CreateCell(h, FontWeights.Bold, h is "Due (Rs)" or "Paid (Rs)" ? TextAlignment.Right : TextAlignment.Center));
            }
            rg.Rows.Add(hdr);

            decimal totalPaid = 0m;
            foreach (var s in schedules.OrderBy(item => item.Sequence))
            {
                totalPaid += s.AmountPaid;
                var row = new TableRow();
                if (s.Status == "Paid") row.Background = new SolidColorBrush(Color.FromRgb(240, 253, 244)); // Emerald tint
                else if (s.Status == "Overdue") row.Background = new SolidColorBrush(Color.FromRgb(254, 242, 242)); // Rose tint

                row.Cells.Add(CreateCell($"#{s.Sequence}", FontWeights.Bold, TextAlignment.Center));
                row.Cells.Add(CreateCell(s.DueDate.ToString("dd MMM yyyy"), FontWeights.Normal, TextAlignment.Center));
                row.Cells.Add(CreateCell($"Rs {s.AmountDue:N0}", FontWeights.SemiBold, TextAlignment.Right));
                row.Cells.Add(CreateCell(s.AmountPaid > 0 ? DateTime.Now.ToString("dd/MM/yy") : "—", FontWeights.Normal, TextAlignment.Center));
                row.Cells.Add(CreateCell(s.AmountPaid > 0 ? $"Rs {s.AmountPaid:N0}" : "—", FontWeights.SemiBold, TextAlignment.Right));
                row.Cells.Add(CreateCell(s.Status.ToUpperInvariant(), FontWeights.Bold, TextAlignment.Center));
                row.Cells.Add(CreateCell("_______________", FontWeights.Normal, TextAlignment.Center));
                rg.Rows.Add(row);
            }
            table.RowGroups.Add(rg);
            doc.Blocks.Add(table);

            // Summary
            var summary = new Paragraph { TextAlignment = TextAlignment.Right, Margin = new Thickness(0, 10, 0, 0) };
            summary.Inlines.Add(new Run($"Total Financed: Rs {contract.TotalPayable:N0}   |   Total Paid: Rs {contract.DownPayment + totalPaid:N0}   |   Remaining: Rs {Math.Max(0m, contract.TotalPayable - (contract.DownPayment + totalPaid)):N0}") { FontSize = 11, FontWeight = FontWeights.Bold });
            doc.Blocks.Add(summary);

            return doc;
        }

        private FlowDocument BuildDailyZReportDocument(
            DateTime date,
            DailyZReportSummary summary,
            StoreSettings settings)
        {
            var doc = new FlowDocument
            {
                FontFamily = new FontFamily("Segoe UI"),
                FontSize = 10,
                Foreground = Brushes.Black,
                PagePadding = new Thickness(40)
            };

            var header = new Paragraph { TextAlignment = TextAlignment.Center, Margin = new Thickness(0, 0, 0, 10) };
            header.Inlines.Add(new Run(settings.ShopName) { FontSize = 18, FontWeight = FontWeights.Bold });
            header.Inlines.Add(new LineBreak());
            header.Inlines.Add(new Run("DAILY CASH REGISTER CLOSING REPORT (روزنامچہ اختتام)") { FontSize = 12, FontWeight = FontWeights.Bold });
            header.Inlines.Add(new LineBreak());
            header.Inlines.Add(new Run($"Date: {date:dd MMMM yyyy}   |   Generated: {DateTime.Now:HH:mm:ss}   |   Cashier: {summary.CashierName}") { FontSize = 9, Foreground = Brushes.DimGray });
            doc.Blocks.Add(header);

            var table = new Table { CellSpacing = 0, BorderBrush = new SolidColorBrush(Color.FromRgb(203, 213, 225)), BorderThickness = new Thickness(1) };
            table.Columns.Add(new TableColumn { Width = new GridLength(2.5, GridUnitType.Star) });
            table.Columns.Add(new TableColumn { Width = new GridLength(1.5, GridUnitType.Star) });

            var rg = new TableRowGroup();

            void AddSection(string title)
            {
                var r = new TableRow { Background = new SolidColorBrush(Color.FromRgb(241, 245, 249)) };
                r.Cells.Add(new TableCell(new Paragraph(new Run(title)) { FontWeight = FontWeights.Bold, Margin = new Thickness(8, 5, 8, 5) }) { ColumnSpan = 2 });
                rg.Rows.Add(r);
            }

            void AddRow(string label, decimal amount, bool isBold = false)
            {
                var r = new TableRow();
                r.Cells.Add(CreateCell(label, isBold ? FontWeights.Bold : FontWeights.Normal, TextAlignment.Left));
                r.Cells.Add(CreateCell($"Rs {amount:N0}", isBold ? FontWeights.Bold : FontWeights.Normal, TextAlignment.Right));
                rg.Rows.Add(r);
            }

            AddSection("1. SALES & BILLING REVENUE");
            AddRow("Total Invoices Count", summary.TotalInvoicesCount, false);
            AddRow("Cash Sales", summary.CashSales);
            AddRow("Card / Bank / Raast Sales", summary.CardSales + summary.BankSales);
            AddRow("Credit (Udhaar) Sales", summary.CreditSales);
            AddRow("Total Net Sales Revenue", summary.TotalSalesRevenue, true);

            AddSection("2. CASH DRAWER INFLOWS");
            AddRow("Cash Sales Tendered", summary.CashSales);
            AddRow("Khata Cash In (Customer Wasooli)", summary.KhataCashIn);
            AddRow("Installment Qist Recoveries & Advance", summary.InstallmentRecoveries);
            AddRow("Total Cash Inflow", summary.TotalCashInflow, true);

            AddSection("3. CASH DRAWER OUTFLOWS / EXPENSES");
            AddRow("Supplier Payments & Shop Expenses", summary.CashOutExpenses, true);

            AddSection("4. DRAWER RECONCILIATION & CLOSING");
            AddRow("Expected Drawer Cash Balance", summary.ExpectedDrawerCash, true);
            AddRow("Actual Physical Cash Counted", summary.ActualDrawerCash, true);

            var diffRow = new TableRow { Background = summary.Variance == 0 ? new SolidColorBrush(Color.FromRgb(240, 253, 244)) : new SolidColorBrush(Color.FromRgb(254, 242, 242)) };
            diffRow.Cells.Add(CreateCell("Drawer Variance (Over / Short)", FontWeights.Bold, TextAlignment.Left));
            diffRow.Cells.Add(CreateCell($"Rs {summary.Variance:+#,##0;-#,##0;0}", FontWeights.Bold, TextAlignment.Right));
            rg.Rows.Add(diffRow);

            AddSection("5. FISCAL INTEGRATION STATUS");
            AddRow("FBR Offline Invoices Pending Sync", summary.FbrOfflinePendingCount, summary.FbrOfflinePendingCount > 0);

            table.RowGroups.Add(rg);
            doc.Blocks.Add(table);

            var sigs = new Paragraph { Margin = new Thickness(0, 24, 0, 0), TextAlignment = TextAlignment.Center };
            sigs.Inlines.Add(new Run("Cashier Signature: _______________________          Owner / Manager Verification: _______________________") { FontWeight = FontWeights.SemiBold });
            doc.Blocks.Add(sigs);

            return doc;
        }

        private (bool success, string message) PrintThermalZReport(DailyZReportSummary s, StoreSettings settings)
        {
            if (!OperatingSystem.IsWindows()) return (false, "Windows required for thermal spooler.");
            try
            {
                var sb = new StringBuilder();
                sb.AppendLine("\x1B\x40"); // ESC @ Init
                sb.AppendLine("\x1B\x61\x01"); // Center
                sb.AppendLine(settings.ShopName);
                sb.AppendLine("DAILY CLOSING (Z-REPORT)");
                sb.AppendLine($"Date: {s.Date:dd-MMM-yyyy}  Time: {DateTime.Now:HH:mm}");
                sb.AppendLine($"Cashier: {s.CashierName}");
                sb.AppendLine("--------------------------------");
                sb.AppendLine("\x1B\x61\x00"); // Left align
                sb.AppendLine($"Invoices Count : {s.TotalInvoicesCount}");
                sb.AppendLine($"Cash Sales     : Rs {s.CashSales,12:N0}");
                sb.AppendLine($"Card/Bank Sales: Rs {s.CardSales + s.BankSales,12:N0}");
                sb.AppendLine($"Credit Sales   : Rs {s.CreditSales,12:N0}");
                sb.AppendLine($"Net Revenue    : Rs {s.TotalSalesRevenue,12:N0}");
                sb.AppendLine("--------------------------------");
                sb.AppendLine("CASH DRAWER COLLECTIONS:");
                sb.AppendLine($"Sales Cash     : Rs {s.CashSales,12:N0}");
                sb.AppendLine($"Khata Wasooli  : Rs {s.KhataCashIn,12:N0}");
                sb.AppendLine($"Qist Recoveries: Rs {s.InstallmentRecoveries,12:N0}");
                sb.AppendLine($"Total Cash In  : Rs {s.TotalCashInflow,12:N0}");
                sb.AppendLine($"Cash Outflows  : Rs {s.CashOutExpenses,12:N0}");
                sb.AppendLine("--------------------------------");
                sb.AppendLine($"Expected Drawer: Rs {s.ExpectedDrawerCash,12:N0}");
                sb.AppendLine($"Actual Counted : Rs {s.ActualDrawerCash,12:N0}");
                sb.AppendLine($"Variance (Diff): Rs {s.Variance,12:+#,##0;-#,##0;0}");
                sb.AppendLine("--------------------------------");
                if (s.FbrOfflinePendingCount > 0)
                {
                    sb.AppendLine($"FBR Offline Queued: {s.FbrOfflinePendingCount} INVOICES");
                }
                sb.AppendLine("\n\nCashier Sign: __________________\n");
                sb.AppendLine("\x1D\x56\x00"); // Cut

                string printerName = string.IsNullOrWhiteSpace(settings.PrinterName) ? "XP-80" : settings.PrinterName;
                byte[] bytes = Encoding.ASCII.GetBytes(sb.ToString());
                RawPrinterHelper.SendBytesToPrinter(printerName, bytes);
                return (true, "Daily Z-Report printed to thermal.");
            }
            catch (Exception ex)
            {
                return (false, $"Thermal Z-Report error: {ex.Message}");
            }
        }

        public static string NumberToWords(decimal number)
        {
            long n = (long)Math.Floor(number);
            if (n == 0) return "Zero Rupees Only";
            string words = NumberToWordsHelper(Math.Abs(n));
            return (n < 0 ? "Minus " : "") + words.Trim() + " Rupees Only";
        }

        private static string NumberToWordsHelper(long number)
        {
            if (number == 0) return "";
            string words = "";

            if ((number / 10000000) > 0)
            {
                words += NumberToWordsHelper(number / 10000000) + " Crore ";
                number %= 10000000;
            }
            if ((number / 100000) > 0)
            {
                words += NumberToWordsHelper(number / 100000) + " Lakh ";
                number %= 100000;
            }
            if ((number / 1000) > 0)
            {
                words += NumberToWordsHelper(number / 1000) + " Thousand ";
                number %= 1000;
            }
            if ((number / 100) > 0)
            {
                words += NumberToWordsHelper(number / 100) + " Hundred ";
                number %= 100;
            }
            if (number > 0)
            {
                var unitsMap = new[] { "Zero", "One", "Two", "Three", "Four", "Five", "Six", "Seven", "Eight", "Nine", "Ten", "Eleven", "Twelve", "Thirteen", "Fourteen", "Fifteen", "Sixteen", "Seventeen", "Eighteen", "Nineteen" };
                var tensMap = new[] { "Zero", "Ten", "Twenty", "Thirty", "Forty", "Fifty", "Sixty", "Seventy", "Eighty", "Ninety" };

                if (number < 20)
                    words += unitsMap[number];
                else
                {
                    words += tensMap[number / 10];
                    if ((number % 10) > 0)
                        words += "-" + unitsMap[number % 10];
                }
            }
            return words.Trim();
        }

        private static TableCell CreateCell(string text, FontWeight weight, TextAlignment align, Brush? foreground = null, Brush? background = null, Thickness? border = null, double fontSize = 11)
        {
            var p = new Paragraph(new Run(text))
            {
                Margin = new Thickness(8, 6, 8, 6),
                FontWeight = weight,
                TextAlignment = align,
                FontSize = fontSize
            };
            if (foreground != null) p.Foreground = foreground;

            var cell = new TableCell(p)
            {
                BorderBrush = new SolidColorBrush(Color.FromRgb(226, 232, 240)),
                BorderThickness = border ?? new Thickness(0, 0, 0, 1)
            };
            if (background != null) cell.Background = background;
            return cell;
        }

        private static string ToSafeAscii(string? text)
        {
            if (string.IsNullOrEmpty(text)) return string.Empty;
            var sb = new StringBuilder(text.Length);
            foreach (char c in text)
            {
                sb.Append(c <= 126 && c >= 32 ? c : '?');
            }
            return sb.ToString();
        }
    }
}
