using System;
using System.Collections.Generic;
using System.Text;

namespace InvoiceSaaS.Application.DTOs
{
    public class PnLLineDto
    {
        public string Label { get; set; } = default!;
        public decimal Amount { get; set; }
    }
}
