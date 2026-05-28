using System;
using System.Collections.Generic;
using System.Text;

namespace InvoiceSaaS.Application.DTOs.FiscalYear
{
    public class UpdateFiscalYearDto
    {
        public Guid Id { get; set; }
        public string? Notes { get; set; }
    }
}
