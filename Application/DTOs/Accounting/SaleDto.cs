using System;
using System.Collections.Generic;
using System.Text;

namespace InvoiceSaaS.Application.DTOs
{
    public class SaleDto
    {
        public Guid Id { get; set; }
        public string SaleNumber { get; set; } = default!;
        public Guid? ClientId { get; set; }
        public string? ClientName { get; set; }
        public DateTime SaleDate { get; set; }
        public int Status { get; set; }
        public string StatusName { get; set; } = default!;
        public decimal SubTotal { get; set; }
        public decimal TaxRate { get; set; }
        public decimal TaxAmount { get; set; }
        public decimal Discount { get; set; }
        public decimal Total { get; set; }
        public string? PaymentMethod { get; set; }
        public string? Notes { get; set; }
        public string CurrencyCode { get; set; } = "USD";
        public DateTime CreatedAt { get; set; }
        public List<SaleItemDto> Items { get; set; } = new();
    }
}
