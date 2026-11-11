using System;

namespace POS.Models
{
    public class StaffUser
    {
        public int Id { get; set; }
        public string DisplayName { get; set; } = string.Empty;
        public string Role { get; set; } = "Cashier"; // Owner or Cashier
        public string PinHash { get; set; } = string.Empty;
        public bool IsActive { get; set; } = true;
        public DateTime CreatedAt { get; set; } = DateTime.Now;
    }
}

