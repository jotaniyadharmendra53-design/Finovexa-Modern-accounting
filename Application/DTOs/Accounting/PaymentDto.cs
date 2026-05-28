using System;
using System.Collections.Generic;
using System.Text;

namespace InvoiceSaaS.Application.DTOs
{
    public class PaymentDto
    {
        public Guid Id { get; set; }
        public string PaymentNumber { get; set; } = default!;
        public int Direction { get; set; }
        public string DirectionName { get; set; } = default!;
        public decimal Amount { get; set; }
        public DateTime PaymentDate { get; set; }
        public string? Method { get; set; }
        public string? Reference { get; set; }
        public string? InvoiceNumber { get; set; }
        public string? ExpenseNumber { get; set; }
        public string? VendorName { get; set; }
        public string? ClientName { get; set; }
        public string? Notes { get; set; }
        public DateTime CreatedAt { get; set; }
    }
}
