using InvoiceSaaS.Domain.Enums;
using System;
using System.Collections.Generic;
using System.Text;

namespace InvoiceSaaS.Application.DTOs.Invoices
{
    public class InvoiceDto
    {
        public Guid Id { get; set; }
        public string InvoiceNumber { get; set; } = default!;
        public Guid CompanyId { get; set; }
        public string CompanyName { get; set; } = default!;
        public Guid ClientId { get; set; }
        public string ClientName { get; set; } = default!;
        public string? ClientEmail { get; set; }
        public DateTime IssueDate { get; set; }
        public DateTime DueDate { get; set; }
        public InvoiceStatus Status { get; set; }
        public string StatusName { get; set; } = default!;
        public string StatusBadge { get; set; } = default!;  // CSS class
        public decimal SubTotal { get; set; }
        public decimal TaxRate { get; set; }
        public decimal TaxAmount { get; set; }
        public decimal Discount { get; set; }
        public decimal Total { get; set; }
        public decimal PaidAmount { get; set; }
        public decimal BalanceDue { get; set; }
        public string? Notes { get; set; }
        public string? Terms { get; set; }
        public DateTime? SentAt { get; set; }
        public DateTime? PaidAt { get; set; }
        public DateTime CreatedAt { get; set; }
        public string CurrencyCode { get; set; } = "USD";
        public decimal ExchangeRate { get; set; } = 1m;
        public decimal BaseAmount { get; set; }
        public string? CompanyLogo { get; set; }
        public List<InvoiceItemDto> Items { get; set; } = new();

        public string? LastEditRemark { get; set; }
        public List<InvoiceEditHistoryDto> EditHistory { get; set; } = new();

    }
}
