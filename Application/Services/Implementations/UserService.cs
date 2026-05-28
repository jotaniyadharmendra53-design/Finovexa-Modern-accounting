using InvoiceSaaS.Application.Common;
using InvoiceSaaS.Application.DTOs.Common;
using InvoiceSaaS.Application.DTOs.Users;
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
//  UserService
// ═══════════════════════════════════════════════════════════
public class UserService : IUserService
    {
        private readonly IUserRepository _userRepo;
        private readonly ICompanyRepository _companyRepo;
        private readonly IEmailService _emailService;
        private readonly ILogger<UserService> _logger;

        public UserService(IUserRepository userRepo, ICompanyRepository companyRepo,
            IEmailService emailService, ILogger<UserService> logger)
        {
            _userRepo = userRepo;
            _companyRepo = companyRepo;
            _emailService = emailService;
            _logger = logger;
        }

        public async Task<ServiceResult<UserDto>> GetByIdAsync(Guid id, CancellationToken ct = default)
        {
            var user = await _userRepo.GetWithRoleAndPermissionsAsync(id, ct);
            if (user is null) return ServiceResult<UserDto>.Failure("User not found.");
            var company = await _companyRepo.GetByUserAsync(id, ct);
            return ServiceResult<UserDto>.Success(await MapToDtoAsync(user, company));
        }

        public async Task<ServiceResult<IEnumerable<UserListItemDto>>> GetAllAsync(Guid? companyId = null, CancellationToken ct = default)
        {
            var users = await _userRepo.GetAllWithRolesAsync(companyId, ct);
            var items = users.Select(u => new UserListItemDto
            {
                Id = u.Id,
                FullName = u.FullName,
                Email = u.Email,
                Phone = u.Phone,
                IsActive = u.IsActive,
                RoleName = u.Role?.Name ?? string.Empty,
                RoleIsSystem = u.Role?.IsSystem ?? false,
                CreatedAt = u.CreatedAt,
                LastLoginAt = u.LastLoginAt
            });
            return ServiceResult<IEnumerable<UserListItemDto>>.Success(items);
        }

        //public async Task<ServiceResult<UserDto>> CreateAsync(CreateUserDto dto, Guid createdBy, CancellationToken ct = default)
        //{
        //    try
        //    {
        //        var user = new User
        //        {
        //            FullName = dto.FullName.Trim(),
        //            Email = dto.Email.Trim().ToLower(),
        //            PasswordHash = BCrypt.Net.BCrypt.HashPassword(dto.Password, workFactor: 11),
        //            RoleId = dto.RoleId,
        //            Phone = dto.Phone?.Trim(),
        //            IsActive = dto.IsActive,
        //            CreatedBy = createdBy
        //        };
        //        await _userRepo.AddAsync(user, ct);

        //        // Link to company if provided
        //        if (dto.CompanyId.HasValue)
        //        {
        //            // handled by repository/EF
        //        }

        //        // Send welcome email
        //        if (dto.SendWelcomeEmail)
        //        {
        //            await _emailService.QueueAsync(new EmailMessage
        //            {
        //                ToEmail = user.Email,
        //                ToName = user.FullName,
        //                Subject = "Welcome to Finovexa",
        //                Body = BuildWelcomeEmail(user.FullName, user.Email, dto.Password),
        //                IsHtml = true,
        //                RelatedId = user.Id,
        //                EmailType = "Welcome"
        //            }, ct);
        //        }

        //        _logger.LogInformation("User {Email} created by {CreatedBy}", user.Email, createdBy);
        //        var createdUser = await _userRepo.GetWithRoleAndPermissionsAsync(user.Id, ct);
        //        return ServiceResult<UserDto>.Success(await MapToDtoAsync(createdUser!, null), "User created successfully.");
        //    }
        //    catch (Exception ex)
        //    {
        //        _logger.LogError(ex, "Error creating user");
        //        return ServiceResult<UserDto>.Failure("An error occurred while creating the user.");
        //    }
        //}

        public async Task<ServiceResult<UserDto>> CreateAsync(
           CreateUserDto dto, Guid createdBy, CancellationToken ct = default)
        {
            try
            {
                // Company users must have a CompanyId
                if (!dto.CompanyId.HasValue)
                    return ServiceResult<UserDto>.Failure(
                        "A company must be selected when creating a user.");

                if (await _userRepo.EmailExistsAsync(dto.Email.Trim().ToLower(), null, ct))
                    return ServiceResult<UserDto>.Failure(
                        $"A user with email '{dto.Email}' already exists.");

                var user = new User
                {
                    FullName = dto.FullName.Trim(),
                    Email = dto.Email.Trim().ToLower(),
                    PasswordHash = BCrypt.Net.BCrypt.HashPassword(dto.Password, workFactor: 11),
                    RoleId = dto.RoleId,
                    Phone = dto.Phone?.Trim(),
                    IsActive = dto.IsActive,
                    IsFirstLogin = false,   // ← ADD THIS LINE
                    CreatedBy = createdBy
                };
                await _userRepo.AddAsync(user, ct);

                // ── Link to company ───────────────────────────────
                await _userRepo.AddToCompanyAsync(user.Id, dto.CompanyId.Value, ct);

                // ── Welcome email ─────────────────────────────────
                if (dto.SendWelcomeEmail)
                {
                    await _emailService.QueueAsync(new EmailMessage
                    {
                        ToEmail = user.Email,
                        ToName = user.FullName,
                        Subject = "Welcome to Finovexa",
                        Body = BuildWelcomeEmail(user.FullName, user.Email, dto.Password),
                        IsHtml = true,
                        RelatedId = user.Id,
                        EmailType = "Welcome"
                    }, ct);
                }

                _logger.LogInformation("User {Email} created for company {CId} by {By}",
                    user.Email, dto.CompanyId, createdBy);

                var created = await _userRepo.GetWithRoleAndPermissionsAsync(user.Id, ct);
                var company = await _companyRepo.GetByUserAsync(user.Id, ct);
                return ServiceResult<UserDto>.Success(
                    await MapToDtoAsync(created!, company), "User created successfully.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating user");
                return ServiceResult<UserDto>.Failure(
                    $"Create user failed: {ex.InnerException?.Message ?? ex.Message}");
            }
        }

        public async Task<ServiceResult<UserDto>> UpdateAsync(UpdateUserDto dto, Guid updatedBy, CancellationToken ct = default)
        {
            try
            {
                var user = await _userRepo.GetByIdAsync(dto.Id, ct);
                if (user is null) return ServiceResult<UserDto>.Failure("User not found.");

                user.FullName = dto.FullName.Trim();
                user.Email = dto.Email.Trim().ToLower();
                user.Phone = dto.Phone?.Trim();
                user.RoleId = dto.RoleId;
                user.IsActive = dto.IsActive;
                user.UpdatedAt = DateTime.UtcNow;
                user.UpdatedBy = updatedBy;
                await _userRepo.UpdateAsync(user, ct);

                var updated = await _userRepo.GetWithRoleAndPermissionsAsync(user.Id, ct);
                return ServiceResult<UserDto>.Success(await MapToDtoAsync(updated!, null), "User updated successfully.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating user {Id}", dto.Id);
                return ServiceResult<UserDto>.Failure("An error occurred while updating the user.");
            }
        }

        public async Task<ServiceResult> DeleteAsync(Guid id, Guid deletedBy, CancellationToken ct = default)
        {
            if (id == deletedBy) return ServiceResult.Failure("You cannot delete your own account.");
            var user = await _userRepo.GetByIdAsync(id, ct);
            if (user is null) return ServiceResult.Failure("User not found.");
            await _userRepo.DeleteAsync(id, deletedBy, ct);
            return ServiceResult.Success("User deleted successfully.");
        }

        public async Task<ServiceResult> ToggleActiveAsync(Guid id, Guid updatedBy, CancellationToken ct = default)
        {
            if (id == updatedBy) return ServiceResult.Failure("You cannot deactivate your own account.");
            var user = await _userRepo.GetByIdAsync(id, ct);
            if (user is null) return ServiceResult.Failure("User not found.");
            user.IsActive = !user.IsActive;
            user.UpdatedAt = DateTime.UtcNow;
            user.UpdatedBy = updatedBy;
            await _userRepo.UpdateAsync(user, ct);
            return ServiceResult.Success(user.IsActive ? "User activated." : "User deactivated.");
        }

        //public async Task<ServiceResult<IEnumerable<SelectItemDto>>> GetSelectListAsync(CancellationToken ct = default)
        //{
        //    var users = await _userRepo.GetAllWithRolesAsync(null, ct);
        //    var items = users.Where(u => u.IsActive).Select(u => new SelectItemDto
        //    {
        //        Value = u.Id.ToString(),
        //        Text = $"{u.FullName} ({u.Email})"
        //    });
        //    return ServiceResult<IEnumerable<SelectItemDto>>.Success(items);
        //}


        // ── GetSelectListAsync — scoped to company ────────────────
        public async Task<ServiceResult<IEnumerable<SelectItemDto>>> GetSelectListAsync(
            Guid? companyId = null, CancellationToken ct = default)
        {
            var users = await _userRepo.GetAllWithRolesAsync(companyId, ct);
            var items = users
                .Where(u => u.IsActive)
                .Select(u => new SelectItemDto
                {
                    Value = u.Id.ToString(),
                    Text = $"{u.FullName} ({u.Email})"
                });
            return ServiceResult<IEnumerable<SelectItemDto>>.Success(items);
        }

        // Backward-compat overload
        public async Task<ServiceResult<IEnumerable<SelectItemDto>>> GetSelectListAsync(
            CancellationToken ct = default)
            => await GetSelectListAsync(null, ct);



        private async Task<UserDto> MapToDtoAsync(User u, InvoiceSaaS.Domain.Entities.Company? company)
        {
            var permissions = await _userRepo.GetUserPermissionsAsync(u.Id);
            return new UserDto
            {
                Id = u.Id,
                FullName = u.FullName,
                Email = u.Email,
                Phone = u.Phone,
                ProfilePicture = u.ProfilePicture,
                IsActive = u.IsActive,
                IsEmailVerified = u.IsEmailVerified,
                LastLoginAt = u.LastLoginAt,
                CreatedAt = u.CreatedAt,
                RoleId = u.RoleId,
                RoleName = u.Role?.Name ?? string.Empty,
                RoleIsSystem = u.Role?.IsSystem ?? false,
                CompanyId = company?.Id,
                CompanyName = company?.Name,
                Permissions = permissions
            };
        }

        private static string BuildWelcomeEmail(string name, string email, string tempPassword) => $"""
        <!DOCTYPE html><html><body style="font-family:Arial,sans-serif;background:#f4f4f4;margin:0;padding:0;">
        <table width="100%" cellpadding="0" cellspacing="0">
          <tr><td align="center" style="padding:40px 0;">
            <table width="600" style="background:#fff;border-radius:8px;overflow:hidden;box-shadow:0 2px 8px rgba(0,0,0,0.1);">
              <tr><td style="background:#4F46E5;padding:32px 40px;">
                <h1 style="color:#fff;margin:0;font-size:24px;">Finovexa</h1>
              </td></tr>
              <tr><td style="padding:40px;">
                <h2 style="color:#1e293b;margin-top:0;">Welcome, {name}!</h2>
                <p style="color:#475569;line-height:1.6;">Your account has been created. Here are your login details:</p>
                <div style="background:#f8fafc;border-radius:8px;padding:20px;margin:20px 0;">
                  <p style="margin:4px 0;color:#475569;"><strong>Email:</strong> {email}</p>
                  <p style="margin:4px 0;color:#475569;"><strong>Temporary Password:</strong> {tempPassword}</p>
                </div>
                <p style="color:#e11d48;font-size:13px;">Please change your password after first login.</p>
              </td></tr>
            </table>
          </td></tr>
        </table></body></html>
        """;
    }
}
