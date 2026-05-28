using System;
using System.Collections.Generic;
using System.Text;

namespace InvoiceSaaS.Application.DTOs
{
    public class SaveSaleDto
    {
        public Guid? Id { get; set; }
        public Guid? ClientId { get; set; }
        public DateTime SaleDate { get; set; }
        public decimal TaxRate { get; set; }
        public decimal Discount { get; set; }
        public string? PaymentMethod { get; set; }
        public string? Notes { get; set; }
        public List<SaleItemDto> Items { get; set; } = new();
    }
}
