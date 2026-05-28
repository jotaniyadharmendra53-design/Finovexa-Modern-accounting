using System;
using System.Collections.Generic;
using System.Text;

namespace InvoiceSaaS.Application.DTOs
{
    public class CreatePaymentDto
    {
        public int Direction { get; set; }
        public decimal Amount { get; set; }
        public DateTime PaymentDate { get; set; }
        public string? Method { get; set; }
        public string? Reference { get; set; }
        public Guid? InvoiceId { get; set; }
        public Guid? ExpenseId { get; set; }
        public Guid? VendorId { get; set; }
        public Guid? ClientId { get; set; }
        public string? Notes { get; set; }
    }
}
