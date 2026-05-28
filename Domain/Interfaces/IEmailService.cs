using System;
using System.Collections.Generic;
using System.Text;

namespace InvoiceSaaS.Domain.Interfaces
{
    // ═══════════════════════════════════════════════════════════
    //  Email Service Interface
    // ═══════════════════════════════════════════════════════════
    public interface IEmailService
    {
        Task SendAsync(EmailMessage message, CancellationToken ct = default);
        Task QueueAsync(EmailMessage message, CancellationToken ct = default);
    }

    public class EmailMessage
    {
        public string ToEmail { get; set; } = default!;
        public string? ToName { get; set; }
        public string Subject { get; set; } = default!;
        public string Body { get; set; } = default!;
        public bool IsHtml { get; set; } = true;
        public Guid? RelatedId { get; set; }
        public string? EmailType { get; set; }
    }
}
