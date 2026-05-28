using System;
using System.Collections.Generic;
using System.Text;

namespace InvoiceSaaS.Domain.Entities
{
    // ═══════════════════════════════════════════════════════════
    //  EmailLog
    // ═══════════════════════════════════════════════════════════
    public class EmailLog
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public string ToEmail { get; set; } = default!;
        public string? ToName { get; set; }
        public string Subject { get; set; } = default!;
        public string Body { get; set; } = default!;
        public bool IsHtml { get; set; } = true;
        public bool IsSuccess { get; set; } = false;
        public string? ErrorMessage { get; set; }
        public DateTime? SentAt { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public Guid? RelatedId { get; set; }
        public string? EmailType { get; set; }
    }
}
