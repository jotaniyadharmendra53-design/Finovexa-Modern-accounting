using System;
using System.Collections.Generic;
using System.Text;

namespace InvoiceSaaS.Domain.Entities
{
    // ═══════════════════════════════════════════════════════════
    //  InvoiceItem
    // ═══════════════════════════════════════════════════════════
    public class InvoiceItem
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public Guid InvoiceId { get; set; }
        public string Description { get; set; } = default!;
        public decimal Quantity { get; set; } = 1;
        public decimal UnitPrice { get; set; }
        public decimal Amount { get; set; }   // Quantity * UnitPrice
        public int SortOrder { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        // Navigation
        public Invoice Invoice { get; set; } = default!;
    }
}
