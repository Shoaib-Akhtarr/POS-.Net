using System;
using System.ComponentModel.DataAnnotations.Schema;

namespace POS.Models
{
    public sealed class AuditLog
    {
        public int Id { get; set; }
        public string TableName { get; set; } = string.Empty;
        public int RecordId { get; set; }
        public string Action { get; set; } = string.Empty; // INSERT, UPDATE, DELETE, LOGIN, LOGOUT, etc.
        public string? OldValues { get; set; } // JSON snapshot before change
        public string? NewValues { get; set; } // JSON snapshot after change
        public string? CreatedBy { get; set; } // Staff user name
        public int? CreatedByUserId { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.Now;

        [NotMapped]
        public string DisplayAction => Action switch
        {
            "INSERT" => "Created",
            "UPDATE" => "Updated",
            "DELETE" => "Deleted",
            "LOGIN" => "Signed In",
            "LOGOUT" => "Signed Out",
            _ => Action
        };
    }
}