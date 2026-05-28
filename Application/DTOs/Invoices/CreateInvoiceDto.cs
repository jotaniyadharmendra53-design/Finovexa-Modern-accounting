using System;
using System.Collections.Generic;
using System.Text;

namespace InvoiceSaaS.Application.DTOs.Invoices
{
    public class CreateInvoiceDto
    {
        public Guid ClientId { get; set; }
        public DateTime IssueDate { get; set; } = DateTime.Today;
        public DateTime DueDate { get; set; } = DateTime.Today.AddDays(30);
        public decimal TaxRate { get; set; } = 0;
        public decimal Discount { get; set; } = 0;
        public string CurrencyCode { get; set; } = "INR";
        public decimal ExchangeRate { get; set; } = 1m;
        public string? Notes { get; set; }
        public string? Terms { get; set; }
        public List<InvoiceItemDto> Items { get; set; } = new();
        public bool SendNow { get; set; } = false;
    }
}
