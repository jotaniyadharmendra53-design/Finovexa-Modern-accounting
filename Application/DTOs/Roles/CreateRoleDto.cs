using System;
using System.Collections.Generic;
using System.Text;

namespace InvoiceSaaS.Application.DTOs.Roles
{
    public class CreateRoleDto
    {
        public string Name { get; set; } = default!;
        public string? Description { get; set; }
        public List<Guid> PermissionIds { get; set; } = new();
        public Guid? CompanyId { get; set; }

    }
}
