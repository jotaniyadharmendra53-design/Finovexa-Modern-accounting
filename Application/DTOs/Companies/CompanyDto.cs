using InvoiceSaaS.Domain.Enums;
using System;
using System.Collections.Generic;
using System.Text;

namespace InvoiceSaaS.Application.DTOs.Companies
{
    public class CompanyDto
    {
        public Guid Id { get; set; }
        public string Name { get; set; } = default!;
        public string? Email { get; set; }
        public string? Phone { get; set; }
        public string? Website { get; set; }
        public string? Address { get; set; }
        public string? City { get; set; }
        public string? State { get; set; }
        public string? Country { get; set; }
        public string? PostalCode { get; set; }
        public string? Logo { get; set; }
        public string? TaxNumber { get; set; }
        public string CurrencyCode { get; set; } = "USD";
        public bool IsActive { get; set; }

        public string InvoiceTemplate { get; set; } = "classic";


        public bool IsSetupCompleted { get; set; }
        public string? Timezone { get; set; }
        public string? DateFormat { get; set; }
        public TaxType? TaxType { get; set; }
        public GstType? GstType { get; set; }

        public string? AdminEmail { get; set; }

        public string? AdminFullName { get; set; }

    }
}
