using InvoiceSaaS.Domain.Enums;
using System;
using System.Collections.Generic;
using System.Text;

namespace InvoiceSaaS.Domain.Interfaces
{
    // ═══════════════════════════════════════════════════════════
    //  Supporting DTOs used by repositories
    // ═══════════════════════════════════════════════════════════
    public class InvoiceFilterDto
    {
        public InvoiceStatus? Status { get; set; }
        public Guid? ClientId { get; set; }
        public DateTime? DateFrom { get; set; }
        public DateTime? DateTo { get; set; }
        public string? Search { get; set; }
        public string SortBy { get; set; } = "CreatedAt";
        public bool SortDesc { get; set; } = true;
        public int Page { get; set; } = 1;
        public int PageSize { get; set; } = 10;
    }


}
