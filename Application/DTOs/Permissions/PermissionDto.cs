using System;
using System.Collections.Generic;
using System.Text;

namespace InvoiceSaaS.Application.DTOs.Permissions
{
    public class PermissionDto
    {
        public Guid Id { get; set; }
        public string Name { get; set; } = default!;
        public string Code { get; set; } = default!;
        public string Module { get; set; } = default!;
        public string? Description { get; set; }
        public bool IsChecked { get; set; }  // used in Role edit UI
    }
}
