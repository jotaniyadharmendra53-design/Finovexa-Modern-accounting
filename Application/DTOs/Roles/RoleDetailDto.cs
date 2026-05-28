using InvoiceSaaS.Application.DTOs.Permissions;
using System;
using System.Collections.Generic;
using System.Text;

namespace InvoiceSaaS.Application.DTOs.Roles
{
    public class RoleDetailDto : RoleDto
    {
        public List<PermissionGroupDto> PermissionGroups { get; set; } = new();
        public List<Guid> SelectedPermissionIds { get; set; } = new();
    }

}
