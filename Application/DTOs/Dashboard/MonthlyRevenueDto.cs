using System;
using System.Collections.Generic;
using System.Text;

namespace InvoiceSaaS.Application.DTOs.Dashboard
{
    public class MonthlyRevenueDto
    {
        public int Month { get; set; }
        public int Year { get; set; }
        public string Label { get; set; } = default!;
        public decimal Amount { get; set; }
    }
}
