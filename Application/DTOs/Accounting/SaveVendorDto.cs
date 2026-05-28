using System;
using System.Collections.Generic;
using System.Text;

namespace InvoiceSaaS.Application.DTOs
{
    public class SaveVendorDto
    {
        public Guid? Id { get; set; }
        public string Name { get; set; } = default!;
        public string? Email { get; set; }
        public string? Phone { get; set; }
        public string? Address { get; set; }
        public string? City { get; set; }
        public string? State { get; set; }
        public string? Country { get; set; }
        public string? PostalCode { get; set; }
        public string? ContactPerson { get; set; }
        public string? TaxNumber { get; set; }
        public string? PaymentTerms { get; set; }
        public string? Notes { get; set; }
        public bool IsActive { get; set; } = true;
    }
}
