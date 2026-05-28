using System;
using System.Collections.Generic;
using System.Text;

namespace InvoiceSaaS.Domain.Interfaces
{
    public class InvoiceSummaryStats
    {
        public int TotalInvoices { get; set; }
        public int DraftCount { get; set; }
        public int SentCount { get; set; }
        public int PaidCount { get; set; }
        public int OverdueCount { get; set; }
        public decimal TotalRevenue { get; set; }
        public decimal PendingAmount { get; set; }
        public decimal OverdueAmount { get; set; }
        public decimal ThisMonthTotal { get; set; }
    }
}
