using System;
using System.Collections.Generic;
using System.Text;

namespace InvoiceSaaS.Application.DTOs.FiscalYear
{
    public class FiscalYearCloseCheckDto
    {
        public bool CanClose { get; set; }
        public int UnpaidInvoiceCount { get; set; }
        public int UnpaidExpenseCount { get; set; }
        public int DraftInvoiceCount { get; set; }
        public string FiscalYearLabel { get; set; } = default!;
        public List<string> Warnings { get; set; } = new();
        public List<string> BlockingIssues { get; set; } = new();
    }
}
