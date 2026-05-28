using System;
using System.Collections.Generic;
using System.Text;

namespace InvoiceSaaS.Application.DTOs
{
    public class PnLReportDto
    {
        public decimal TotalRevenue { get; set; }  // Invoices paid + Sales
        public decimal TotalExpenses { get; set; }  // All expenses
        public decimal GrossProfit { get; set; }  // Revenue - Expenses
        public decimal NetProfit { get; set; }
        public string PeriodLabel { get; set; } = default!;
        public List<PnLLineDto> RevenueLines { get; set; } = new();
        public List<PnLLineDto> ExpenseLines { get; set; } = new();
    }
}
