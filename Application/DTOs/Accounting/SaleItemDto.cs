using System;
using System.Collections.Generic;
using System.Text;

namespace InvoiceSaaS.Application.DTOs
{
    public class SaleItemDto
    {
        public Guid? Id { get; set; }
        public Guid? ProductId { get; set; }
        public string Description { get; set; } = default!;
        public decimal Quantity { get; set; } = 1;
        public decimal UnitPrice { get; set; }
        public decimal TaxRate { get; set; }
        public decimal Amount => Math.Round(Quantity * UnitPrice, 2);
        public int SortOrder { get; set; }
    }
}
