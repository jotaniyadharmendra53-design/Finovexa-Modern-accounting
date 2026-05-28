using InvoiceSaaS.Domain.Enums;
using System;
using System.Collections.Generic;
using System.Text;

namespace InvoiceSaaS.Application.DTOs.Companies
{
    public class SetupCompanyDto
    {
        public Guid CompanyId { get; set; }

        // Company fields
        public string Name { get; set; } = default!;
        public string? Phone { get; set; }
        public string? Address { get; set; }
        public string? TaxNumber { get; set; }
        public string CurrencyCode { get; set; } = "INR";
        public string? Timezone { get; set; }
        public string? DateFormat { get; set; }
        public TaxType TaxType { get; set; } = TaxType.GST;
        public GstType? GstType { get; set; }

        // New password (mandatory on first login)
        public string NewPassword { get; set; } = default!;
        public string ConfirmNewPassword { get; set; } = default!;
    }
}
