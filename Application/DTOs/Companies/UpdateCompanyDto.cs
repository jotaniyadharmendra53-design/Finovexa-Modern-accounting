using System;
using System.Collections.Generic;
using System.Text;

namespace InvoiceSaaS.Application.DTOs.Companies
{
    public class UpdateCompanyDto
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
        public string? TaxNumber { get; set; }
        public string CurrencyCode { get; set; } = "USD";
        public string InvoiceTemplate { get; set; } = "classic";
    }
}
