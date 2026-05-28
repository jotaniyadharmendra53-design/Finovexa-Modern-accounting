using InvoiceSaaS.Domain.Common;
using InvoiceSaaS.Domain.Enums;
using System;
using System.Collections.Generic;
using System.Text;

namespace InvoiceSaaS.Domain.Entities
{
    // ═══════════════════════════════════════════════════════════
    //  Invoice
    // ═══════════════════════════════════════════════════════════
    public class Invoice : BaseEntity
    {
        public string InvoiceNumber { get; set; } = default!;
        public Guid CompanyId { get; set; }
        public Guid ClientId { get; set; }
        public DateTime IssueDate { get; set; }
        public DateTime DueDate { get; set; }
        public InvoiceStatus Status { get; set; } = InvoiceStatus.Draft;
        public decimal SubTotal { get; set; }
        public decimal TaxRate { get; set; }
        public decimal TaxAmount { get; set; }
        public decimal Discount { get; set; }
        public decimal Total { get; set; }
        public decimal PaidAmount { get; set; }
        public string? Notes { get; set; }
        public string? Terms { get; set; }
        public DateTime? SentAt { get; set; }
        public DateTime? PaidAt { get; set; }

        public string CurrencyCode { get; set; } = "INR";
        public decimal ExchangeRate  { get; set; } = 1m;
        public decimal BaseAmount { get; set; }

        public string? LastEditRemark { get; set; }

        // Navigation
        public Company Company { get; set; } = default!;
        public Client Client { get; set; } = default!;
        public ICollection<InvoiceItem> InvoiceItems { get; set; } = new List<InvoiceItem>();

        // Computed helpers
        public decimal BalanceDue => Total - PaidAmount;
        public bool IsOverdue => Status == InvoiceStatus.Sent
                                  && DueDate < DateTime.UtcNow.Date;
    }
}
