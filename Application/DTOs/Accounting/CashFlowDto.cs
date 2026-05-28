using System;
using System.Collections.Generic;
using System.Text;

namespace InvoiceSaaS.Application.DTOs
{
    public class CashFlowDto
    {
        public decimal OpeningBalance { get; set; }
        public decimal TotalInbound { get; set; }
        public decimal TotalOutbound { get; set; }
        public decimal ClosingBalance { get; set; }
        public List<CashFlowRowDto> Rows { get; set; } = new();
    }
}
