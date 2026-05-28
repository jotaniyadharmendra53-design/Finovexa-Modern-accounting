using System;
using System.Collections.Generic;
using System.Text;

namespace InvoiceSaaS.Application.DTOs.Roles
{
    public class UpdateRoleDto : CreateRoleDto
    {
        public Guid Id { get; set; }
        public Guid CompanyId { get; set; }
    }
}
