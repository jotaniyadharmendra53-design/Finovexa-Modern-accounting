using System;
using System.Collections.Generic;
using System.Text;

namespace InvoiceSaaS.Application.DTOs
{
    public class ExpenseDto
    {
        public Guid Id { get; set; }
        public string ExpenseNumber { get; set; } = default!;
        public string? VendorName { get; set; }
        public Guid? VendorId { get; set; }
        public string Category { get; set; } = default!;
        public string? Description { get; set; }
        public decimal Amount { get; set; }
        public decimal TaxRate { get; set; }
        public decimal TaxAmount { get; set; }
        public decimal Total { get; set; }
        public DateTime ExpenseDate { get; set; }
        public string? PaymentMethod { get; set; }
        public int Status { get; set; }
        public string StatusName { get; set; } = default!;
        public string? ReceiptUrl { get; set; }
        public string? Notes { get; set; }
        public bool IsRecurring { get; set; }
        public DateTime CreatedAt { get; set; }
    }
}
