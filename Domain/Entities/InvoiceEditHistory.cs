using System;
using System.Collections.Generic;
using System.Text;

namespace InvoiceSaaS.Domain.Entities
{
    public class InvoiceEditHistory
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public Guid InvoiceId { get; set; }
        public Guid EditedBy { get; set; }
        public DateTime EditedAt { get; set; } = DateTime.UtcNow;
        public string Remark { get; set; } = default!;
        public byte FromStatus { get; set; }
    }
}
