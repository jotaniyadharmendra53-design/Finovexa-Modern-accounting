using InvoiceSaaS.Domain.Enums;
using System;
using System.Collections.Generic;
using System.Text;

namespace InvoiceSaaS.Application.DTOs.Invoices
{
    public class InvoiceListItemDto
    {
        public Guid Id { get; set; }
        public string InvoiceNumber { get; set; } = default!;

        public Guid ClientId { get; set; }
        public string ClientName { get; set; } = default!;
        public DateTime IssueDate { get; set; }
        public DateTime DueDate { get; set; }
        public InvoiceStatus Status { get; set; }
        public string StatusName { get; set; } = default!;
        public string StatusBadge { get; set; } = default!;
        public decimal Total { get; set; }
        public decimal BalanceDue { get; set; }
        public string CurrencyCode { get; set; } = "USD";
        public decimal ExchangeRate { get; set; } = 1m;
        public decimal BaseAmount { get; set; }
        public bool IsOverdue { get; set; }
        public DateTime CreatedAt { get; set; }

        public Guid Cli { get; set; }
    }
}
