using System;

namespace POS.Models
{
    public class Customer
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Phone { get; set; } = string.Empty;
        public string Address { get; set; } = string.Empty;
        public string FatherOrHusbandName { get; set; } = string.Empty;
        public string WorkAddress { get; set; } = string.Empty;
        public string Cnic { get; set; } = string.Empty;
        public string Ntn { get; set; } = string.Empty;
        public decimal TotalDues { get; set; }
        public decimal CreditLimit { get; set; }
        public decimal TotalDiscount { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.Now;
        public DateTime UpdatedAt { get; set; } = DateTime.Now;
    }
}
