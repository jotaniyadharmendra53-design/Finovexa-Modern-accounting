using System;
using System.Collections.Generic;
using System.Text;

namespace InvoiceSaaS.Application.DTOs.FiscalYear
{
    public class CloseFiscalYearDto
    {
        public Guid FiscalYearId { get; set; }
        public string? Notes { get; set; }
        public bool OpenNextYear { get; set; } = true;  // auto-open next FY on close
    }
}
