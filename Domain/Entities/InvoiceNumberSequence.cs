using System;
using System.Collections.Generic;
using System.Text;

namespace InvoiceSaaS.Domain.Entities
{
    // ═══════════════════════════════════════════════════════════
    //  InvoiceNumberSequence
    // ═══════════════════════════════════════════════════════════
    public class InvoiceNumberSequence
    {
        public Guid CompanyId { get; set; }
        public int Year { get; set; }
        public int LastSequence { get; set; }
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

        // Navigation
        public Company Company { get; set; } = default!;
    }
}
