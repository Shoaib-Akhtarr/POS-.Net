using System;
using System.Linq;
using Microsoft.EntityFrameworkCore;
using POS.Models;

namespace POS.Services
{
    public static class KhataService
    {
        public static KhataAccount GetCustomerAccount(AppDbContext context, Customer customer)
        {
            var account = context.KhataAccounts.FirstOrDefault(a => a.CustomerId == customer.Id);
            if (account != null) return account;

            account = new KhataAccount
            {
                CustomerId = customer.Id,
                PartyType = "Customer",
                Name = customer.Name,
                Phone = customer.Phone,
                Address = customer.Address,
                CreditLimit = customer.CreditLimit,
                CreatedBy = SessionService.CurrentUserName,
                CreatedAt = customer.CreatedAt,
                UpdatedAt = DateTime.Now
            };
            context.KhataAccounts.Add(account);
            context.SaveChanges();
            if (customer.TotalDues != 0m)
                Post(context, account, "Opening balance", "Opening", Math.Max(0m, customer.TotalDues), Math.Max(0m, -customer.TotalDues), "OpeningBalance", customer.Id.ToString(), customer.CreatedAt);
            return account;
        }

        public static KhataAccount GetOrCreatePartyAccount(AppDbContext context, string partyType, string name, string phone = "", string address = "")
        {
            if (string.IsNullOrWhiteSpace(name))
                throw new ArgumentException("A party name is required.", nameof(name));
            if (partyType is not ("Supplier" or "Recovery Agent"))
                throw new ArgumentOutOfRangeException(nameof(partyType));

            string cleanName = name.Trim();
            var account = context.KhataAccounts.FirstOrDefault(a => a.PartyType == partyType && a.Name.ToLower() == cleanName.ToLower());
            if (account != null) return account;

            account = new KhataAccount
            {
                PartyType = partyType,
                Name = cleanName,
                Phone = phone.Trim(),
                Address = address.Trim(),
                CreatedBy = SessionService.CurrentUserName,
                CreatedAt = DateTime.Now,
                UpdatedAt = DateTime.Now
            };
            context.KhataAccounts.Add(account);
            context.SaveChanges();
            return account;
        }

        public static KhataEntry Post(
            AppDbContext context,
            KhataAccount account,
            string description,
            string paymentMode,
            decimal debit,
            decimal credit,
            string sourceType,
            string sourceKey,
            DateTime? createdAt = null,
            long? reversesEntryId = null)
        {
            debit = decimal.Round(debit, 2, MidpointRounding.AwayFromZero);
            credit = decimal.Round(credit, 2, MidpointRounding.AwayFromZero);
            if (debit < 0m || credit < 0m || (debit > 0m && credit > 0m) || (debit == 0m && credit == 0m))
                throw new ArgumentOutOfRangeException(nameof(debit), "A ledger posting must have exactly one positive debit or credit.");
            if (string.IsNullOrWhiteSpace(sourceType) || string.IsNullOrWhiteSpace(sourceKey))
                throw new ArgumentException("Every ledger posting requires an idempotency source key.");

            var entry = new KhataEntry
            {
                AccountId = account.Id,
                VoucherNumber = $"KH-{DateTime.Now:yyMMddHHmmss}-{Guid.NewGuid():N}"[..21].ToUpperInvariant(),
                Description = description.Trim(),
                PaymentMode = paymentMode.Trim(),
                Debit = debit,
                Credit = credit,
                SourceType = sourceType.Trim(),
                SourceKey = sourceKey.Trim(),
                ReversesEntryId = reversesEntryId,
                CreatedBy = SessionService.CurrentUserName,
                CreatedAt = createdAt ?? DateTime.Now
            };
            context.KhataEntries.Add(entry);
            return entry;
        }

        public static decimal GetBalance(AppDbContext context, int accountId)
        {
            return context.KhataEntries.AsNoTracking()
                .Where(entry => entry.AccountId == accountId)
                .ToList()
                .Sum(entry => entry.Debit - entry.Credit);
        }
    }
}
