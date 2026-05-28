using System;
using System.Collections.Generic;
using System.Text;

namespace InvoiceSaaS.Application.DTOs.Dashboard
{
    public class CurrencyRevenueDto
    {
        public string CurrencyCode { get; set; } = default!;
        public decimal Revenue { get; set; }   // sum of paid invoices in this currency
        public decimal Pending { get; set; }   // sum of sent/overdue in this currency
        public decimal BaseRevenue { get; set; }   // Revenue converted to base currency
        public int InvoiceCount { get; set; }
        public string CurrencySymbol { get; set; } = default!;
    }
}
