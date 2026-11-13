using System;
using System.Collections.Generic;
using System.Linq;
using POS.Models;

namespace POS.Services
{
    public sealed record InstallmentPlanInput(
        decimal CashPrice,
        decimal MarkupAmount,
        decimal ProcessingFee,
        decimal DownPayment,
        int Count,
        string Frequency,
        int CustomIntervalDays,
        DateTime FirstDueDate);

    public sealed record PlannedInstallment(int Sequence, DateTime DueDate, decimal AmountDue);

    public static class InstallmentPlanService
    {
        public static IReadOnlyList<PlannedInstallment> BuildSchedule(InstallmentPlanInput plan)
        {
            if (plan.CashPrice <= 0m) throw new ArgumentOutOfRangeException(nameof(plan.CashPrice), "Cash price must be greater than zero.");
            if (plan.MarkupAmount < 0m || plan.ProcessingFee < 0m || plan.DownPayment < 0m)
                throw new ArgumentOutOfRangeException(nameof(plan), "Markup, fee, and down payment cannot be negative.");
            if (plan.Count is < 1 or > 240) throw new ArgumentOutOfRangeException(nameof(plan.Count), "Choose between 1 and 240 installments.");
            if (plan.Frequency == "Custom" && plan.CustomIntervalDays is < 1 or > 365)
                throw new ArgumentOutOfRangeException(nameof(plan.CustomIntervalDays), "A custom interval must be between 1 and 365 days.");

            decimal total = decimal.Round(plan.CashPrice + plan.MarkupAmount + plan.ProcessingFee, 2, MidpointRounding.AwayFromZero);
            if (plan.DownPayment >= total)
                throw new ArgumentOutOfRangeException(nameof(plan.DownPayment), "Down payment must be less than the total contract value.");

            decimal financed = total - plan.DownPayment;
            decimal regular = decimal.Round(financed / plan.Count, 2, MidpointRounding.AwayFromZero);
            var schedule = new List<PlannedInstallment>(plan.Count);
            decimal remainder = financed;
            for (int index = 0; index < plan.Count; index++)
            {
                decimal amount = index == plan.Count - 1 ? remainder : Math.Min(regular, remainder);
                amount = decimal.Round(amount, 2, MidpointRounding.AwayFromZero);
                schedule.Add(new PlannedInstallment(index + 1, DueDate(plan, index), amount));
                remainder -= amount;
            }

            if (schedule.Sum(item => item.AmountDue) != financed)
                throw new InvalidOperationException("The schedule does not reconcile to the financed amount.");
            return schedule;
        }

        public static IReadOnlyList<InstallmentPaymentAllocation> Allocate(
            IReadOnlyList<InstallmentSchedule> schedules,
            decimal paymentAmount,
            out IReadOnlyDictionary<long, (decimal Principal, decimal Fee)> paymentBySchedule)
        {
            if (paymentAmount <= 0m) throw new ArgumentOutOfRangeException(nameof(paymentAmount));
            decimal remaining = decimal.Round(paymentAmount, 2, MidpointRounding.AwayFromZero);
            var allocations = new List<InstallmentPaymentAllocation>();
            var details = new Dictionary<long, (decimal Principal, decimal Fee)>();

            foreach (var item in schedules.OrderBy(s => s.DueDate).ThenBy(s => s.Sequence))
            {
                if (remaining <= 0m) break;
                decimal feeDue = item.LateFeeWaived ? 0m : Math.Max(0m, item.LateFee - item.LateFeePaid);
                decimal feePaid = Math.Min(remaining, feeDue);
                remaining -= feePaid;

                decimal principalDue = Math.Max(0m, item.AmountDue - item.AmountPaid);
                decimal principalPaid = Math.Min(remaining, principalDue);
                remaining -= principalPaid;

                decimal allocated = feePaid + principalPaid;
                if (allocated == 0m) continue;
                allocations.Add(new InstallmentPaymentAllocation { ScheduleId = item.Id, Amount = allocated });
                details[item.Id] = (principalPaid, feePaid);
            }

            if (remaining > 0m)
                throw new ArgumentOutOfRangeException(nameof(paymentAmount), "Payment exceeds the remaining contract balance.");
            paymentBySchedule = details;
            return allocations;
        }

        private static DateTime DueDate(InstallmentPlanInput plan, int index) => plan.Frequency switch
        {
            "Monthly" => plan.FirstDueDate.Date.AddMonths(index),
            "Weekly" => plan.FirstDueDate.Date.AddDays(index * 7),
            _ => plan.FirstDueDate.Date.AddDays(index * plan.CustomIntervalDays)
        };
    }
}
