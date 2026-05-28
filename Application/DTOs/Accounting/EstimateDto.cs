using System;
using System.Collections.Generic;
using System.Text;

namespace InvoiceSaaS.Application.DTOs
{
    public class EstimateDto
    {
        public Guid Id { get; set; }
        public string EstimateNumber { get; set; } = default!;
        public Guid ClientId { get; set; }
        public string ClientName { get; set; } = default!;
        public string? ClientEmail { get; set; }
        public int Status { get; set; }
        public string StatusName { get; set; } = default!;
        public DateTime IssueDate { get; set; }
        public DateTime ExpiryDate { get; set; }
        public decimal SubTotal { get; set; }
        public decimal TaxRate { get; set; }
        public decimal TaxAmount { get; set; }
        public decimal Discount { get; set; }
        public decimal Total { get; set; }
        public string? Notes { get; set; }
        public string? Terms { get; set; }
        public Guid? ConvertedInvoiceId { get; set; }
        public DateTime? SentAt { get; set; }
        public DateTime CreatedAt { get; set; }
        public string CompanyName { get; set; } = default!;
        public string CurrencyCode { get; set; } = "USD";
        public List<EstimateItemDto> Items { get; set; } = new();

        public bool IsExpired => ExpiryDate.Date < DateTime.Today;
    }
}
