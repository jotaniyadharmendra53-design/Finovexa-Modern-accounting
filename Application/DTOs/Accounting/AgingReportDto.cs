using System;
using System.Collections.Generic;
using System.Text;

namespace InvoiceSaaS.Application.DTOs.Accounting
{
    public class AgingReportDto
    {
        public string Name { get; set; } = default!;
        public decimal Current { get; set; }
        public decimal Days1_30 { get; set; }
        public decimal Days31_60 { get; set; }
        public decimal Over60 { get; set; }
        public decimal Total { get; set; }
    }
}
