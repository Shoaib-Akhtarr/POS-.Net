using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows.Controls;
using POS.Models;

namespace POS.Services
{
    public static class ExportService
    {
        public static string ExportSalesToCsv(
            System.Collections.ObjectModel.ObservableCollection<POS.Models.Sale> sales,
            DateTime? filterStartDate = null,
            DateTime? filterEndDate = null,
            int? filterCustomerId = null,
            string? exportPath = null)
        {
            try
            {
                var sb = new StringBuilder();
                sb.AppendLine("Export Date,Export Time");
                sb.AppendLine(DateTime.Now.ToString("yyyy-MM-dd") + "," + DateTime.Now.ToString("HH:mm:ss"));
                sb.AppendLine();

                sb.AppendLine("ReceiptNumber,RecordType,Date,CustomerName,CustomerId,PaymentMethod,");
                sb.AppendLine("Subtotal,Discount,TotalAmount,AmountPaid,BalanceDue,");
                sb.AppendLine("ItemsCount,FbrStatus,FbrInvoiceNumber");

                var filtered = sales.Where(s =>
                {
                    if (filterStartDate.HasValue && s.CreatedAt < filterStartDate.Value)
                        return false;
                    if (filterEndDate.HasValue && s.CreatedAt > filterEndDate.Value)
                        return false;
                    if (filterCustomerId.HasValue && s.CustomerId != filterCustomerId.Value)
                        return false;
                    return true;
                }).ToList();

                foreach (var sale in filtered)
                {
                    var customerName = sale.CustomerName ?? "Walk-in";
                    var itemsCount = sale.SaleItems?.Count ?? 0;

                    string Sanitize(string value) =>
                        string.IsNullOrEmpty(value) ? "" : value.Replace(",", " ").Replace("\"", "'");

                    sb.AppendLine(Sanitize(sale.ReceiptNumber) + "," + Sanitize(sale.RecordType) + ",");
                    sb.AppendLine(Sanitize(sale.CreatedAt.ToString("yyyy-MM-dd HH:mm:ss")) + "," + Sanitize(customerName) + ",");
                    sb.AppendLine((sale.CustomerId?.ToString() ?? "") + "," + Sanitize(sale.PaymentMethod) + ",");
                    sb.AppendLine(sale.TotalAmount.ToString("0.##") + "," + sale.Discount.ToString("0.##") + ",");
                    sb.AppendLine(sale.TotalAmount.ToString("0.##") + "," + sale.AmountPaid.ToString("0.##") + ",");
                    sb.AppendLine(sale.BalanceDue.ToString("0.##") + "," + itemsCount.ToString() + ",");
                    sb.AppendLine(Sanitize(sale.FbrStatus) + "," + Sanitize(sale.FbrInvoiceNumber));
                }

                var timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
                var fileName = "Sales_Export_" + timestamp + ".csv";
                var finalPath = exportPath ?? System.IO.Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.Desktop),
                    fileName);

                System.IO.File.WriteAllText(finalPath, sb.ToString(), new System.Text.UTF8Encoding(false));

                return finalPath;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("[Export] Failed: " + ex.Message);
                return string.Empty;
            }
        }

        public static string ExportDataGridToCsv(DataGrid dataGrid, bool includeHeaders = true)
        {
            var sb = new StringBuilder();

            if (includeHeaders)
            {
                var headerRow = "";
                foreach (var column in dataGrid.Columns)
                {
                    if (column.Visibility == System.Windows.Visibility.Collapsed) continue;
                    headerRow += column.Header?.ToString() + ",";
                }
                sb.AppendLine(headerRow.TrimEnd(','));
            }

            var itemsSource = dataGrid.ItemsSource as System.Collections.IEnumerable;
            if (itemsSource != null)
            {
                foreach (var item in itemsSource)
                {
                    var columnValues = new List<string>();
                    foreach (var column in dataGrid.Columns)
                    {
                        if (column.Visibility == System.Windows.Visibility.Collapsed) continue;

                        string cellValue = " ";
                        // Get the value by binding path from the column
                        var bindingPath = column.SortMemberPath ?? "";
                        if (!string.IsNullOrEmpty(bindingPath))
                        {
                            var prop = item.GetType().GetProperty(bindingPath);
                            if (prop != null)
                            {
                                var value = prop.GetValue(item);
                                cellValue = value != null ? value.ToString() : "";
                            }
                        }
                        else
                        {
                            // Fallback: find the content presenter's text
                            var content = column.GetCellContent(item);
                            if (content != null)
                                cellValue = content.ToString();
                        }
                        columnValues.Add(cellValue);
                    }
                    sb.AppendLine(string.Join(",", columnValues));
                }
            }

            return sb.ToString();
        }

        public static string GetExportPath(string shopName = "POS", DateTime? date = null)
        {
            date ??= DateTime.Today;

            string safeDate = date.Value.ToString("yyyyMMdd");
            string folder = System.IO.Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.Desktop),
                "KarobarPOS_Reports");

            Directory.CreateDirectory(folder);
            return System.IO.Path.Combine(folder, "Report_" + shopName + "_" + safeDate + ".csv");
        }
    }
}
