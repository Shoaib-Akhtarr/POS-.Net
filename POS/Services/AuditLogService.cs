using System;
using System.Text.Json;
using POS.Models;

namespace POS.Services
{
    public static class AuditLogService
    {
        public static void Write(string tableName, int recordId, string action, object? oldValues = null, object? newValues = null, string? createdBy = null, int? createdByUserId = null)
        {
            try
            {
                using var context = new AppDbContext();
                var entry = new AuditLog
                {
                    TableName = tableName,
                    RecordId = recordId,
                    Action = action,
                    OldValues = oldValues == null ? null : JsonSerializer.Serialize(oldValues),
                    NewValues = newValues == null ? null : JsonSerializer.Serialize(newValues),
                    CreatedBy = createdBy ?? SessionService.CurrentUserName,
                    CreatedByUserId = createdByUserId ?? SessionService.CurrentUser?.Id,
                    CreatedAt = DateTime.Now
                };
                context.AuditLogs.Add(entry);
                context.SaveChanges();
            }
            catch
            {
                // Audit logging must never break the main operation
            }
        }

        public static void Write(string tableName, int recordId, string action, string? oldValuesJson, string? newValuesJson, string? createdBy = null, int? createdByUserId = null)
        {
            try
            {
                using var context = new AppDbContext();
                var entry = new AuditLog
                {
                    TableName = tableName,
                    RecordId = recordId,
                    Action = action,
                    OldValues = oldValuesJson,
                    NewValues = newValuesJson,
                    CreatedBy = createdBy ?? SessionService.CurrentUserName,
                    CreatedByUserId = createdByUserId ?? SessionService.CurrentUser?.Id,
                    CreatedAt = DateTime.Now
                };
                context.AuditLogs.Add(entry);
                context.SaveChanges();
            }
            catch
            {
                // Audit logging must never break the main operation
            }
        }
    }
}