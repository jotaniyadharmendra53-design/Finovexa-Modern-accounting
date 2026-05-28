using System;
using System.Collections.Generic;
using System.Text;

namespace InvoiceSaaS.Domain.Entities
{
    // ═══════════════════════════════════════════════════════════
    //  AuditLog
    // ═══════════════════════════════════════════════════════════
    public class AuditLog
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public Guid? UserId { get; set; }
        public string Action { get; set; } = default!;
        public string? TableName { get; set; }
        public Guid? RecordId { get; set; }
        public string? OldValues { get; set; }   // JSON
        public string? NewValues { get; set; }   // JSON
        public string? IpAddress { get; set; }
        public string? UserAgent { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}
