using System;
using System.Collections.Generic;
using System.Text;

namespace InvoiceSaaS.Application.DTOs.Companies
{
    public class EditCompanyDto
    {
        public Guid Id { get; set; }
        public string Name { get; set; } = default!;
        public string? Email { get; set; }
        public string? Phone { get; set; }
        public string? Website { get; set; }
        public string? Address { get; set; }
        public string CurrencyCode { get; set; } = "INR";
        public string? AdminFullName { get; set; }  
        public string? AdminEmail { get; set; }
    }
}
