using System;
using System.Collections.Generic;
using System.Text;

namespace InvoiceSaaS.Domain.Entities
{
    public class CurrencyRevenueRow
    {
        public string CurrencyCode { get; set; } = default!;
        public decimal Revenue { get; set; }   // paid invoices
        public decimal Pending { get; set; }   // sent + overdue
        public decimal BaseRevenue { get; set; }   // sum of BaseAmount (paid)
        public int InvoiceCount { get; set; }
    }
}
