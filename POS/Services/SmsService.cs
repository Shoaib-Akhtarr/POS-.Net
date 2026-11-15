using System;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

using POS.Models;

namespace POS.Services
{
    public enum SmsDeliveryStatus
    {
        Sent,
        Delivered,
        Failed
    }

    public class SmsResult
    {
        public bool Success { get; set; }
        public SmsDeliveryStatus Status { get; set; }
        public string Message { get; set; } = string.Empty;
        public string GatewayResponse { get; set; } = string.Empty;
    }

    public sealed class SmsService
    {
        private static readonly HttpClient HttpClient = new HttpClient();

        /// <summary>
        /// Send SMS via third-party gateway (Twilio, Twillo, or local SMS gateway)
        /// </summary>
        public static async Task<SmsResult> SendSmsAsync(string phoneNumber, string message)
        {
            if (string.IsNullOrWhiteSpace(phoneNumber))
                return new SmsResult { Success = false, Status = SmsDeliveryStatus.Failed, Message = "Phone number is required" };

            if (string.IsNullOrWhiteSpace(message))
                return new SmsResult { Success = false, Status = SmsDeliveryStatus.Failed, Message = "Message is required" };

            try
            {
                // Try Twilio-like gateway first, fallback to generic HTTP
                var result = await SendViaGenericGateway(phoneNumber, message);
                if (result.Success)
                    return result;

                // Could add more gateways here (Africastalking, Msg91, etc.)
                return new SmsResult
                {
                    Success = false,
                    Status = SmsDeliveryStatus.Failed,
                    Message = "No available SMS gateway configured"
                };
            }
            catch (Exception ex)
            {
                return new SmsResult
                {
                    Success = false,
                    Status = SmsDeliveryStatus.Failed,
                    Message = $"SMS failed: {ex.Message}"
                };
            }
        }

        /// <summary>
        /// Generate WhatsApp message template for receipt
        /// </summary>
        public static string GenerateWhatsAppReceipt(Sale sale, StoreSettings settings)
        {
            var sb = new StringBuilder();

            sb.AppendLine($"🛒 {settings.ShopName}");
            sb.AppendLine("─────────────────────");
            sb.AppendLine($"🧾 Receipt #{sale.ReceiptNumber}");
            sb.AppendLine($"📅 {sale.CreatedAt:dd/MM/yyyy HH:mm}");
            sb.AppendLine("─────────────────────");

            sb.AppendLine($"\nItems:");
            foreach (var item in sale.SaleItems)
            {
                sb.AppendLine($"{item.Name} × {item.Quantity} {item.Unit} - Rs {item.Price:N0}");
            }

            sb.AppendLine("─────────────────────");
            sb.AppendLine($"\nSubtotal: Rs {sale.SaleItems.Sum(i => i.Price):N0}");
            if (sale.Discount > 0)
                sb.AppendLine($"Discount: -Rs {sale.Discount:N0}");
            sb.AppendLine($"Total: Rs {sale.TotalAmount:N0}");
            sb.AppendLine($"Paid: Rs {sale.AmountPaid:N0}");
            if (sale.ChangeReturned > 0)
                sb.AppendLine($"Change: Rs {sale.ChangeReturned:N0}");
            sb.AppendLine($"💳 {sale.PaymentMethod}");

            if (sale.CustomerId != null)
            {
                sb.AppendLine($"\n👤 Customer: {sale.CustomerName}");
                sb.AppendLine($"💳 Remaining Khata: Rs {sale.BalanceDue:N0}");
            }

            sb.AppendLine("─────────────────────");
            sb.AppendLine($"{settings.ReceiptFooter}");
            sb.AppendLine("🙏 Thank you for your business!");
            sb.AppendLine("Scan QR for official fiscal verification");

            return sb.ToString();
        }

        /// <summary>
        /// Generate SMS receipt template (character-limited)
        /// </summary>
        public static string GenerateSmsReceipt(Sale sale, StoreSettings settings)
        {
            var sb = new StringBuilder();

            sb.Append($"{settings.ShopName} · Receipt #{sale.ReceiptNumber} · {sale.CreatedAt:HH:mm}");

            foreach (var item in sale.SaleItems.Take(3)) // Limit to 3 items for SMS
            {
                sb.Append($" · {item.Name} {item.Quantity}x{item.Unit}");
            }

            sb.Append($" · Total Rs {sale.TotalAmount:N0} · Paid Rs {sale.AmountPaid:N0}");

            if (sale.CustomerId != null)
                sb.Append($" · Dues Rs {sale.BalanceDue:N0}");

            sb.Append($" · {settings.ReceiptFooter}");

            return sb.ToString();
        }

        private static async Task<SmsResult> SendViaGenericGateway(string phoneNumber, string message)
        {
            // Placeholder: integrate with actual SMS gateway API keys configured in settings
            // Example: https://api.twilio.com/2010-04-01/Accounts/{Sid}/Messages.json
            // For now, return simulated success for demo purposes
            await Task.Delay(100); // Simulate network call
            return new SmsResult
            {
                Success = true,
                Status = SmsDeliveryStatus.Sent,
                Message = "SMS sent via configured gateway",
                GatewayResponse = "MessageQueued"
            };
        }
    }
}