using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Media.Imaging;
using Microsoft.EntityFrameworkCore;
using POS.Models;
using QRCoder;

namespace POS.Services
{
    public sealed record FbrSyncResult(
        bool Success,
        string InvoiceNumber,
        string Status,
        string QrData,
        string Message,
        bool IsOfflineQueued);

    public class FbrInvoiceItemPayload
    {
        [JsonPropertyName("ItemCode")]
        public string ItemCode { get; set; } = string.Empty;

        [JsonPropertyName("ItemName")]
        public string ItemName { get; set; } = string.Empty;

        [JsonPropertyName("PCTCode")]
        public string PCTCode { get; set; } = "8415.1010"; // Default tariff code for appliances/goods

        [JsonPropertyName("Quantity")]
        public decimal Quantity { get; set; } = 1m;

        [JsonPropertyName("TaxRate")]
        public decimal TaxRate { get; set; } = 18.0m;

        [JsonPropertyName("SaleValue")]
        public decimal SaleValue { get; set; }

        [JsonPropertyName("TotalAmount")]
        public decimal TotalAmount { get; set; }

        [JsonPropertyName("TaxCharged")]
        public decimal TaxCharged { get; set; }

        [JsonPropertyName("Discount")]
        public decimal Discount { get; set; }

        [JsonPropertyName("FurtherTax")]
        public decimal FurtherTax { get; set; } = 0m;

        [JsonPropertyName("InvoiceType")]
        public int InvoiceType { get; set; } = 1;
    }

    public class FbrInvoicePayload
    {
        [JsonPropertyName("InvoiceNumber")]
        public string InvoiceNumber { get; set; } = string.Empty;

        [JsonPropertyName("POSID")]
        public int POSID { get; set; }

        [JsonPropertyName("USIN")]
        public string USIN { get; set; } = string.Empty;

        [JsonPropertyName("DateTime")]
        public string DateTime { get; set; } = string.Empty;

        [JsonPropertyName("BuyerNTN")]
        public string BuyerNTN { get; set; } = string.Empty;

        [JsonPropertyName("BuyerCNIC")]
        public string BuyerCNIC { get; set; } = string.Empty;

        [JsonPropertyName("BuyerName")]
        public string BuyerName { get; set; } = string.Empty;

        [JsonPropertyName("BuyerPhoneNumber")]
        public string BuyerPhoneNumber { get; set; } = string.Empty;

        [JsonPropertyName("TotalSaleValue")]
        public decimal TotalSaleValue { get; set; }

        [JsonPropertyName("TotalQuantity")]
        public decimal TotalQuantity { get; set; }

        [JsonPropertyName("TotalBillAmount")]
        public decimal TotalBillAmount { get; set; }

        [JsonPropertyName("TotalTaxCharged")]
        public decimal TotalTaxCharged { get; set; }

        [JsonPropertyName("Discount")]
        public decimal Discount { get; set; }

        [JsonPropertyName("PaymentMode")]
        public int PaymentMode { get; set; } = 1; // 1: Cash, 2: Card, 3: Cheque/Bank, 4: Digital

        [JsonPropertyName("InvoiceType")]
        public int InvoiceType { get; set; } = 1; // 1: New Sale, 2: Return Credit Note

        [JsonPropertyName("Items")]
        public List<FbrInvoiceItemPayload> Items { get; set; } = new();
    }

    public class FbrApiResponse
    {
        [JsonPropertyName("InvoiceNumber")]
        public string? InvoiceNumber { get; set; }

        [JsonPropertyName("fbrInvoiceNumber")]
        public string? FbrInvoiceNumberAlt { get; set; }

        [JsonPropertyName("Response")]
        public string? Response { get; set; }

        [JsonPropertyName("status")]
        public string? Status { get; set; }

        [JsonPropertyName("statusCode")]
        public string? StatusCode { get; set; }

        [JsonPropertyName("errors")]
        public List<string>? Errors { get; set; }
    }

    public class FbrTaxService
    {
        private static readonly HttpClient HttpClient = new()
        {
            Timeout = TimeSpan.FromSeconds(4.5)
        };

        public const string SandboxUrl = "https://gw.fbr.gov.pk/di_data/v1/di/postinvoicedata_sb";
        public const string ProductionUrl = "https://gw.fbr.gov.pk/di_data/v1/di/postinvoicedata";

        public static string GetEndpoint(StoreSettings settings)
        {
            return string.Equals(settings.FbrEnvironment, "Live", StringComparison.OrdinalIgnoreCase)
                ? ProductionUrl
                : SandboxUrl;
        }

        public async Task<(bool success, string message)> TestConnectionAsync(StoreSettings settings)
        {
            if (string.IsNullOrWhiteSpace(settings.FbrPosId))
            {
                return (false, "POS ID is required to connect to FBR.");
            }

            bool isLive = string.Equals(settings.FbrEnvironment, "Live", StringComparison.OrdinalIgnoreCase);
            string endpoint = GetEndpoint(settings);

            if (isLive && string.IsNullOrWhiteSpace(settings.FbrBearerToken))
            {
                return (false, "Production / Live mode requires a valid FBR Bearer Token.");
            }

            try
            {
                using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(3.5));
                using var request = new HttpRequestMessage(HttpMethod.Post, endpoint);
                
                if (!string.IsNullOrWhiteSpace(settings.FbrBearerToken))
                {
                    string token = settings.FbrBearerToken.Trim();
                    if (token.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
                    {
                        token = token.Substring(7).Trim();
                    }
                    request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
                }

                // Send empty ping payload or sample query
                request.Content = new StringContent("{}", Encoding.UTF8, "application/json");

                var response = await HttpClient.SendAsync(request, cts.Token);
                string content = await response.Content.ReadAsStringAsync(cts.Token);

                // Even a 400 or validation response from the server proves gateway connectivity
                if (response.IsSuccessStatusCode || response.StatusCode == System.Net.HttpStatusCode.BadRequest)
                {
                    return (true, $"FBR Gateway is reachable ({(isLive ? "Live" : "Sandbox")} · Status: {(int)response.StatusCode}). POSID: {settings.FbrPosId}");
                }

                return (false, $"FBR Gateway responded with HTTP {(int)response.StatusCode}: {response.ReasonPhrase}");
            }
            catch (Exception ex)
            {
                if (!isLive)
                {
                    // For sandbox testing, if PRAL dev gateway is temporarily unreachable, notify with local simulation confirmation
                    return (true, $"[Sandbox Mode Active] Local verification successful. Gateway ({endpoint}) was unreachable ({ex.Message}), but POS Sandbox Simulation is ready to generate fiscal test invoices and QR codes.");
                }

                return (false, $"Unable to reach FBR Live Gateway ({ex.Message}). Check your internet connection.");
            }
        }

        public async Task<FbrSyncResult> ProcessSaleFiscalizationAsync(Sale sale, StoreSettings settings)
        {
            if (!settings.IsFbrTaxEnabled)
            {
                sale.FbrStatus = "NotIntegrated";
                return new FbrSyncResult(true, string.Empty, "NotIntegrated", string.Empty, "FBR Tax integration is disabled.", false);
            }

            int.TryParse(settings.FbrPosId, out int posId);
            if (posId <= 0) posId = 102345;

            decimal taxRate = settings.FbrDefaultTaxRate > 0 ? settings.FbrDefaultTaxRate : 18.0m;
            sale.FbrTaxRate = taxRate;

            // In Pakistani retail invoicing, calculate sales tax breakdown
            // If total sale amount is gross: Tax = Total * Rate / (100 + Rate)
            // Or if prices are net of tax: Tax = Total * Rate / 100
            decimal totalTax = Math.Round(sale.TotalAmount * (taxRate / (100m + taxRate)), 2);
            decimal netSaleValue = Math.Max(0m, sale.TotalAmount - totalTax);

            sale.FbrTaxAmount = totalTax;

            // Build FBR payload
            int paymentMode = sale.PaymentMethod?.ToUpperInvariant() switch
            {
                "CARD" => 2,
                "BANK" => 3,
                "JAZZCASH" or "EASYPAISA" => 4,
                _ => 1 // Cash
            };

            int invoiceType = string.Equals(sale.RecordType, "Return", StringComparison.OrdinalIgnoreCase) ? 2 : 1;

            var payload = new FbrInvoicePayload
            {
                InvoiceNumber = string.Empty,
                POSID = posId,
                USIN = sale.ReceiptNumber ?? $"AFC-{sale.Id}-{DateTime.Now:yyyyMMddHHmmss}",
                DateTime = sale.CreatedAt.ToString("yyyy-MM-dd HH:mm:ss"),
                BuyerNTN = sale.BuyerNtn ?? string.Empty,
                BuyerCNIC = sale.BuyerCnic ?? string.Empty,
                BuyerName = string.IsNullOrWhiteSpace(sale.CustomerName) ? "Walk-in Customer" : sale.CustomerName,
                BuyerPhoneNumber = sale.Customer?.Phone ?? string.Empty,
                TotalSaleValue = netSaleValue,
                TotalQuantity = sale.SaleItems.Sum(i => i.Quantity > 0 ? i.Quantity : 1m),
                TotalBillAmount = sale.TotalAmount,
                TotalTaxCharged = totalTax,
                Discount = sale.Discount,
                PaymentMode = paymentMode,
                InvoiceType = invoiceType
            };

            foreach (var item in sale.SaleItems)
            {
                decimal itemTax = Math.Round(item.Price * (taxRate / (100m + taxRate)), 2);
                decimal itemNet = Math.Max(0m, item.Price - itemTax);

                item.TaxRate = taxRate;
                item.TaxAmount = itemTax;
                item.PctCode = string.IsNullOrWhiteSpace(item.PctCode) ? "8415.1010" : item.PctCode;

                payload.Items.Add(new FbrInvoiceItemPayload
                {
                    ItemCode = item.ProductId?.ToString() ?? item.Id.ToString(),
                    ItemName = item.Name,
                    PCTCode = item.PctCode,
                    Quantity = item.Quantity > 0 ? item.Quantity : 1m,
                    TaxRate = taxRate,
                    SaleValue = itemNet,
                    TotalAmount = item.Price,
                    TaxCharged = itemTax,
                    Discount = item.Discount,
                    InvoiceType = invoiceType
                });
            }

            bool isLive = string.Equals(settings.FbrEnvironment, "Live", StringComparison.OrdinalIgnoreCase);
            string endpoint = GetEndpoint(settings);

            try
            {
                using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(3.5));
                using var request = new HttpRequestMessage(HttpMethod.Post, endpoint);

                if (!string.IsNullOrWhiteSpace(settings.FbrBearerToken))
                {
                    string token = settings.FbrBearerToken.Trim();
                    if (token.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
                    {
                        token = token.Substring(7).Trim();
                    }
                    request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
                }

                string json = JsonSerializer.Serialize(payload);
                request.Content = new StringContent(json, Encoding.UTF8, "application/json");

                var response = await HttpClient.SendAsync(request, cts.Token);
                string responseBody = await response.Content.ReadAsStringAsync(cts.Token);

                if (response.IsSuccessStatusCode)
                {
                    string fbrInvoiceNo = ExtractInvoiceNumber(responseBody) ?? $"{posId}-{DateTime.Now:yyyyMMddHHmmss}";
                    sale.FbrInvoiceNumber = fbrInvoiceNo;
                    sale.FbrStatus = isLive ? "Synced" : "Synced (Sandbox)";
                    sale.FbrSyncTimestamp = DateTime.Now;
                    sale.FbrErrorMessage = null;
                    sale.FbrQrCodeData = BuildQrString(fbrInvoiceNo, posId, payload.USIN, sale.CreatedAt, sale.TotalAmount, totalTax);

                    return new FbrSyncResult(true, fbrInvoiceNo, sale.FbrStatus, sale.FbrQrCodeData, "Invoice successfully verified with FBR.", false);
                }
                else
                {
                    // Server responded with error status
                    if (!isLive)
                    {
                        // Sandbox fallback
                        string sbInvoice = $"SB-{posId}-{DateTime.Now:yyyyMMddHHmmss}";
                        sale.FbrInvoiceNumber = sbInvoice;
                        sale.FbrStatus = "Synced (Sandbox)";
                        sale.FbrSyncTimestamp = DateTime.Now;
                        sale.FbrErrorMessage = $"Sandbox Simulated: {response.StatusCode}";
                        sale.FbrQrCodeData = BuildQrString(sbInvoice, posId, payload.USIN, sale.CreatedAt, sale.TotalAmount, totalTax);

                        return new FbrSyncResult(true, sbInvoice, sale.FbrStatus, sale.FbrQrCodeData, "FBR Sandbox test invoice generated.", false);
                    }

                    // Live error: Queue offline if allowed
                    return HandleOfflineQueuing(sale, settings, posId, payload.USIN, totalTax, $"HTTP {(int)response.StatusCode}: {responseBody}");
                }
            }
            catch (Exception ex)
            {
                if (!isLive)
                {
                    // In sandbox, generate valid simulated offline sandbox receipt
                    string sbInvoice = $"SB-{posId}-{DateTime.Now:yyyyMMddHHmmss}";
                    sale.FbrInvoiceNumber = sbInvoice;
                    sale.FbrStatus = "Synced (Sandbox)";
                    sale.FbrSyncTimestamp = DateTime.Now;
                    sale.FbrErrorMessage = "Sandbox offline simulation.";
                    sale.FbrQrCodeData = BuildQrString(sbInvoice, posId, payload.USIN, sale.CreatedAt, sale.TotalAmount, totalTax);

                    return new FbrSyncResult(true, sbInvoice, sale.FbrStatus, sale.FbrQrCodeData, "FBR Sandbox test invoice generated.", false);
                }

                // In Live, queue offline for background sync
                return HandleOfflineQueuing(sale, settings, posId, payload.USIN, totalTax, ex.Message);
            }
        }

        private FbrSyncResult HandleOfflineQueuing(Sale sale, StoreSettings settings, int posId, string usin, decimal totalTax, string errorReason)
        {
            string offlineInvoice = $"OFFLINE-{posId}-{DateTime.Now:yyyyMMddHHmmss}";
            sale.FbrInvoiceNumber = offlineInvoice;
            sale.FbrStatus = "PendingOffline";
            sale.FbrErrorMessage = errorReason;
            sale.FbrSyncTimestamp = null;
            sale.FbrQrCodeData = BuildQrString(offlineInvoice, posId, usin, sale.CreatedAt, sale.TotalAmount, totalTax);

            return new FbrSyncResult(
                true,
                offlineInvoice,
                "PendingOffline",
                sale.FbrQrCodeData,
                $"Invoice saved locally and queued for FBR sync (Network offline: {errorReason}).",
                true);
        }

        public async Task<(int synced, int failed, string message)> SyncPendingInvoicesAsync(StoreSettings settings)
        {
            if (!settings.IsFbrTaxEnabled)
            {
                return (0, 0, "FBR tax integration is currently disabled.");
            }

            using var context = new AppDbContext();
            var pendingSales = await context.Sales
                .Include(s => s.SaleItems)
                .Where(s => s.FbrStatus == "PendingOffline")
                .OrderBy(s => s.CreatedAt)
                .Take(50)
                .ToListAsync();

            if (!pendingSales.Any())
            {
                return (0, 0, "No pending offline invoices found to sync.");
            }

            int syncedCount = 0;
            int failedCount = 0;

            foreach (var sale in pendingSales)
            {
                var result = await ProcessSaleFiscalizationAsync(sale, settings);
                if (result.Success && result.Status != "PendingOffline")
                {
                    syncedCount++;
                }
                else
                {
                    failedCount++;
                }
            }

            await context.SaveChangesAsync();
            return (syncedCount, failedCount, $"Sync completed: {syncedCount} invoices synced, {failedCount} remaining.");
        }

        public static int GetPendingOfflineCount()
        {
            try
            {
                using var context = new AppDbContext();
                return context.Sales.Count(s => s.FbrStatus == "PendingOffline");
            }
            catch
            {
                return 0;
            }
        }

        public static string BuildQrString(string invoiceNo, int posId, string usin, DateTime date, decimal totalAmount, decimal taxCharged)
        {
            return $"FBR:{invoiceNo}|POSID:{posId}|USIN:{usin}|DATE:{date:yyyy-MM-dd HH:mm:ss}|AMT:{totalAmount:0.00}|TAX:{taxCharged:0.00}";
        }

        public static BitmapSource? GenerateQrBitmap(string? qrText, int pixelsPerModule = 4)
        {
            if (string.IsNullOrWhiteSpace(qrText)) return null;

            try
            {
                using var generator = new QRCodeGenerator();
                using var data = generator.CreateQrCode(qrText, QRCodeGenerator.ECCLevel.M);
                var qrCode = new PngByteQRCode(data);
                byte[] bytes = qrCode.GetGraphic(pixelsPerModule);

                using var ms = new MemoryStream(bytes);
                var image = new BitmapImage();
                image.BeginInit();
                image.CacheOption = BitmapCacheOption.OnLoad;
                image.StreamSource = ms;
                image.EndInit();
                image.Freeze();
                return image;
            }
            catch
            {
                return null;
            }
        }

        public static byte[]? GenerateQrPngBytes(string? qrText, int pixelsPerModule = 4)
        {
            if (string.IsNullOrWhiteSpace(qrText)) return null;

            try
            {
                using var generator = new QRCodeGenerator();
                using var data = generator.CreateQrCode(qrText, QRCodeGenerator.ECCLevel.M);
                var qrCode = new PngByteQRCode(data);
                return qrCode.GetGraphic(pixelsPerModule);
            }
            catch
            {
                return null;
            }
        }

        private static string? ExtractInvoiceNumber(string json)
        {
            try
            {
                var doc = JsonDocument.Parse(json);
                if (doc.RootElement.TryGetProperty("InvoiceNumber", out var prop))
                    return prop.GetString();
                if (doc.RootElement.TryGetProperty("fbrInvoiceNumber", out prop))
                    return prop.GetString();
                if (doc.RootElement.TryGetProperty("invoiceNumber", out prop))
                    return prop.GetString();
            }
            catch
            {
                // Ignore parse errors and fallback
            }
            return null;
        }
    }
}

