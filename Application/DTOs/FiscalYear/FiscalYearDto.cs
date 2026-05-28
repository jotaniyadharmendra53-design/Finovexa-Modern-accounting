using System;
using System.Collections.Generic;
using System.Text;

namespace InvoiceSaaS.Application.DTOs.FiscalYear
{
    public class FiscalYearDto
    {
        public Guid Id { get; set; }
        public string Label { get; set; } = default!;
        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }
        public int Status { get; set; }   // 0=Open, 1=Locked, 2=Future
        public string StatusName { get; set; } = default!;
        public bool IsDefault { get; set; }
        public string PeriodDisplay { get; set; } = default!;
        public string? Notes { get; set; }
        public DateTime? ClosedAt { get; set; }
        public DateTime CreatedAt { get; set; }

        // Computed for UI
        public bool IsOpen => Status == 0;
        public bool IsLocked => Status == 1;
        public bool IsFuture => Status == 2;
    }
}
