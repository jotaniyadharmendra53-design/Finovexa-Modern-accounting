using System;
using System.Collections.Generic;
using System.Text;

namespace InvoiceSaaS.Application.DTOs
{
    public class SaveExpenseDto
    {
        public Guid? Id { get; set; }
        public Guid? VendorId { get; set; }
        public string Category { get; set; } = default!;
        public string? Description { get; set; }
        public decimal Amount { get; set; }
        public decimal TaxRate { get; set; }
        public DateTime ExpenseDate { get; set; }
        public string? PaymentMethod { get; set; }
        public int Status { get; set; }
        public string? Notes { get; set; }
        public bool IsRecurring { get; set; }
        public string? RecurrencePeriod { get; set; }
    }
}
