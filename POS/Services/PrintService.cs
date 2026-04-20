using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using POS.Models;

namespace POS.Services
{
    public class PrintService
    {
        // Default printer name - user can change this in Windows settings to match
        public string PrinterName { get; set; } = "XP-80"; 

        public void PrintReceipt(Sale sale)
        {
            try
            {
                byte[] bytes = FormatReceipt(sale);
                
                // 1. Try default printer
                var result = RawPrinterHelper.SendBytesToPrinter(PrinterName, bytes);
                
                // 2. If default fails, try to auto-detect a thermal printer
                if (!result.success)
                {
                    string? detectedPrinter = AutoDetectPrinter();
                    if (detectedPrinter != null)
                    {
                        PrinterName = detectedPrinter;
                        result = RawPrinterHelper.SendBytesToPrinter(PrinterName, bytes);
                    }
                }

                // 3. Show error if still failing
                if (!result.success)
                {
                    System.Windows.MessageBox.Show(result.error, "Printing Error", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Warning);
                }
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show($"System Error: {ex.Message}", "Printing Error", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
            }
        }

        private string? AutoDetectPrinter()
        {
            try
            {
                // Simple keyword search in installed printers
                foreach (string printer in System.Drawing.Printing.PrinterSettings.InstalledPrinters)
                {
                    string p = printer.ToLower();
                    if (p.Contains("xp") || p.Contains("pos") || p.Contains("speed") || p.Contains("80mm") || p.Contains("thermal"))
                    {
                        return printer;
                    }
                }
            }
            catch { }
            return null;
        }

        private byte[] FormatReceipt(Sale sale)
        {
            List<byte> bytes = new List<byte>();

            // 1. Initialize Printer
            bytes.AddRange(new byte[] { 0x1B, 0x40 });

            // 2. Header: Haji Faiz Traders
            bytes.AddRange(new byte[] { 0x1B, 0x61, 0x01 }); // Center align
            bytes.AddRange(new byte[] { 0x1B, 0x21, 0x30 }); // Double height + Double width
            bytes.AddRange(Encoding.ASCII.GetBytes("Haji Faiz Traders\n"));
            bytes.AddRange(new byte[] { 0x1B, 0x21, 0x00 }); // Normal font
            bytes.AddRange(Encoding.ASCII.GetBytes("Nr Al-Ghani Hall, Shadola Rd, Gujrat\n\n"));

            // 3. Metadata: Left align
            bytes.AddRange(new byte[] { 0x1B, 0x61, 0x00 }); // Left align
            bytes.AddRange(Encoding.ASCII.GetBytes($"Date:    {sale.CreatedAt:dd/MM/yyyy HH:mm}\n"));
            bytes.AddRange(Encoding.ASCII.GetBytes($"Receipt: #{sale.ReceiptNumber ?? sale.Id.ToString()}\n"));
            bytes.AddRange(Encoding.ASCII.GetBytes($"Customer: {sale.CustomerName ?? "Walk-in"}\n"));
            bytes.AddRange(Encoding.ASCII.GetBytes("------------------------------------------\n"));

            // 4. Items Table
            // Header: Qty  Item Detail          Price
            // Width 80mm is approx 42-48 chars. Let's use 42 as safe baseline.
            // Qty(5) Item(27) Total(10) = 42
            bytes.AddRange(new byte[] { 0x1B, 0x45, 0x01 }); // Bold on
            bytes.AddRange(Encoding.ASCII.GetBytes("Qty  Items                       Total\n"));
            bytes.AddRange(new byte[] { 0x1B, 0x45, 0x00 }); // Bold off
            bytes.AddRange(Encoding.ASCII.GetBytes("------------------------------------------\n"));

            foreach (var item in sale.SaleItems)
            {
                string qty = item.Quantity.ToString().PadRight(5);
                string name = (item.Name.Length > 25 ? item.Name.Substring(0, 25) : item.Name).PadRight(27);
                string total = item.Price.ToString("N0").PadLeft(10);
                bytes.AddRange(Encoding.ASCII.GetBytes($"{qty}{name}{total}\n"));
            }
            bytes.AddRange(Encoding.ASCII.GetBytes("------------------------------------------\n"));

            // 5. Financial Summary: Right align
            bytes.AddRange(new byte[] { 0x1B, 0x61, 0x02 }); // Right align
            
            bytes.AddRange(Encoding.ASCII.GetBytes($"Total Now ------ {sale.TotalAmount:N0}\n"));
            
            if (sale.CustomerId != null)
            {
                bytes.AddRange(new byte[] { 0x1B, 0x45, 0x01 }); // Bold on
                bytes.AddRange(Encoding.ASCII.GetBytes($"PREVIOUS DUES:   Rs {sale.PreviousDues:N0}\n"));
                bytes.AddRange(new byte[] { 0x1B, 0x45, 0x00 }); // Bold off
                
                decimal grandTotal = sale.TotalAmount + sale.PreviousDues;
                bytes.AddRange(Encoding.ASCII.GetBytes($"SUBTOTAL:        Rs {grandTotal:N0}\n"));
            }

            bytes.AddRange(new byte[] { 0x1B, 0x45, 0x01 }); // Bold on
            bytes.AddRange(Encoding.ASCII.GetBytes($"Pay Now :        Rs {sale.AmountPaid:N0}\n"));
            bytes.AddRange(new byte[] { 0x1B, 0x45, 0x00 }); // Bold off
            
            bytes.AddRange(Encoding.ASCII.GetBytes($"Remaining --     Rs {sale.BalanceDue:N0}\n"));
            
            bytes.AddRange(Encoding.ASCII.GetBytes("\n"));

            // 6. Footer: Center
            bytes.AddRange(new byte[] { 0x1B, 0x61, 0x01 }); // Center
            bytes.AddRange(Encoding.ASCII.GetBytes("------------------------------------------\n"));
            bytes.AddRange(Encoding.ASCII.GetBytes("Thank you for your business!\n"));
            bytes.AddRange(Encoding.ASCII.GetBytes("Haji Rehman Zia: 0306-8924806\n"));
            bytes.AddRange(Encoding.ASCII.GetBytes("Software by: Antigravity AI\n"));
            bytes.AddRange(new byte[] { 0x0A, 0x0A, 0x0A, 0x0A, 0x0A }); // Feed paper

            // 7. Cut Paper
            bytes.AddRange(new byte[] { 0x1D, 0x56, 0x00 }); // Full cut

            return bytes.ToArray();
        }
    }
}
