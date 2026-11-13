using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using POS.Models;

namespace POS.Services
{
    public static class InstallmentRecoveryService
    {
        public static void AccrueLateFees(AppDbContext context, InstallmentContract contract)
        {
            if (contract.LateFeePerInstallment <= 0m || contract.Status != "Active") return;
            var overdue = context.InstallmentSchedules
                .Where(schedule => schedule.ContractId == contract.Id && schedule.Status != "Paid" && !schedule.LateFeeWaived && schedule.LateFee == 0m)
                .ToList()
                .Where(schedule => DateTime.Today > schedule.DueDate.Date.AddDays(contract.GraceDays))
                .ToList();
            if (overdue.Count == 0) return;

            var customer = context.Customers.Find(contract.CustomerId);
            if (customer == null) return;
            var account = KhataService.GetCustomerAccount(context, customer);
            foreach (var schedule in overdue)
            {
                schedule.LateFee = contract.LateFeePerInstallment;
                customer.TotalDues += schedule.LateFee;
                KhataService.Post(context, account, $"Late fee · {contract.ContractNumber} · installment {schedule.Sequence}", "Late fee",
                    schedule.LateFee, 0m, "InstallmentLateFee", $"{contract.Id}:{schedule.Id}");
            }
            customer.UpdatedAt = DateTime.Now;
        }

        public static async Task<InstallmentPayment> RecordPaymentAsync(
            AppDbContext context,
            InstallmentContract contract,
            decimal amount,
            string paymentMode,
            string notes,
            string sourceType = "InstallmentPayment",
            string? sourceKey = null)
        {
            if (contract.Status != "Active") throw new InvalidOperationException("This installment contract is not active.");
            if (amount <= 0m) throw new ArgumentOutOfRangeException(nameof(amount), "Payment must be greater than zero.");
            amount = decimal.Round(amount, 2, MidpointRounding.AwayFromZero);

            AccrueLateFees(context, contract);
            var schedules = await context.InstallmentSchedules.Where(s => s.ContractId == contract.Id)
                .OrderBy(s => s.DueDate).ThenBy(s => s.Sequence).ToListAsync();
            var allocations = InstallmentPlanService.Allocate(schedules, amount, out var splits);
            var payment = new InstallmentPayment
            {
                ContractId = contract.Id,
                ReceiptNumber = $"KRP-{DateTime.Now:yyyyMMddHHmmss}-{Guid.NewGuid():N}"[..31],
                Amount = amount,
                PaymentMode = paymentMode,
                Notes = notes.Trim(),
                CreatedBy = SessionService.CurrentUserName,
                CreatedAt = DateTime.Now
            };
            context.InstallmentPayments.Add(payment);
            await context.SaveChangesAsync();

            foreach (var allocation in allocations)
            {
                var schedule = schedules.First(item => item.Id == allocation.ScheduleId);
                var split = splits[allocation.ScheduleId];
                schedule.AmountPaid += split.Principal;
                schedule.LateFeePaid += split.Fee;
                SetScheduleStatus(schedule, contract.GraceDays);
                allocation.PaymentId = payment.Id;
                allocation.PrincipalAmount = split.Principal;
                allocation.LateFeeAmount = split.Fee;
                context.InstallmentPaymentAllocations.Add(allocation);
            }

            var customer = await context.Customers.FirstAsync(c => c.Id == contract.CustomerId);
            decimal previousDues = customer.TotalDues;
            customer.TotalDues -= amount;
            customer.UpdatedAt = DateTime.Now;
            context.Sales.Add(new Sale
            {
                RecordType = "Payment",
                CustomerId = customer.Id,
                CustomerName = customer.Name,
                TotalAmount = 0m,
                PreviousDues = previousDues,
                AmountPaid = amount,
                BalanceDue = customer.TotalDues,
                PaymentMethod = paymentMode,
                IsPaid = true,
                Notes = $"Installment receipt · {contract.ContractNumber} · {notes.Trim()}",
                CreatedBy = SessionService.CurrentUserName,
                CreatedAt = DateTime.Now,
                UpdatedAt = DateTime.Now,
                ReceiptNumber = payment.ReceiptNumber
            });
            var account = KhataService.GetCustomerAccount(context, customer);
            KhataService.Post(context, account, $"Installment recovery · {contract.ContractNumber} · {payment.ReceiptNumber}", paymentMode,
                0m, amount, sourceType, sourceKey ?? payment.Id.ToString(), payment.CreatedAt);

            if (schedules.All(schedule => schedule.Status == "Paid"))
            {
                contract.Status = "Completed";
                contract.ModifiedAt = DateTime.Now;
                contract.ModifiedReason = "All installments paid";
            }
            await context.SaveChangesAsync();
            return payment;
        }

        public static async Task ReversePaymentAsync(AppDbContext context, InstallmentPayment payment, string reason)
        {
            if (payment.IsReversed) return;
            var contract = await context.InstallmentContracts.FirstAsync(c => c.Id == payment.ContractId);
            var allocations = await context.InstallmentPaymentAllocations.Where(a => a.PaymentId == payment.Id).ToListAsync();
            foreach (var allocation in allocations)
            {
                var schedule = await context.InstallmentSchedules.FirstAsync(s => s.Id == allocation.ScheduleId);
                schedule.AmountPaid = Math.Max(0m, schedule.AmountPaid - allocation.PrincipalAmount);
                schedule.LateFeePaid = Math.Max(0m, schedule.LateFeePaid - allocation.LateFeeAmount);
                SetScheduleStatus(schedule, contract.GraceDays);
            }

            payment.IsReversed = true;
            payment.ReversedReason = reason.Trim();
            var customer = await context.Customers.FirstAsync(c => c.Id == contract.CustomerId);
            decimal previousDues = customer.TotalDues;
            customer.TotalDues += payment.Amount;
            customer.UpdatedAt = DateTime.Now;
            context.Sales.Add(new Sale
            {
                RecordType = "Payment Reversal",
                CustomerId = customer.Id,
                CustomerName = customer.Name,
                TotalAmount = 0m,
                PreviousDues = previousDues,
                AmountPaid = -payment.Amount,
                BalanceDue = customer.TotalDues,
                PaymentMethod = "Reversal",
                IsPaid = true,
                Notes = $"Reversed installment payment {payment.ReceiptNumber} · {reason.Trim()}",
                CreatedBy = SessionService.CurrentUserName,
                CreatedAt = DateTime.Now,
                UpdatedAt = DateTime.Now,
                ReceiptNumber = $"REV-{payment.ReceiptNumber}"
            });
            if (contract.Status == "Completed") contract.Status = "Active";
            contract.ModifiedAt = DateTime.Now;
            contract.ModifiedReason = $"Payment {payment.ReceiptNumber} reversed: {reason.Trim()}";
            var account = KhataService.GetCustomerAccount(context, customer);
            KhataService.Post(context, account, $"Reversal · {payment.ReceiptNumber} · {reason.Trim()}", "Reversal",
                payment.Amount, 0m, "InstallmentPaymentReversal", payment.Id.ToString());
        }

        public static void SetScheduleStatus(InstallmentSchedule schedule, int graceDays)
        {
            bool principalPaid = schedule.AmountPaid >= schedule.AmountDue;
            bool feePaid = schedule.LateFeeWaived || schedule.LateFeePaid >= schedule.LateFee;
            if (principalPaid && feePaid)
                schedule.Status = "Paid";
            else if (DateTime.Today > schedule.DueDate.Date.AddDays(graceDays))
                schedule.Status = "Overdue";
            else if (schedule.DueDate.Date <= DateTime.Today)
                schedule.Status = "Due";
            else
                schedule.Status = "Upcoming";
        }
    }
}
