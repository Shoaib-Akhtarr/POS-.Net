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

        public (bool success, string message) PrintSaleAuto(Sale sale)
        {
            var settings = ThemeService.GetSettings();
            if (string.Equals(settings.PreferredPrintFormat, "A4", StringComparison.OrdinalIgnoreCase))
            {
                return PrintA4Invoice(sale, settings);
            }
            return PrintThermalReceipt(sale, settings);
        }

        public void PrintReceipt(Sale sale)
        {
            var result = PrintSaleAuto(sale);
            if (!result.success && !string.IsNullOrWhiteSpace(result.message))
            {
                MessageBox.Show(result.message, "Printing Notice", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        public (bool success, string message) PrintThermalReceipt(Sale sale, StoreSettings? settings = null)
        {
            settings ??= ThemeService.GetSettings();

            if (!OperatingSystem.IsWindows())
            {
                return (false, "Thermal printing is only supported on Windows.");
            }

            try
            {
                byte[] bytes = settings.UseBitmapThermalForUrdu
                    ? FormatBitmapReceipt(sale, settings)
                    : FormatAsciiReceipt(sale, settings);

                string printerName = string.IsNullOrWhiteSpace(settings.PrinterName) ? "XP-80" : settings.PrinterName;
                var result = RawPrinterHelper.SendBytesToPrinter(printerName, bytes);

                if (!result.success)
                {
                    string? detected = AutoDetectPrinter();
                    if (detected != null && !string.Equals(detected, printerName, StringComparison.OrdinalIgnoreCase))
                    {
                        result = RawPrinterHelper.SendBytesToPrinter(detected, bytes);
                    }
                }

                return result.success
                    ? (true, "Printed successfully.")
                    : (false, result.error);
            }
            catch (Exception ex)
            {
                return (false, $"System Error: {ex.Message}");
            }
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
                CustomerName = "Test Customer",
                Notes = "Printer Verification",
                CreatedBy = SessionService.CurrentUserName,
                TotalAmount = 1500,
                AmountPaid = 1500,
                CashTendered = 2000,
                ChangeReturned = 500,
                SaleItems = new List<SaleItem>
                {
                    new SaleItem { Name = "GM 7/29 Electric Cable", Quantity = 12.5m, Unit = "m", UnitPrice = 120, Price = 1500 }
                }
            };

            return PrintThermalReceipt(testSale, settings);
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

        private (bool success, string message) PrintFlowDocument(FlowDocument doc, string jobTitle)
        {
            var dlg = new PrintDialog();
            if (dlg.ShowDialog() != true)
            {
                return (false, string.Empty);
            }

            doc.PageWidth = dlg.PrintableAreaWidth;
            doc.PageHeight = dlg.PrintableAreaHeight;
            doc.PagePadding = new Thickness(40);
            doc.ColumnWidth = dlg.PrintableAreaWidth;

            IDocumentPaginatorSource idp = doc;
            dlg.PrintDocument(idp.DocumentPaginator, jobTitle);
            return (true, "Printed successfully.");
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

        private FlowDocument BuildA4InvoiceDocument(Sale sale, StoreSettings settings)
        {
            var doc = new FlowDocument
            {
                FontFamily = new FontFamily("Segoe UI"),
                FontSize = 12,
                Foreground = new SolidColorBrush(Color.FromRgb(15, 23, 42))
            };

            // Store Header
            var header = new Paragraph { TextAlignment = TextAlignment.Center, Margin = new Thickness(0, 0, 0, 12) };
            header.Inlines.Add(new Run(settings.ShopName) { FontSize = 22, FontWeight = FontWeights.Bold });
            header.Inlines.Add(new LineBreak());
            if (!string.IsNullOrWhiteSpace(settings.Subtitle))
            {
                header.Inlines.Add(new Run(settings.Subtitle) { FontSize = 12, Foreground = new SolidColorBrush(Color.FromRgb(71, 85, 105)) });
                header.Inlines.Add(new LineBreak());
            }
            header.Inlines.Add(new Run($"{settings.Address}  |  {settings.Phone1} / {settings.Phone2}")
            {
                FontSize = 11,
                Foreground = new SolidColorBrush(Color.FromRgb(71, 85, 105))
            });
            doc.Blocks.Add(header);

            string docType = sale.RecordType.ToUpperInvariant() switch
            {
                "ESTIMATE" => "QUOTATION / ESTIMATE",
                "RETURN" => "RETURN CREDIT NOTE",
                "PAYMENT" => "PAYMENT RECEIPT",
                _ => "SALE INVOICE"
            };

            var meta = new Paragraph { Margin = new Thickness(0, 0, 0, 14) };
            meta.Inlines.Add(new Run($"{docType}   ") { FontSize = 15, FontWeight = FontWeights.Bold, Foreground = new SolidColorBrush(Color.FromRgb(13, 148, 136)) });
            meta.Inlines.Add(new Run($"Ref: #{sale.ReceiptNumber ?? sale.Id.ToString()}   |   Date: {sale.CreatedAt:dd MMM yyyy HH:mm}"));
            meta.Inlines.Add(new LineBreak());
            meta.Inlines.Add(new Run($"Customer: {sale.CustomerName ?? "Walk-in Customer"}") { FontWeight = FontWeights.SemiBold });
            if (!string.IsNullOrWhiteSpace(sale.Notes))
                meta.Inlines.Add(new Run($"   |   Site / Notes: {sale.Notes}"));
            if (!string.IsNullOrWhiteSpace(sale.CreatedBy))
                meta.Inlines.Add(new Run($"   |   Staff: {sale.CreatedBy}"));
            doc.Blocks.Add(meta);

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

            if (sale.SaleItems.Any())
            {
                var table = new Table { CellSpacing = 0, BorderBrush = new SolidColorBrush(Color.FromRgb(203, 213, 225)), BorderThickness = new Thickness(1) };
                table.Columns.Add(new TableColumn { Width = new GridLength(3, GridUnitType.Star) });
                table.Columns.Add(new TableColumn { Width = new GridLength(1, GridUnitType.Star) });
                table.Columns.Add(new TableColumn { Width = new GridLength(1, GridUnitType.Star) });
                table.Columns.Add(new TableColumn { Width = new GridLength(1, GridUnitType.Star) });
                table.Columns.Add(new TableColumn { Width = new GridLength(1.2, GridUnitType.Star) });

                var rg = new TableRowGroup();
                var hdrRow = new TableRow { Background = new SolidColorBrush(Color.FromRgb(241, 245, 249)) };
                foreach (var h in new[] { "Item Description / Serial / Warranty", "Qty", "Rate", "Disc", "Amount" })
                {
                    hdrRow.Cells.Add(CreateCell(h, FontWeights.Bold, h == "Item Description / Serial / Warranty" ? TextAlignment.Left : TextAlignment.Right));
                }
                rg.Rows.Add(hdrRow);

                foreach (var item in sale.SaleItems)
                {
                    var row = new TableRow();
                    var desc = new StringBuilder(item.Name);
                    if (!string.IsNullOrWhiteSpace(item.SerialNumber)) desc.Append($"\nS/N: {item.SerialNumber}");
                    if (!string.IsNullOrWhiteSpace(item.WarrantyPeriod)) desc.Append($"  |  Warranty: {item.WarrantyPeriod}");

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

            var summary = new Paragraph { TextAlignment = TextAlignment.Right, Margin = new Thickness(0, 16, 0, 20) };
            if (sale.Discount > 0)
            {
                summary.Inlines.Add(new Run($"Bill Discount: Rs {sale.Discount:N0}"));
                summary.Inlines.Add(new LineBreak());
            }
            if (sale.FbrTaxAmount > 0)
            {
                decimal exclTax = Math.Max(0m, sale.TotalAmount - sale.FbrTaxAmount);
                summary.Inlines.Add(new Run($"Total Value (Excl. Tax): Rs {exclTax:N0}"));
                summary.Inlines.Add(new LineBreak());
                summary.Inlines.Add(new Run($"Sales Tax / GST ({sale.FbrTaxRate:0.#}%): Rs {sale.FbrTaxAmount:N0}"));
                summary.Inlines.Add(new LineBreak());
            }
            if (sale.RecordType != "Payment")
            {
                summary.Inlines.Add(new Run($"Bill Total: Rs {sale.TotalAmount:N0}") { FontSize = 14, FontWeight = FontWeights.Bold });
                summary.Inlines.Add(new LineBreak());
            }
            if (sale.CustomerId != null)
            {
                summary.Inlines.Add(new Run($"Previous Dues: Rs {sale.PreviousDues:N0}"));
                summary.Inlines.Add(new LineBreak());
            }
            summary.Inlines.Add(new Run($"Amount Paid ({sale.PaymentMethod}): Rs {sale.AmountPaid:N0}") { FontSize = 14, FontWeight = FontWeights.Bold });
            if (sale.ChangeReturned > 0)
            {
                summary.Inlines.Add(new LineBreak());
                summary.Inlines.Add(new Run($"Cash Tendered: Rs {sale.CashTendered:N0}  |  Change: Rs {sale.ChangeReturned:N0}"));
            }
            if (sale.CustomerId != null)
            {
                summary.Inlines.Add(new LineBreak());
                summary.Inlines.Add(new Run($"Remaining Khata Balance: Rs {sale.BalanceDue:N0}") { FontSize = 14, FontWeight = FontWeights.Bold });
            }
            doc.Blocks.Add(summary);

            var footer = new Paragraph { TextAlignment = TextAlignment.Center, Margin = new Thickness(0, 24, 0, 0), Foreground = new SolidColorBrush(Color.FromRgb(100, 116, 139)) };
            footer.Inlines.Add(new Run(settings.ReceiptFooter));
            doc.Blocks.Add(footer);

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
                FontSize = 12,
                Foreground = new SolidColorBrush(Color.FromRgb(15, 23, 42))
            };

            var header = new Paragraph { TextAlignment = TextAlignment.Center, Margin = new Thickness(0, 0, 0, 14) };
            header.Inlines.Add(new Run(settings.ShopName) { FontSize = 20, FontWeight = FontWeights.Bold });
            header.Inlines.Add(new LineBreak());
            header.Inlines.Add(new Run($"CUSTOMER KHATA STATEMENT  —  {DateTime.Now:dd MMM yyyy}") { FontSize = 12, FontWeight = FontWeights.SemiBold });
            doc.Blocks.Add(header);

            var custInfo = new Paragraph { Margin = new Thickness(0, 0, 0, 12) };
            custInfo.Inlines.Add(new Run($"Customer: {customer.Name}") { FontSize = 15, FontWeight = FontWeights.Bold });
            custInfo.Inlines.Add(new Run($"   |   Phone: {customer.Phone}"));
            custInfo.Inlines.Add(new LineBreak());
            custInfo.Inlines.Add(new Run($"Current Outstanding Balance: Rs {customer.TotalDues:N0}") { FontSize = 14, FontWeight = FontWeights.Bold });
            doc.Blocks.Add(custInfo);

            var table = new Table { CellSpacing = 0, BorderBrush = new SolidColorBrush(Color.FromRgb(203, 213, 225)), BorderThickness = new Thickness(1) };
            table.Columns.Add(new TableColumn { Width = new GridLength(1.2, GridUnitType.Star) });
            table.Columns.Add(new TableColumn { Width = new GridLength(1.2, GridUnitType.Star) });
            table.Columns.Add(new TableColumn { Width = new GridLength(1, GridUnitType.Star) });
            table.Columns.Add(new TableColumn { Width = new GridLength(2, GridUnitType.Star) });
            table.Columns.Add(new TableColumn { Width = new GridLength(1, GridUnitType.Star) });
            table.Columns.Add(new TableColumn { Width = new GridLength(1, GridUnitType.Star) });

            var rg = new TableRowGroup();
            var hdrRow = new TableRow { Background = new SolidColorBrush(Color.FromRgb(241, 245, 249)) };
            foreach (var h in new[] { "Date", "Ref", "Type", "Notes", "Debit (+)", "Credit (-)" })
            {
                hdrRow.Cells.Add(CreateCell(h, FontWeights.Bold, h is "Debit (+)" or "Credit (-)" ? TextAlignment.Right : TextAlignment.Left));
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
            return doc;
        }

        private static TableCell CreateCell(string text, FontWeight weight, TextAlignment align)
        {
            return new TableCell(new Paragraph(new Run(text))
            {
                Margin = new Thickness(8, 6, 8, 6),
                FontWeight = weight,
                TextAlignment = align
            })
            {
                BorderBrush = new SolidColorBrush(Color.FromRgb(226, 232, 240)),
                BorderThickness = new Thickness(0, 0, 0, 1)
            };
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
