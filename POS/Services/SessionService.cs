using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using CommunityToolkit.Mvvm.Messaging;
using POS.Models;

namespace POS.Services
{
    public static class SessionService
    {
        private const int SaltSize = 16;
        private const int KeySize = 32;
        private const int Iterations = 10000;

        public static StaffUser? CurrentUser { get; private set; }
        public static bool IsAuthenticated => CurrentUser != null;
        public static bool IsOwner => string.Equals(CurrentUser?.Role, "Owner", StringComparison.OrdinalIgnoreCase);
        public static string CurrentUserName => CurrentUser?.DisplayName ?? "Counter Cashier";

        public static string HashPin(string pin)
        {
            byte[] salt = RandomNumberGenerator.GetBytes(SaltSize);
            byte[] hash = Rfc2898DeriveBytes.Pbkdf2(pin ?? string.Empty, salt, Iterations, HashAlgorithmName.SHA256, KeySize);
            return $"{Convert.ToBase64String(salt)}:{Convert.ToBase64String(hash)}";
        }

        public static bool VerifyPinHash(string pin, string storedHash)
        {
            if (string.IsNullOrWhiteSpace(storedHash) || !storedHash.Contains(':'))
                return false;

            var parts = storedHash.Split(':');
            if (parts.Length != 2) return false;

            try
            {
                byte[] salt = Convert.FromBase64String(parts[0]);
                byte[] expectedHash = Convert.FromBase64String(parts[1]);
                byte[] actualHash = Rfc2898DeriveBytes.Pbkdf2(pin ?? string.Empty, salt, Iterations, HashAlgorithmName.SHA256, expectedHash.Length);
                return CryptographicOperations.FixedTimeEquals(actualHash, expectedHash);
            }
            catch
            {
                return false;
            }
        }

        public static List<StaffUser> GetActiveUsers()
        {
            using var context = new AppDbContext();
            return context.StaffUsers
                .Where(u => u.IsActive)
                .OrderByDescending(u => u.Role == "Owner")
                .ThenBy(u => u.DisplayName)
                .ToList();
        }

        public static bool SignIn(int userId, string pin)
        {
            using var context = new AppDbContext();
            var user = context.StaffUsers.FirstOrDefault(u => u.Id == userId && u.IsActive);
            if (user == null) return false;
            if (!VerifyPinHash(pin, user.PinHash)) return false;

            CurrentUser = user;
            WeakReferenceMessenger.Default.Send(DataChangedMessage.SessionChanged);
            return true;
        }

        public static bool SignInWithPin(string pin)
        {
            using var context = new AppDbContext();
            var users = context.StaffUsers.Where(u => u.IsActive).ToList();
            var match = users.FirstOrDefault(u => VerifyPinHash(pin, u.PinHash));
            if (match == null) return false;

            CurrentUser = match;
            WeakReferenceMessenger.Default.Send(DataChangedMessage.SessionChanged);
            return true;
        }

        public static void SignInAsDefaultCashier()
        {
            using var context = new AppDbContext();
            var cashier = context.StaffUsers.FirstOrDefault(u => u.IsActive && u.Role == "Cashier")
                          ?? context.StaffUsers.FirstOrDefault(u => u.IsActive);
            if (cashier != null)
            {
                CurrentUser = cashier;
                WeakReferenceMessenger.Default.Send(DataChangedMessage.SessionChanged);
            }
        }

        public static bool VerifyAnyOwnerPin(string pin)
        {
            if (string.IsNullOrWhiteSpace(pin)) return false;
            using var context = new AppDbContext();
            var owners = context.StaffUsers.Where(u => u.IsActive && u.Role == "Owner").ToList();
            return owners.Any(o => VerifyPinHash(pin, o.PinHash));
        }

        public static void SignOut()
        {
            CurrentUser = null;
            WeakReferenceMessenger.Default.Send(DataChangedMessage.SessionChanged);
        }
    }
}

