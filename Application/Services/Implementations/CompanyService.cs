using InvoiceSaaS.Application.Common;
using InvoiceSaaS.Application.DTOs.Companies;
using InvoiceSaaS.Application.Services.Interfaces;
using InvoiceSaaS.Domain.Entities;
using InvoiceSaaS.Domain.Interfaces;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Text;

namespace InvoiceSaaS.Application.Services.Implementations
{
    public class CompanyService : ICompanyService
    {
        private readonly ICompanyRepository _companyRepo;
        private readonly IUserRepository _userRepo;
        private readonly IRoleRepository _roleRepo;
        private readonly IPermissionRepository _permRepo;
        private readonly IFiscalYearService _fyService;
        private readonly IEmailService _emailService;
        private readonly ILogger<CompanyService> _logger;

        public CompanyService(
            ICompanyRepository companyRepo,
            IUserRepository userRepo,
            IRoleRepository roleRepo,
            IPermissionRepository permRepo,
            IFiscalYearService fyService,
            IEmailService emailService,
            ILogger<CompanyService> logger)
        {
            _companyRepo = companyRepo;
            _userRepo = userRepo;
            _roleRepo = roleRepo;
            _permRepo = permRepo;
            _fyService = fyService;
            _emailService = emailService;
            _logger = logger;
        }

        // ════════════════════════════════════════════════════════
        //  ProvisionAsync — the heart of Step 2
        //  Creates: Company → Admin Role (all perms) → Admin User
        //           → UserCompany link → FiscalYear seed
        //           → Welcome email
        // ════════════════════════════════════════════════════════
        public async Task<ServiceResult<CompanyProvisionResultDto>> ProvisionAsync(
            CreateCompanyDto dto, Guid createdBy, CancellationToken ct = default)
        {
            try
            {
                // ── Validate inputs ────────────────────────────
                if (string.IsNullOrWhiteSpace(dto.CompanyName))
                    return ServiceResult<CompanyProvisionResultDto>.Failure(
                        "Company name is required.");

                if (string.IsNullOrWhiteSpace(dto.AdminEmail))
                    return ServiceResult<CompanyProvisionResultDto>.Failure(
                        "Admin email is required.");

                if (string.IsNullOrWhiteSpace(dto.AdminPassword))
                    return ServiceResult<CompanyProvisionResultDto>.Failure(
                        "Admin password is required.");

                if (await _companyRepo.NameExistsAsync(dto.CompanyName.Trim(), null, ct))
                    return ServiceResult<CompanyProvisionResultDto>.Failure(
                        $"A company named '{dto.CompanyName}' already exists.");

                //if (await _userRepo.EmailExistsAsync(dto.AdminEmail.Trim().ToLower(), null, ct))
                //    return ServiceResult<CompanyProvisionResultDto>.Failure(
                //        $"A user with email '{dto.AdminEmail}' already exists.");

                if (await _userRepo.EmailExistsAsync(dto.AdminEmail.Trim().ToLower(), null, ct))
                    return ServiceResult<CompanyProvisionResultDto>.Failure(
                        "Admin Email already exists");

                // ── STEP A: Create the Company ─────────────────
                var company = new Company
                {
                    Name = dto.CompanyName.Trim(),
                    Email = dto.CompanyEmail?.Trim().ToLower(),
                    Phone = dto.CompanyPhone?.Trim(),
                    Website = dto.CompanyWebsite?.Trim(),
                    Address = dto.CompanyAddress?.Trim(),
                    CurrencyCode = dto.CurrencyCode?.Trim().ToUpper() ?? "INR",
                    InvoiceTemplate = "classic",
                    FiscalYearStartMonth = dto.FiscalStartMonth > 0 && dto.FiscalStartMonth <= 12
                                              ? dto.FiscalStartMonth : 4,
                    IsActive = true,
                    OwnerId = createdBy   // temporary — updated after admin user is created
                };
                await _companyRepo.AddAsync(company, ct);
                _logger.LogInformation("Company '{Name}' created (Id={Id})",
                    company.Name, company.Id);

                // ── STEP B: Create Admin Role with ALL permissions ─
                var allPermissions = (await _permRepo.GetAllAsync(ct)).ToList();
                var adminRole = new Role
                {
                    Name = "Admin",
                    Description = $"Administrator role for {company.Name}",
                    CompanyId = company.Id,
                    IsSystem = true,
                    IsActive = true,
                    CreatedBy = createdBy
                };
                await _roleRepo.AddAsync(adminRole, ct);
                await _roleRepo.AssignPermissionsAsync(
                    adminRole.Id,
                    allPermissions.Select(p => p.Id),
                    createdBy, ct);
                _logger.LogInformation(
                    "Admin role created for company {CId} with {PermCount} permissions",
                    company.Id, allPermissions.Count);

                // ── STEP C: Create Admin User ──────────────────
                var adminUser = new User
                {
                    FullName = dto.AdminFullName?.Trim() ?? dto.AdminEmail.Split('@')[0],
                    Email = dto.AdminEmail.Trim().ToLower(),
                    PasswordHash = BCrypt.Net.BCrypt.HashPassword(dto.AdminPassword, workFactor: 11),
                    RoleId = adminRole.Id,
                    Phone = dto.AdminPhone?.Trim(),
                    IsActive = true,
                    CreatedBy = createdBy
                };
                await _userRepo.AddAsync(adminUser, ct);
                _logger.LogInformation("Admin user '{Email}' created for company {CId}",
                    adminUser.Email, company.Id);

                // ── STEP D: Set Company.OwnerId to the real admin ─
                company.OwnerId = adminUser.Id;
                company.UpdatedAt = DateTime.UtcNow;
                company.UpdatedBy = createdBy;
                await _companyRepo.UpdateAsync(company, ct);

                // ── STEP E: Link User → Company (UserCompanies) ─
                await _userRepo.AddToCompanyAsync(adminUser.Id, company.Id, ct);
                _logger.LogInformation("User {UserId} linked to company {CId}",
                    adminUser.Id, company.Id);

                // ── STEP F: Seed first Fiscal Year ────────────
                var fyResult = await _fyService.OpenFirstAsync(
                    company.Id,
                    company.FiscalYearStartMonth,
                    adminUser.Id,
                    ct);

                var fyLabel = fyResult.Succeeded
                    ? fyResult.Data!.Label
                    : "FY (seeding failed — run manually)";

                if (!fyResult.Succeeded)
                    _logger.LogWarning("FY seeding failed for company {CId}: {Err}",
                        company.Id, fyResult.Errors.FirstOrDefault());

                // ── STEP G: Send welcome email ─────────────────
                if (dto.SendWelcomeEmail)
                {
                    await _emailService.QueueAsync(new EmailMessage
                    {
                        ToEmail = adminUser.Email,
                        ToName = adminUser.FullName,
                        Subject = $"Welcome to Finovexa — {company.Name}",
                        Body = BuildWelcomeEmail(
                                        adminUser.FullName,
                                        company.Name,
                                        adminUser.Email,
                                        dto.AdminPassword),
                        IsHtml = true,
                        RelatedId = adminUser.Id,
                        EmailType = "Welcome"
                    }, ct);
                    _logger.LogInformation("Welcome email queued for {Email}", adminUser.Email);
                }

                var result = new CompanyProvisionResultDto
                {
                    CompanyId = company.Id,
                    CompanyName = company.Name,
                    AdminUserId = adminUser.Id,
                    AdminEmail = adminUser.Email,
                    AdminRoleId = adminRole.Id,
                    FiscalYearLabel = fyLabel,
                    Message = $"Company '{company.Name}' provisioned. " +
                                      $"Admin login: {adminUser.Email}"
                };

                _logger.LogInformation(
                    "Company provisioning complete: {CId} — admin: {Email}",
                    company.Id, adminUser.Email);

                return ServiceResult<CompanyProvisionResultDto>.Success(result, result.Message);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "ProvisionAsync failed for company '{Name}'",
                    dto.CompanyName);
                return ServiceResult<CompanyProvisionResultDto>.Failure(
                    $"Provisioning failed: {ex.InnerException?.Message ?? ex.Message}");
            }
        }

        // ── GetAllAsync (SuperAdmin only) ─────────────────────
        public async Task<ServiceResult<IEnumerable<CompanyDto>>> GetAllAsync(
            CancellationToken ct = default)
        {
            //var companies = await _companyRepo.GetAllAsync(ct);
            //return ServiceResult<IEnumerable<CompanyDto>>.Success(
            //    companies.Select(MapToDto));
            var companies = await _companyRepo.GetAllAsync(ct);
            var dtos = new List<CompanyDto>();
            foreach (var c in companies)
            {
                var dto = MapToDto(c);
                if (c.OwnerId != Guid.Empty)
                {
                    var owner = await _userRepo.GetByIdAsync(c.OwnerId, ct);
                    dto.AdminEmail = owner?.Email;
                    dto.AdminFullName = owner?.FullName;
                }
                dtos.Add(dto);
            }
            return ServiceResult<IEnumerable<CompanyDto>>.Success(dtos);
        }

        // ── ToggleActiveAsync ─────────────────────────────────
        public async Task<ServiceResult> ToggleActiveAsync(
            Guid companyId, Guid updatedBy, CancellationToken ct = default)
        {
            var company = await _companyRepo.GetByIdAsync(companyId, ct);
            if (company is null) return ServiceResult.Failure("Company not found.");
            company.IsActive = !company.IsActive;
            company.UpdatedAt = DateTime.UtcNow;
            company.UpdatedBy = updatedBy;
            await _companyRepo.UpdateAsync(company, ct);
            return ServiceResult.Success(
                company.IsActive ? "Company activated." : "Company deactivated.");
        }

        // ── Existing methods (unchanged) ──────────────────────
        public async Task<ServiceResult<CompanyDto>> GetByUserAsync(
            Guid userId, CancellationToken ct = default)
        {
            var company = await _companyRepo.GetByUserAsync(userId, ct);
            if (company is null) return ServiceResult<CompanyDto>.Failure("Company not found.");
            return ServiceResult<CompanyDto>.Success(MapToDto(company));
        }

        public async Task<ServiceResult<CompanyDto>> UpdateAsync(
            UpdateCompanyDto dto, Guid updatedBy, CancellationToken ct = default)
        {
            try
            {
                var company = await _companyRepo.GetByIdAsync(dto.Id, ct);
                if (company is null) 
                    return ServiceResult<CompanyDto>.Failure("Company not found.");

                company.Name = dto.Name.Trim();
                company.Email = dto.Email?.Trim().ToLower();
                company.Phone = dto.Phone?.Trim();
                company.Website = dto.Website?.Trim();
                company.Address = dto.Address?.Trim();
                company.City = dto.City?.Trim();
                company.State = dto.State?.Trim();
                company.Country = dto.Country?.Trim();
                company.PostalCode = dto.PostalCode?.Trim();
                company.TaxNumber = dto.TaxNumber?.Trim();
                company.CurrencyCode = dto.CurrencyCode.Trim().ToUpper();
                company.InvoiceTemplate = dto.InvoiceTemplate?.Trim().ToLower() ?? "classic";
                company.UpdatedAt = DateTime.UtcNow;
                company.UpdatedBy = updatedBy;

                await _companyRepo.UpdateAsync(company, ct);
                return ServiceResult<CompanyDto>.Success(MapToDto(company),
                    "Company settings updated.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating company {Id}", dto.Id);
                return ServiceResult<CompanyDto>.Failure(
                    "An error occurred while saving company settings.");
            }
        }

        public async Task<ServiceResult<string>> UploadLogoAsync(
            Guid companyId, Stream fileStream, string fileName, CancellationToken ct = default)
        {
            try
            {
                var company = await _companyRepo.GetByIdAsync(companyId, ct);
                if (company is null) return ServiceResult<string>.Failure("Company not found.");

                var ext = Path.GetExtension(fileName).ToLowerInvariant();
                var allowed = new[] { ".jpg", ".jpeg", ".png", ".webp"};
                if (!allowed.Contains(ext))
                    return ServiceResult<string>.Failure(
                        "Only JPG, PNG, WebP or SVG files are allowed.");

                var logoName = $"logo_{companyId}{ext}";
                var savePath = Path.Combine("wwwroot", "uploads", "logos", logoName);
                Directory.CreateDirectory(Path.GetDirectoryName(savePath)!);

                await using var fs = File.Create(savePath);
                await fileStream.CopyToAsync(fs, ct);

                //var logoUrl = $"/uploads/logos/{logoName}";
                //company.Logo = logoUrl;
                company.Logo = logoName;
                company.UpdatedAt = DateTime.UtcNow;
                await _companyRepo.UpdateAsync(company, ct);

                return ServiceResult<string>.Success(logoName, "Logo uploaded successfully.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error uploading logo for company {Id}", companyId);
                return ServiceResult<string>.Failure(
                    "An error occurred while uploading the logo.");
            }
        }

        public async Task<ServiceResult> SaveTemplateAsync(
            Guid companyId, string template, CancellationToken ct = default)
        {
            var allowed = new[] { "classic", "modern", "minimal", "elegant" };
            var tpl = template?.Trim().ToLower() ?? "classic";
            if (!allowed.Contains(tpl)) return ServiceResult.Failure("Invalid template name.");

            var company = await _companyRepo.GetByIdAsync(companyId, ct);
            if (company is null) return ServiceResult.Failure("Company not found.");

            company.InvoiceTemplate = tpl;
            company.UpdatedAt = DateTime.UtcNow;
            await _companyRepo.UpdateAsync(company, ct);
            return ServiceResult.Success($"Template '{tpl}' saved successfully.");
        }

        // ── First-login setup: save company settings and update user password
        public async Task<ServiceResult> CompleteSetupAsync(SetupCompanyDto dto, Guid userId, CancellationToken ct = default)
        {
            try
            {
                var company = await _companyRepo.GetByIdAsync(dto.CompanyId, ct);
                if (company is null) return ServiceResult.Failure("Company not found.");

                company.Name = dto.Name.Trim();
                company.Phone = dto.Phone?.Trim();
                company.Address = dto.Address?.Trim();
                company.TaxNumber = dto.TaxNumber?.Trim();
                company.CurrencyCode = string.IsNullOrWhiteSpace(dto.CurrencyCode) ? company.CurrencyCode : dto.CurrencyCode.Trim().ToUpper();
                company.Timezone = dto.Timezone?.Trim();
                company.DateFormat = dto.DateFormat?.Trim();
                company.TaxType = dto.TaxType;
                company.GstType = dto.GstType;
                company.IsSetupCompleted = true;
                company.UpdatedAt = DateTime.UtcNow;
                await _companyRepo.UpdateAsync(company, ct);

                var user = await _userRepo.GetByIdAsync(userId, ct);
                if (user is null) return ServiceResult.Failure("User not found.");
                // Update password and mark first-login complete
                user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(dto.NewPassword, workFactor: 11);
                user.IsFirstLogin = false;
                user.UpdatedAt = DateTime.UtcNow;
                await _userRepo.UpdateAsync(user, ct);

                return ServiceResult.Success("Setup completed.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error completing setup for company {Id}", dto.CompanyId);
                return ServiceResult.Failure($"Setup failed: {ex.InnerException?.Message ?? ex.Message}");
            }
        }

        //public async Task<bool> NeedsSetupAsync(Guid userId, CancellationToken ct = default)
        //{
        //    var user = await _userRepo.GetByIdAsync(userId, ct);
        //    return user is not null && user.IsFirstLogin;
        //}

        public async Task<bool> NeedsSetupAsync(Guid userId, CancellationToken ct = default)
        {
            // Step 1: find the company this user belongs to
            var company = await _companyRepo.GetByUserAsync(userId, ct);

            // If the user has no company, or the company is already set up → no setup needed
            if (company is null || company.IsSetupCompleted)
                return false;

            // Step 2: only the company OWNER needs to complete setup.
            //         Employees (created by Admin) go straight to Dashboard.
            //         OwnerId is set to the Admin user when the company is provisioned.
            if (company.OwnerId != userId)
                return false;

            // Step 3: also check IsFirstLogin as a double-guard
            var user = await _userRepo.GetByIdAsync(userId, ct);
            if (user is null || !user.IsFirstLogin)
                return false;

            // This user is the company owner AND setup is not done → show setup wizard
            return true;
        }


        // ── Mapper ────────────────────────────────────────────
        private static CompanyDto MapToDto(Company c) => new()
        {
            Id = c.Id,
            Name = c.Name,
            Email = c.Email,
            Phone = c.Phone,
            Website = c.Website,
            Address = c.Address,
            City = c.City,
            State = c.State,
            Country = c.Country,
            PostalCode = c.PostalCode,
            Logo = c.Logo,
            TaxNumber = c.TaxNumber,
            CurrencyCode = c.CurrencyCode,
            InvoiceTemplate = c.InvoiceTemplate ?? "classic",
            IsActive = c.IsActive
        };


        // ── EditAsync — SuperAdmin edits a company ─────────────────
        public async Task<ServiceResult<CompanyDto>> EditAsync(
            EditCompanyDto dto, Guid updatedBy, CancellationToken ct = default)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(dto.Name))
                    return ServiceResult<CompanyDto>.Failure("Company name is required.");

                var company = await _companyRepo.GetByIdAsync(dto.Id, ct);
                if (company is null)
                    return ServiceResult<CompanyDto>.Failure("Company not found.");

                // Check name uniqueness (excluding this company)
                if (!company.Name.Equals(dto.Name.Trim(), StringComparison.OrdinalIgnoreCase)
                    && await _companyRepo.NameExistsAsync(dto.Name.Trim(), dto.Id, ct))
                    return ServiceResult<CompanyDto>.Failure(
                        $"A company named '{dto.Name}' already exists.");

                company.Name = dto.Name.Trim();
                company.Email = dto.Email?.Trim().ToLower();
                company.Phone = dto.Phone?.Trim();
                company.Website = dto.Website?.Trim();
                company.Address = dto.Address?.Trim();
                company.CurrencyCode = (dto.CurrencyCode?.Trim().ToUpper()) ?? "INR";
                company.UpdatedAt = DateTime.UtcNow;
                company.UpdatedBy = updatedBy;
                await _companyRepo.UpdateAsync(company, ct);

                // ✅ Update admin user if provided
                if (!string.IsNullOrWhiteSpace(dto.AdminFullName) || !string.IsNullOrWhiteSpace(dto.AdminEmail))
                {
                    var owner = await _userRepo.GetByIdAsync(company.OwnerId, ct);
                    if (owner != null)
                    {
                        if (!string.IsNullOrWhiteSpace(dto.AdminFullName))
                            owner.FullName = dto.AdminFullName.Trim();

                        if (!string.IsNullOrWhiteSpace(dto.AdminEmail))
                            owner.Email = dto.AdminEmail.Trim().ToLower();

                        owner.UpdatedAt = DateTime.UtcNow;
                        owner.UpdatedBy = updatedBy;
                        await _userRepo.UpdateAsync(owner, ct);
                    }
                }

                _logger.LogInformation("Company {Id} edited by SuperAdmin {By}", dto.Id, updatedBy);
                return ServiceResult<CompanyDto>.Success(MapToDto(company), "Company updated.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "EditAsync failed for company {Id}", dto.Id);
                return ServiceResult<CompanyDto>.Failure(
                    $"Edit failed: {ex.InnerException?.Message ?? ex.Message}");
            }
        }

        // ── DeleteAsync — SuperAdmin soft-deletes a company ────────
        public async Task<ServiceResult> DeleteAsync(
            Guid companyId, Guid deletedBy, CancellationToken ct = default)
        {
            try
            {
                var company = await _companyRepo.GetByIdAsync(companyId, ct);
                if (company is null)
                    return ServiceResult.Failure("Company not found.");

                company.IsDeleted = true;
                company.DeletedAt = DateTime.UtcNow;
                company.UpdatedAt = DateTime.UtcNow;
                company.UpdatedBy = deletedBy;
                await _companyRepo.UpdateAsync(company, ct);

                _logger.LogInformation("Company {Id} deleted by SuperAdmin {By}", companyId, deletedBy);
                return ServiceResult.Success("Company deleted.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "DeleteAsync failed for company {Id}", companyId);
                return ServiceResult.Failure(
                    $"Delete failed: {ex.InnerException?.Message ?? ex.Message}");
            }
        }



        // ── Welcome email template ────────────────────────────
        private static string BuildWelcomeEmail(
            string name, string companyName, string email, string password) => $"""
            <!DOCTYPE html><html><body style="font-family:Arial,sans-serif;background:#f4f4f4;margin:0;padding:0;">
            <table width="100%" cellpadding="0" cellspacing="0">
              <tr><td align="center" style="padding:40px 20px;">
                <table width="600" style="background:#fff;border-radius:10px;overflow:hidden;box-shadow:0 2px 12px rgba(0,0,0,.1);">
                  <tr><td style="background:#4F46E5;padding:28px 40px;">
                    <h1 style="color:#fff;margin:0;font-size:22px;font-weight:800;">Finovexa</h1>
                    <p style="color:#c7d2fe;margin:4px 0 0;font-size:14px;">Your company is ready</p>
                  </td></tr>
                  <tr><td style="padding:32px 40px;">
                    <h2 style="color:#1e293b;margin-top:0;">Welcome, {name}!</h2>
                    <p style="color:#475569;line-height:1.6;">
                      Your company <strong>{companyName}</strong> has been set up on Finovexa.
                      You are the Admin — you have full access to all features.
                    </p>
                    <div style="background:#EEF2FF;border-radius:8px;padding:20px 24px;margin:24px 0;">
                      <p style="margin:0 0 8px;font-size:13px;font-weight:700;color:#4F46E5;text-transform:uppercase;letter-spacing:.06em;">Your login credentials</p>
                      <p style="margin:6px 0;color:#1e293b;font-size:14px;"><strong>Email:</strong> {email}</p>
                      <p style="margin:6px 0;color:#1e293b;font-size:14px;"><strong>Password:</strong> {password}</p>
                    </div>
                    <p style="color:#e11d48;font-size:13px;font-weight:600;">
                      Please change your password immediately after first login.
                    </p>
                    <p style="color:#64748b;font-size:13px;margin-top:24px;">
                      As Admin you can: create roles, add team members, manage invoices, expenses, clients and more.
                    </p>
                  </td></tr>
                  <tr><td style="background:#f8fafc;padding:18px 40px;text-align:center;border-top:1px solid #e5e7eb;">
                    <p style="color:#94a3b8;font-size:12px;margin:0;">© {DateTime.UtcNow.Year} Finovexa — an AllUpNext product. All rights reserved.</p>
                  </td></tr>
                </table>
              </td></tr>
            </table></body></html>
            """;
    }


}
