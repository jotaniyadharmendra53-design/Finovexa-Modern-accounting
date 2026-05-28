using InvoiceSaaS.Domain.Common;
using System;
using System.Collections.Generic;
using System.Text;
using InvoiceSaaS.Domain.Enums;

namespace InvoiceSaaS.Domain.Entities
{
    // ═══════════════════════════════════════════════════════════
    //  Role
    // ═══════════════════════════════════════════════════════════
    public class Role : BaseEntity
    {
        public string Name { get; set; } = default!;
        public string? Description { get; set; }
        public bool IsSystem { get; set; } = false;
        public bool IsActive { get; set; } = true;


        // NULL  = global role (Super Admin role only)
        // Guid  = company-scoped role (all company-created roles)
        public Guid? CompanyId { get; set; }



        // Navigation
        public Company? Company { get; set; }
        public ICollection<User> Users { get; set; } = new List<User>();
        public ICollection<RolePermission> RolePermissions { get; set; } = new List<RolePermission>();


        // Helper — true for the platform Super Admin role
        public bool IsGlobal => CompanyId is null;

    }
}
