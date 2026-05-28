using System;
using System.Collections.Generic;
using System.Text;

namespace InvoiceSaaS.Application.DTOs.Invoices
{
    public class InvoiceEditHistoryDto
    {
        public Guid Id { get; set; }
        public DateTime EditedAt { get; set; }
        public string Remark { get; set; } = default!;
        public string FromStatus { get; set; } = default!;
    }
}
