using InvoiceSaaS.Application.Common;
using InvoiceSaaS.Application.DTOs.Common;
using InvoiceSaaS.Application.DTOs.Permissions;
using InvoiceSaaS.Application.DTOs.Roles;
using InvoiceSaaS.Domain.Entities;
using System;
using System.Collections.Generic;
using System.Text;

namespace InvoiceSaaS.Application.Services.Interfaces
{
    public interface IRoleService
    {
        Task<ServiceResult<RoleDto>> GetByIdAsync(Guid id, CancellationToken ct = default);
        Task<ServiceResult<RoleDetailDto>> GetDetailAsync(Guid id, CancellationToken ct = default);
        Task<ServiceResult<IEnumerable<RoleDto>>> GetAllAsync(Guid? companyId = null, CancellationToken ct = default);
        Task<ServiceResult<RoleDto>> CreateAsync(CreateRoleDto dto, Guid createdBy, CancellationToken ct = default);
        Task<ServiceResult<RoleDto>> UpdateAsync(UpdateRoleDto dto, Guid updatedBy, CancellationToken ct = default);
        Task<ServiceResult> DeleteAsync(Guid id, Guid deletedBy, CancellationToken ct = default);
        Task<ServiceResult<IEnumerable<SelectItemDto>>> GetSelectListAsync(Guid? companyId = null, CancellationToken ct = default);
        Task<ServiceResult<IEnumerable<PermissionGroupDto>>> GetAllPermissionsGroupedAsync(CancellationToken ct = default);
        Task<ServiceResult<IEnumerable<PermissionGroupDto>>> GetPermissionsForRoleAsync(Guid roleId, CancellationToken ct = default);
    }

}
