using System;
using System.Collections.Generic;
using System.Text;

namespace InvoiceSaaS.Application.DTOs.FiscalYear
{
    public class OpenFiscalYearDto
    {
        public int StartMonth { get; set; } = 4;  // 4 = April (India default)
        public int StartYear { get; set; } = DateTime.Today.Year;
        public string? Notes { get; set; }
    }
}
