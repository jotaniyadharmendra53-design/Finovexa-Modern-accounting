using System;
using System.Collections.Generic;
using System.Text;

namespace InvoiceSaaS.Application.DTOs.Companies
{
    public class CreateCompanyDto
    {
        // ── Company fields ────────────────────────────────────
        public string CompanyName { get; set; } = default!;//
        public string? CompanyEmail { get; set; }
        public string? CompanyPhone { get; set; }
        public string? CompanyWebsite { get; set; }
        public string? CompanyAddress { get; set; }
        public string CurrencyCode { get; set; } = "INR";
        public int FiscalStartMonth { get; set; } = 4;   // April default

        // ── Owner / Admin user fields ─────────────────────────
        public string AdminFullName { get; set; } = default!;
        public string AdminEmail { get; set; } = default!;//
        public string AdminPassword { get; set; } = default!;
        public string? AdminPhone { get; set; }

        // If true, sends welcome email to the admin with credentials
        public bool SendWelcomeEmail { get; set; } = true;
    }
}

