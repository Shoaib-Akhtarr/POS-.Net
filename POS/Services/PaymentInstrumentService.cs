using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using POS.Models;

namespace POS.Services
{
    public static class PaymentInstrumentService
    {
        public static async Task RecordAsync(AppDbContext context, PaymentInstrument instrument)
        {
            if (instrument.Amount <= 0m) throw new ArgumentOutOfRangeException(nameof(instrument.Amount), "Payment must be greater than zero.");
            if (instrument.Direction is not ("In" or "Out")) throw new ArgumentOutOfRangeException(nameof(instrument.Direction));
            if (instrument.ClearingDate == default) throw new ArgumentException("Choose the cheque clearing date.");
            if (string.IsNullOrWhiteSpace(instrument.ChequeNumber) || string.IsNullOrWhiteSpace(instrument.BankName))
                throw new ArgumentException("Cheque number and bank name are required for a PDC.");
            if (instrument.InstallmentContractId.HasValue)
            {
                if (instrument.Direction != "In") throw new ArgumentException("An installment cheque must be incoming.");
                var contract = await context.InstallmentContracts.FirstOrDefaultAsync(c => c.Id == instrument.InstallmentContractId.Value && c.Status == "Active");
                var account = await context.KhataAccounts.FirstOrDefaultAsync(a => a.Id == instrument.AccountId);
                if (contract == null || account?.CustomerId != contract.CustomerId)
                    throw new ArgumentException("Select an active contract for the selected customer account.");
            }

            instrument.Amount = decimal.Round(instrument.Amount, 2, MidpointRounding.AwayFromZero);
            instrument.PaymentMode = "PDC";
            instrument.Status = "Pending";
            instrument.CreatedAt = DateTime.Now;
            instrument.CreatedBy = SessionService.CurrentUserName;
            context.PaymentInstruments.Add(instrument);
            await context.SaveChangesAsync();
        }

        public static async Task TransitionAsync(AppDbContext context, long instrumentId, string newStatus, string reason)
        {
            if (newStatus is not ("Cleared" or "Bounced")) throw new ArgumentOutOfRangeException(nameof(newStatus));
            if (newStatus == "Bounced" && string.IsNullOrWhiteSpace(reason))
                throw new ArgumentException("Enter the reason for the bounced cheque.", nameof(reason));

            await using var transaction = await context.Database.BeginTransactionAsync();
            var instrument = await context.PaymentInstruments.FirstOrDefaultAsync(p => p.Id == instrumentId);
            if (instrument == null) throw new InvalidOperationException("The selected cheque could not be found.");
            if (instrument.Status == newStatus) return;
            if (instrument.Status == "Bounced") throw new InvalidOperationException("A bounced cheque cannot be cleared or changed.");

            var account = await context.KhataAccounts.FirstAsync(a => a.Id == instrument.AccountId);
            if (newStatus == "Cleared")
            {
                if (instrument.Status != "Pending") throw new InvalidOperationException("Only a pending cheque can be cleared.");
                if (instrument.InstallmentContractId.HasValue)
                {
                    if (instrument.Direction != "In" || account.CustomerId == null)
                        throw new InvalidOperationException("An installment PDC must be an incoming cheque for a customer.");
                    var contract = await context.InstallmentContracts.FirstAsync(c => c.Id == instrument.InstallmentContractId.Value);
                    if (contract.CustomerId != account.CustomerId.Value)
                        throw new InvalidOperationException("The cheque and installment contract belong to different customers.");
                    var payment = await InstallmentRecoveryService.RecordPaymentAsync(context, contract, instrument.Amount, "PDC",
                        instrument.Notes, "PDC", instrument.Id.ToString());
                    instrument.InstallmentPaymentId = payment.Id;
                    instrument.KhataEntryId = await context.KhataEntries.Where(entry => entry.SourceType == "PDC" && entry.SourceKey == instrument.Id.ToString())
                        .Select(entry => (long?)entry.Id).FirstAsync();
                }
                else
                {
                    var entry = KhataService.Post(
                        context,
                        account,
                        $"PDC cleared · {instrument.ChequeNumber} · {instrument.BankName}",
                        "PDC",
                        instrument.Direction == "Out" ? instrument.Amount : 0m,
                        instrument.Direction == "In" ? instrument.Amount : 0m,
                        "PDC",
                        instrument.Id.ToString());
                    await context.SaveChangesAsync();
                    instrument.KhataEntryId = entry.Id;
                    await ApplyCustomerBalanceDelta(context, account, entry.Debit - entry.Credit);
                }
            }
            else if (instrument.Status == "Cleared" && instrument.KhataEntryId.HasValue)
            {
                if (instrument.InstallmentPaymentId.HasValue)
                {
                    var payment = await context.InstallmentPayments.FirstAsync(p => p.Id == instrument.InstallmentPaymentId.Value);
                    await InstallmentRecoveryService.ReversePaymentAsync(context, payment, reason);
                }
                else
                {
                    var clearedEntry = await context.KhataEntries.FindAsync(instrument.KhataEntryId.Value)
                        ?? throw new InvalidOperationException("The cleared cheque posting is missing.");
                    var reversal = KhataService.Post(
                        context,
                        account,
                        $"PDC bounced · {instrument.ChequeNumber} · {instrument.BankName} · {reason.Trim()}",
                        "Reversal",
                        clearedEntry.Credit,
                        clearedEntry.Debit,
                        "PDCBounce",
                        instrument.Id.ToString(),
                        reversesEntryId: clearedEntry.Id);
                    await ApplyCustomerBalanceDelta(context, account, reversal.Debit - reversal.Credit);
                }
            }

            instrument.Status = newStatus;
            instrument.ModifiedAt = DateTime.Now;
            instrument.ModifiedReason = reason.Trim();
            if (newStatus == "Cleared" && string.IsNullOrWhiteSpace(instrument.ModifiedReason))
                instrument.ModifiedReason = "Cheque cleared";
            await context.SaveChangesAsync();
            await transaction.CommitAsync();
        }

        public static async Task ApplyCustomerBalanceDelta(AppDbContext context, KhataAccount account, decimal debitMinusCredit)
        {
            if (account.CustomerId is not int customerId) return;
            var customer = await context.Customers.FirstAsync(c => c.Id == customerId);
            customer.TotalDues += debitMinusCredit;
            customer.UpdatedAt = DateTime.Now;
        }
    }
}
