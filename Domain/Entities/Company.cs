using InvoiceSaaS.Domain.Common;
using InvoiceSaaS.Domain.Enums;
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices.JavaScript;
using System.Text;

namespace InvoiceSaaS.Domain.Entities
{
    // ═══════════════════════════════════════════════════════════
    //  Company
    // ═══════════════════════════════════════════════════════════
    public class Company : BaseEntity
    {
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
        public string InvoiceTemplate { get; set; } = "classic"; // classic|modern|minimal|elegant
        public Guid OwnerId { get; set; }
        public bool IsActive { get; set; } = true;

        public int FiscalYearStartMonth { get; set; } = 4;  // 4 = April (India default)
        // Indicates whether initial company setup (logo, currency, tax settings etc.) has been completed
        public bool IsSetupCompleted { get; set; } = false;

        public string? Timezone { get; set; }         // e.g. "Asia/Kolkata"
        public string? DateFormat { get; set; }         // e.g. "dd/MM/yyyy"

        // 0=GST, 1=VAT, 2=SalesTax
        public TaxType? TaxType { get; set; }
        // India GST sub-type: 0=CGST_SGST (intra-state), 1=IGST (inter-state)
        public GstType? GstType { get; set; }



        // Navigation
        public User Owner { get; set; } = default!;
        public ICollection<Client> Clients { get; set; } = new List<Client>();
        public ICollection<Invoice> Invoices { get; set; } = new List<Invoice>();
        public ICollection<UserCompany> UserCompanies { get; set; } = new List<UserCompany>();
    }
}
