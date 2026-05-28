using InvoiceSaaS.Application.Common;
using InvoiceSaaS.Application.DTOs.Companies;
using System;
using System.Collections.Generic;
using System.Text;

namespace InvoiceSaaS.Application.Services.Interfaces
{
    public interface ICompanyService
    {
        Task<ServiceResult<CompanyDto>> GetByUserAsync(Guid userId, CancellationToken ct = default);
        Task<ServiceResult<CompanyDto>> UpdateAsync(UpdateCompanyDto dto, Guid updatedBy, CancellationToken ct = default);
        Task<ServiceResult<string>> UploadLogoAsync(Guid companyId, Stream fileStream, string fileName, CancellationToken ct = default);
        Task<ServiceResult> SaveTemplateAsync(Guid companyId, string template, CancellationToken ct = default);

        // ── New: SuperAdmin operations ────────────────────────
        // Full provisioning: company + admin role + admin user + FY
        Task<ServiceResult<CompanyProvisionResultDto>> ProvisionAsync(
            CreateCompanyDto dto, Guid createdBy, CancellationToken ct = default);

        // List all companies (SuperAdmin only)
        Task<ServiceResult<IEnumerable<CompanyDto>>> GetAllAsync(CancellationToken ct = default);

        // Activate / deactivate a company
        Task<ServiceResult> ToggleActiveAsync(Guid companyId, Guid updatedBy, CancellationToken ct = default);

        // First-login setup wizard
        // Saves company settings + new password + marks setup complete
        Task<ServiceResult> CompleteSetupAsync(SetupCompanyDto dto, Guid userId, CancellationToken ct = default);

        // Check if user needs to complete setup (used in DashboardController / LoginRedirect)
        Task<bool> NeedsSetupAsync(Guid userId, CancellationToken ct = default);

        Task<ServiceResult<CompanyDto>> EditAsync(EditCompanyDto dto, Guid updatedBy, CancellationToken ct = default);
        Task<ServiceResult> DeleteAsync(Guid companyId, Guid deletedBy, CancellationToken ct = default);

    }
}
