using InvoiceSaaS.Domain.Common;
using System;
using System.Collections.Generic;
using System.Text;

namespace InvoiceSaaS.Domain.Entities
{
    public class Expense : BaseEntity
    {
        public Guid CompanyId { get; set; }
        public Guid? VendorId { get; set; }
        public string ExpenseNumber { get; set; } = default!;
        public string Category { get; set; } = default!;
        public string? Description { get; set; }
        public decimal Amount { get; set; }
        public decimal TaxRate { get; set; }
        public decimal TaxAmount { get; set; }
        public decimal Total { get; set; }
        public DateTime ExpenseDate { get; set; }   
        public string? PaymentMethod { get; set; }
        public ExpenseStatus Status { get; set; } = ExpenseStatus.Unpaid;
        public string? ReceiptUrl { get; set; }
        public string? Notes { get; set; }
        public bool IsRecurring { get; set; }
        public string? RecurrencePeriod { get; set; }
        public DateTime? NextDueDate { get; set; }

        // Navigation
        public Company Company { get; set; } = default!;
        public Vendor? Vendor { get; set; }
    }
}
