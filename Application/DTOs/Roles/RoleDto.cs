using System;
using System.Collections.Generic;
using System.Text;

namespace InvoiceSaaS.Application.DTOs.Roles
{
    public class RoleDto
    {
        public Guid Id { get; set; }
        public string Name { get; set; } = default!;
        public string? Description { get; set; }
        public bool IsSystem { get; set; }
        public bool IsActive { get; set; }
        public int PermissionCount { get; set; }
        public int UserCount { get; set; }
        public DateTime CreatedAt { get; set; }
    }
}
