using System;
using System.Collections.Generic;
using System.Text;

namespace InvoiceSaaS.Application.DTOs
{
    public class SaveProductDto
    {
        public Guid? Id { get; set; }
        public string Name { get; set; } = default!;
        public string? SKU { get; set; }
        public int Type { get; set; }
        public decimal SalePrice { get; set; }
        public decimal CostPrice { get; set; }
        public decimal TaxRate { get; set; }
        public string? Unit { get; set; }
        public string? Description { get; set; }
        public bool IsActive { get; set; } = true;
    }
}
