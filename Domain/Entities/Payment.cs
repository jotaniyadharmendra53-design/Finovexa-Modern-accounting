using InvoiceSaaS.Domain.Common;
using System;
using System.Collections.Generic;
using System.Text;

namespace InvoiceSaaS.Domain.Entities
{
    public class Payment : BaseEntity
    {
        public Guid CompanyId { get; set; }
        public string PaymentNumber { get; set; } = default!;
        public PaymentDirection Direction { get; set; }
        public decimal Amount { get; set; }
        public DateTime PaymentDate { get; set; }  
        public string? Method { get; set; }
        public string? Reference { get; set; }
        public Guid? InvoiceId { get; set; }
        public Guid? ExpenseId { get; set; }
        public Guid? VendorId { get; set; }
        public Guid? ClientId { get; set; }
        public string? Notes { get; set; }

        // Navigation
        public Company Company { get; set; } = default!;
        public Invoice? Invoice { get; set; }
        public Expense? Expense { get; set; }
        public Vendor? Vendor { get; set; }
        public Client? Client { get; set; }
    }
}
