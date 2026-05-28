using System;
using System.Collections.Generic;
using System.Text;

namespace InvoiceSaaS.Application.DTOs
{
    public class CashFlowRowDto
    {
        public string Date { get; set; } = default!;
        public string Reference { get; set; } = default!;
        public string Type { get; set; } = default!;
        public decimal Inbound { get; set; }
        public decimal Outbound { get; set; }
        public decimal Balance { get; set; }
    }
}
