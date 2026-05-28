using InvoiceSaaS.Application.Common;
using InvoiceSaaS.Application.DTOs.Common;
using InvoiceSaaS.Application.DTOs.Permissions;
using InvoiceSaaS.Application.DTOs.Roles;
using InvoiceSaaS.Application.Services.Interfaces;
using InvoiceSaaS.Domain.Entities;
using InvoiceSaaS.Domain.Interfaces;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Text;

namespace InvoiceSaaS.Application.Services.Implementations
{
    // ═══════════════════════════════════════════════════════════
    //  RoleService
    // ═══════════════════════════════════════════════════════════
    public class RoleService : IRoleService
    {
        private readonly IRoleRepository _roleRepo;
        private readonly IPermissionRepository _permRepo;
        private readonly ILogger<RoleService> _logger;

        public RoleService(IRoleRepository roleRepo, IPermissionRepository permRepo, ILogger<RoleService> logger)
        {
            _roleRepo = roleRepo;
            _permRepo = permRepo;
            _logger = logger;
        }

        public async Task<ServiceResult<RoleDto>> GetByIdAsync(Guid id, CancellationToken ct = default)
        {
            var role = await _roleRepo.GetByIdAsync(id, ct);
            if (role is null) return ServiceResult<RoleDto>.Failure("Role not found.");
            return ServiceResult<RoleDto>.Success(MapToDto(role));
        }

        public async Task<ServiceResult<RoleDetailDto>> GetDetailAsync(Guid id, CancellationToken ct = default)
        {
            var role = await _roleRepo.GetWithPermissionsAsync(id, ct);
            if (role is null) return ServiceResult<RoleDetailDto>.Failure("Role not found.");

            var allPermissions = await _permRepo.GetAllAsync(ct);
            var rolePermIds = role.RolePermissions.Select(rp => rp.PermissionId).ToHashSet();

            var groups = allPermissions
                .Where(p => !p.IsDeleted)
                .GroupBy(p => p.Module)
                .OrderBy(g => g.Key)
                .Select(g => new PermissionGroupDto
                {
                    Module = g.Key,
                    Permissions = g.Select(p => new PermissionDto
                    {
                        Id = p.Id,
                        Name = p.Name,
                        Code = p.Code,
                        Module = p.Module,
                        Description = p.Description,
                        IsChecked = rolePermIds.Contains(p.Id)
                    }).ToList()
                }).ToList();

            return ServiceResult<RoleDetailDto>.Success(new RoleDetailDto
            {
                Id = role.Id,
                Name = role.Name,
                Description = role.Description,
                IsSystem = role.IsSystem,
                IsActive = role.IsActive,
                PermissionCount = rolePermIds.Count,
                PermissionGroups = groups,
                SelectedPermissionIds = rolePermIds.ToList(),
                CreatedAt = role.CreatedAt
            });
        }

        //public async Task<ServiceResult<IEnumerable<RoleDto>>> GetAllAsync(CancellationToken ct = default)
        //{
        //    var roles = await _roleRepo.GetAllWithPermissionsAsync(ct);
        //    return ServiceResult<IEnumerable<RoleDto>>.Success(roles.Select(MapToDto));
        //}

        // ── GetAllAsync — company-scoped ──────────────────────────
        // Pass the caller's CompanyId. SuperAdmin passes null
        // (gets all roles); company users get their company's roles
        // plus global system roles.
        public async Task<ServiceResult<IEnumerable<RoleDto>>> GetAllAsync(
            Guid? companyId = null, CancellationToken ct = default)
        {
            var roles = await _roleRepo.GetAllWithPermissionsAsync(companyId, ct);

            // Company users must NOT see other companies' roles.
            // The repo query already scopes by CompanyId, but we
            // double-filter here as a safety net.
            //if (companyId.HasValue)
            //    //roles = roles.Where(r => r.CompanyId == companyId || r.CompanyId is null);
            //    roles = roles.Where(r => r.CompanyId == companyId && !r.IsSystem);

            if (companyId.HasValue)
            {
                roles = roles
                    // Only this company's roles — not other companies, not global
                    .Where(r => r.CompanyId == companyId)
                    // Hide system/provisioned roles (the auto-created Admin role)
                    // Company users only see roles THEY created
                    .Where(r => !r.IsSystem);
            }


            return ServiceResult<IEnumerable<RoleDto>>.Success(roles.Select(MapToDto));
        }

        //// Backward-compat overload (no companyId) — used by SuperAdmin paths
        //public async Task<ServiceResult<IEnumerable<RoleDto>>> GetAllAsync(
        //    CancellationToken ct = default)
        //    => await GetAllAsync(null, ct);


        //public async Task<ServiceResult<RoleDto>> CreateAsync(CreateRoleDto dto, Guid createdBy, CancellationToken ct = default)
        //{
        //    try
        //    {
        //        var role = new Role
        //        {
        //            Name = dto.Name.Trim(),
        //            Description = dto.Description?.Trim(),
        //            IsSystem = false,
        //            IsActive = true,
        //            CreatedBy = createdBy
        //        };
        //        await _roleRepo.AddAsync(role, ct);
        //        await _roleRepo.AssignPermissionsAsync(role.Id, dto.PermissionIds, createdBy, ct);

        //        _logger.LogInformation("Role '{Name}' created by {UserId}", role.Name, createdBy);
        //        return ServiceResult<RoleDto>.Success(MapToDto(role), "Role created successfully.");
        //    }
        //    catch (Exception ex)
        //    {
        //        _logger.LogError(ex, "Error creating role");
        //        return ServiceResult<RoleDto>.Failure("An error occurred while creating the role.");
        //    }
        //}



        //public async Task<ServiceResult<RoleDto>> CreateAsync(CreateRoleDto dto, Guid createdBy, CancellationToken ct = default)
        //{
        //    try
        //    {
        //        var name = dto.Name?.Trim();
        //        if (string.IsNullOrWhiteSpace(name))
        //            return ServiceResult<RoleDto>.Failure("Role name is required.");

        //        // Check if a role with this name already exists (including soft-deleted)
        //        var existing = await _roleRepo.GetByNameAsync(name, ct);

        //        if (existing != null && !existing.IsDeleted)
        //            return ServiceResult<RoleDto>.Failure($"A role named '{name}' already exists.");

        //        if (existing != null && existing.IsDeleted)
        //        {
        //            // Soft-deleted role with same name — RESTORE it
        //            // This avoids the UNIQUE constraint violation on the Name column
        //            existing.IsDeleted = false;
        //            existing.IsActive = true;
        //            existing.Description = dto.Description?.Trim();
        //            existing.UpdatedBy = createdBy;
        //            existing.UpdatedAt = DateTime.UtcNow;

        //            await _roleRepo.UpdateAsync(existing, ct);
        //            await _roleRepo.RemoveAllPermissionsAsync(existing.Id, ct);
        //            await _roleRepo.AssignPermissionsAsync(existing.Id, dto.PermissionIds, createdBy, ct);

        //            _logger.LogInformation("Role '{Name}' restored by {UserId}", name, createdBy);
        //            return ServiceResult<RoleDto>.Success(MapToDto(existing), "Role restored successfully.");
        //        }

        //        // No existing role — create fresh
        //        var role = new Role
        //        {
        //            Name = name,
        //            Description = dto.Description?.Trim(),
        //            IsSystem = false,
        //            IsActive = true,
        //            CreatedBy = createdBy
        //        };
        //        await _roleRepo.AddAsync(role, ct);
        //        await _roleRepo.AssignPermissionsAsync(role.Id, dto.PermissionIds, createdBy, ct);

        //        _logger.LogInformation("Role '{Name}' created by {UserId}", name, createdBy);
        //        return ServiceResult<RoleDto>.Success(MapToDto(role), "Role created successfully.");
        //    }
        //    catch (Exception ex)
        //    {
        //        _logger.LogError(ex, "Error creating role");
        //        return ServiceResult<RoleDto>.Failure($"Create role failed: {ex.InnerException?.Message ?? ex.Message}");
        //    }
        //}

        // ── CreateAsync — always sets CompanyId ───────────────────
        public async Task<ServiceResult<RoleDto>> CreateAsync(
            CreateRoleDto dto, Guid createdBy, CancellationToken ct = default)
        {
            try
            {
                var name = dto.Name?.Trim();
                if (string.IsNullOrWhiteSpace(name))
                    return ServiceResult<RoleDto>.Failure("Role name is required.");

                // CompanyId is mandatory for company-created roles.
                // System/global roles (SuperAdmin) have no CompanyId.
                if (!dto.CompanyId.HasValue)
                    return ServiceResult<RoleDto>.Failure(
                        "CompanyId is required to create a role.");

                var companyId = dto.CompanyId.Value;

                // Name must be unique within this company
                if (await _roleRepo.NameExistsAsync(name, companyId, null, ct))
                    return ServiceResult<RoleDto>.Failure(
                        $"A role named '{name}' already exists in this company.");

                var role = new Role
                {
                    Name = name,
                    Description = dto.Description?.Trim(),
                    CompanyId = companyId,          // ← key change
                    IsSystem = false,
                    IsActive = true,
                    CreatedBy = createdBy
                };
                await _roleRepo.AddAsync(role, ct);
                await _roleRepo.AssignPermissionsAsync(role.Id, dto.PermissionIds, createdBy, ct);

                _logger.LogInformation("Role '{Name}' created for company {CId} by {UserId}",
                    role.Name, companyId, createdBy);
                return ServiceResult<RoleDto>.Success(MapToDto(role), "Role created successfully.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating role");
                return ServiceResult<RoleDto>.Failure(
                    $"Create role failed: {ex.InnerException?.Message ?? ex.Message}");
            }
        }



        //public async Task<ServiceResult<RoleDto>> UpdateAsync(UpdateRoleDto dto, Guid updatedBy, CancellationToken ct = default)
        //{
        //    try
        //    {
        //        var role = await _roleRepo.GetByIdAsync(dto.Id, ct);
        //        if (role is null) return ServiceResult<RoleDto>.Failure("Role not found.");
        //        if (role.IsSystem && role.Name != dto.Name)
        //            return ServiceResult<RoleDto>.Failure("System role names cannot be changed.");

        //        role.Name = dto.Name.Trim();
        //        role.Description = dto.Description?.Trim();
        //        role.UpdatedAt = DateTime.UtcNow;
        //        role.UpdatedBy = updatedBy;
        //        await _roleRepo.UpdateAsync(role, ct);

        //        // Replace all permissions
        //        await _roleRepo.RemoveAllPermissionsAsync(dto.Id, ct);
        //        await _roleRepo.AssignPermissionsAsync(dto.Id, dto.PermissionIds, updatedBy, ct);

        //        return ServiceResult<RoleDto>.Success(MapToDto(role), "Role updated successfully.");
        //    }
        //    catch (Exception ex)
        //    {
        //        _logger.LogError(ex, "Error updating role {Id}", dto.Id);
        //        return ServiceResult<RoleDto>.Failure("An error occurred while updating the role.");
        //    }
        //}

        // ── UpdateAsync ───────────────────────────────────────────
        public async Task<ServiceResult<RoleDto>> UpdateAsync(
            UpdateRoleDto dto, Guid updatedBy, CancellationToken ct = default)
        {
            try
            {
                var role = await _roleRepo.GetByIdAsync(dto.Id, ct);
                if (role is null)
                      return ServiceResult<RoleDto>.Failure("Role not found.");
                if (role.IsSystem)
                      return ServiceResult<RoleDto>.Failure("System roles cannot be modified.");

                role.Name = dto.Name.Trim();
                role.Description = dto.Description?.Trim();
                role.UpdatedAt = DateTime.UtcNow;
                role.UpdatedBy = updatedBy;
                await _roleRepo.UpdateAsync(role, ct);

                await _roleRepo.RemoveAllPermissionsAsync(dto.Id, ct);
                await _roleRepo.AssignPermissionsAsync(dto.Id, dto.PermissionIds, updatedBy, ct);

                return ServiceResult<RoleDto>.Success(MapToDto(role), "Role updated successfully.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating role {Id}", dto.Id);
                return ServiceResult<RoleDto>.Failure("An error occurred while updating the role.");
            }
        }



        //public async Task<ServiceResult> DeleteAsync(Guid id, Guid deletedBy, CancellationToken ct = default)
        //{
        //    try
        //    {
        //        var role = await _roleRepo.GetByIdAsync(id, ct);
        //        if (role is null) return ServiceResult.Failure("Role not found.");
        //        if (role.IsSystem) return ServiceResult.Failure("System roles cannot be deleted.");
        //        if (await _roleRepo.HasUsersAssignedAsync(id, ct))
        //            return ServiceResult.Failure("Cannot delete a role that has users assigned. Reassign users first.");

        //        await _roleRepo.DeleteAsync(id, deletedBy, ct);
        //        return ServiceResult.Success("Role deleted successfully.");
        //    }
        //    catch (Exception ex)
        //    {
        //        _logger.LogError(ex, "Error deleting role {Id}", id);
        //        return ServiceResult.Failure("An error occurred while deleting the role.");
        //    }
        //}

        public async Task<ServiceResult> DeleteAsync(Guid id, Guid deletedBy, CancellationToken ct = default)
        {
            try
            {
                var role = await _roleRepo.GetByIdAsync(id, ct);
                if (role is null) return ServiceResult.Failure("Role not found.");
                if (role.IsSystem) return ServiceResult.Failure("System roles cannot be deleted.");
                if (await _roleRepo.HasUsersAssignedAsync(id, ct))
                    return ServiceResult.Failure("Cannot delete — users are assigned to this role.");

                // ✅ DELETE PERMISSIONS FIRST (foreign key constraint)
                await _roleRepo.RemoveAllPermissionsAsync(id, ct);
                await _roleRepo.DeleteAsync(id, deletedBy, ct);

                return ServiceResult.Success("Role deleted successfully.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting role {Id} — {Message}", id, ex.Message);
                return ServiceResult.Failure($"Delete failed: {ex.InnerException?.Message ?? ex.Message}");
            }
        }

        //public async Task<ServiceResult<IEnumerable<SelectItemDto>>> GetSelectListAsync(CancellationToken ct = default)
        //{
        //    var roles = await _roleRepo.GetAllWithPermissionsAsync(ct);
        //    var items = roles.Where(r => r.IsActive).Select(r => new SelectItemDto { Value = r.Id.ToString(), Text = r.Name });
        //    return ServiceResult<IEnumerable<SelectItemDto>>.Success(items);
        //}

        // ── GetSelectListAsync — company-scoped ───────────────────
        // Returns only roles belonging to this company (+ system roles).
        // Company users must only be able to assign roles from their
        // own company when creating/editing users.
        public async Task<ServiceResult<IEnumerable<SelectItemDto>>> GetSelectListAsync(
            Guid? companyId = null, CancellationToken ct = default)
        {
            var roles = await _roleRepo.GetAllWithPermissionsAsync(companyId, ct);
            var items = roles
                .Where(r => r.IsActive && !r.IsSystem)   // hide "Super Admin" from dropdowns
                .Where(r => companyId is null
                            || r.CompanyId == companyId
                            || r.CompanyId is null)
                .Select(r => new SelectItemDto { Value = r.Id.ToString(), Text = r.Name });
            return ServiceResult<IEnumerable<SelectItemDto>>.Success(items);
        }

        //// Backward-compat overload
        //public async Task<ServiceResult<IEnumerable<SelectItemDto>>> GetSelectListAsync(
        //    CancellationToken ct = default)
        //    => await GetSelectListAsync(null, ct);


        public async Task<ServiceResult<IEnumerable<PermissionGroupDto>>> GetAllPermissionsGroupedAsync(CancellationToken ct = default)
        {
            var all = await _permRepo.GetAllAsync(ct);
            var groups = all.Where(p => !p.IsDeleted)
                .GroupBy(p => p.Module)
                .OrderBy(g => g.Key)
                .Select(g => new PermissionGroupDto
                {
                    Module = g.Key,
                    Permissions = g.Select(p => new PermissionDto
                    {
                        Id = p.Id,
                        Name = p.Name,
                        Code = p.Code,
                        Module = p.Module,
                        Description = p.Description
                    }).ToList()
                });
            return ServiceResult<IEnumerable<PermissionGroupDto>>.Success(groups);
        }

        public async Task<ServiceResult<IEnumerable<PermissionGroupDto>>> GetPermissionsForRoleAsync(Guid roleId, CancellationToken ct = default)
        {
            var result = await GetDetailAsync(roleId, ct);
            if (!result.Succeeded) return ServiceResult<IEnumerable<PermissionGroupDto>>.Failure(result.Errors);
            return ServiceResult<IEnumerable<PermissionGroupDto>>.Success(result.Data!.PermissionGroups);
        }

        private static RoleDto MapToDto(Role r) => new()
        {
            Id = r.Id,
            Name = r.Name,
            Description = r.Description,
            IsSystem = r.IsSystem,
            IsActive = r.IsActive,
            PermissionCount = r.RolePermissions?.Count ?? 0,
            CreatedAt = r.CreatedAt
        };
    }
}
