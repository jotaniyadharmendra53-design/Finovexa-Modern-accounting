using System;
using System.Collections.Generic;
using System.Text;

namespace InvoiceSaaS.Application.DTOs.Permissions
{
    public class PermissionGroupDto
    {
        public string Module { get; set; } = default!;
        public List<PermissionDto> Permissions { get; set; } = new();
        public bool AllChecked => Permissions.All(p => p.IsChecked);
        public int CheckedCount => Permissions.Count(p => p.IsChecked);
    }
}
