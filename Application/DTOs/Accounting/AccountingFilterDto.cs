using System;
using System.Collections.Generic;
using System.Text;

namespace InvoiceSaaS.Application.DTOs
{
    public class AccountingFilterDto
    {
        public int Year { get; set; } = DateTime.Today.Year;
        public int? Month { get; set; }
        public DateTime? DateFrom { get; set; }
        public DateTime? DateTo { get; set; }

        // FY picker — when set, overrides Year/Month with exact FY date range
        public string? FyLabel { get; set; }   // passed back for display
    }

}
