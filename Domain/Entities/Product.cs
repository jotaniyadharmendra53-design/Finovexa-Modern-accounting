using InvoiceSaaS.Domain.Common;
using System;
using System.Collections.Generic;
using System.Text;

namespace InvoiceSaaS.Domain.Entities
{
    public class Product : BaseEntity
    {
        public Guid CompanyId { get; set; }
        public string Name { get; set; } = default!;
        public string? SKU { get; set; }
        public ProductType Type { get; set; } = ProductType.Service;
        public decimal SalePrice { get; set; }
        public decimal CostPrice { get; set; }
        public decimal TaxRate { get; set; }
        public string? Unit { get; set; }
        public string? Description { get; set; }
        public bool IsActive { get; set; } = true;

        // Navigation
        public Company Company { get; set; } = default!;
    }
}
