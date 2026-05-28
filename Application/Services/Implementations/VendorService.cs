using InvoiceSaaS.Application.Common;
using InvoiceSaaS.Application.DTOs;
using InvoiceSaaS.Application.DTOs.Common;
using InvoiceSaaS.Application.Services.Interfaces;
using InvoiceSaaS.Domain.Entities;
using InvoiceSaaS.Domain.Interfaces;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Text;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace InvoiceSaaS.Application.Services.Implementations
{
    public class VendorService : IVendorService
    {
        private readonly IVendorRepository _repo;
        private readonly ILogger<VendorService> _log;
        public VendorService(IVendorRepository repo, ILogger<VendorService> log) { _repo = repo; _log = log; }

        public async Task<ServiceResult<IEnumerable<VendorDto>>> GetByCompanyAsync(Guid companyId, string? search = null, int page = 1, int pageSize = 20, CancellationToken ct = default)
        {
            var items = await _repo.GetByCompanyAsync(companyId, search, ct);
            var paged = items.Skip(Math.Max(0, (page - 1) * pageSize)).Take(pageSize);
            return ServiceResult<IEnumerable<VendorDto>>.Success(paged.Select(Map));
        }

        public async Task<ServiceResult<VendorDto>> GetByIdAsync(Guid id, CancellationToken ct = default)
        {
            var v = await _repo.GetByIdAsync(id, ct);
            return v is null ? ServiceResult<VendorDto>.Failure("Vendor not found.")
                             : ServiceResult<VendorDto>.Success(Map(v));
        }

        public async Task<ServiceResult<VendorDto>> SaveAsync(SaveVendorDto dto, Guid companyId, Guid userId, CancellationToken ct = default)
        {
            try
            {
                if (dto.Id.HasValue)
                {
                    var existing = await _repo.GetByIdAsync(dto.Id.Value, ct);
                    if (existing is null) return ServiceResult<VendorDto>.Failure("Vendor not found.");
                    existing.Name = dto.Name.Trim();
                    existing.Email = dto.Email?.Trim().ToLower();
                    existing.Phone = dto.Phone?.Trim();
                    existing.Address = dto.Address?.Trim();
                    existing.City = dto.City?.Trim();
                    existing.State = dto.State?.Trim();
                    existing.Country = dto.Country?.Trim();
                    existing.PostalCode = dto.PostalCode?.Trim();
                    existing.ContactPerson = dto.ContactPerson?.Trim();
                    existing.TaxNumber = dto.TaxNumber?.Trim();
                    existing.PaymentTerms = dto.PaymentTerms?.Trim();
                    existing.Notes = dto.Notes?.Trim();
                    existing.IsActive = dto.IsActive;
                    existing.UpdatedAt = DateTime.UtcNow;
                    existing.UpdatedBy = userId;
                    await _repo.UpdateAsync(existing, ct);
                    return ServiceResult<VendorDto>.Success(Map(existing), "Vendor updated.");
                }

                var vendor = new Vendor
                {
                    CompanyId = companyId,
                    Name = dto.Name.Trim(),
                    Email = dto.Email?.Trim().ToLower(),
                    Phone = dto.Phone?.Trim(),
                    Address = dto.Address?.Trim(),
                    City = dto.City?.Trim(),
                    State = dto.State?.Trim(),
                    Country = dto.Country?.Trim(),
                    PostalCode = dto.PostalCode?.Trim(),
                    ContactPerson = dto.ContactPerson?.Trim(),
                    TaxNumber = dto.TaxNumber?.Trim(),
                    PaymentTerms = dto.PaymentTerms?.Trim(),
                    Notes = dto.Notes?.Trim(),
                    IsActive = dto.IsActive,
                    CreatedBy = userId
                };
                await _repo.AddAsync(vendor, ct);
                return ServiceResult<VendorDto>.Success(Map(vendor), "Vendor created.");
            }
            catch (Exception ex) { _log.LogError(ex, "SaveVendor"); return ServiceResult<VendorDto>.Failure($"Save failed: {ex.InnerException?.Message ?? ex.Message}"); }
        }

        public async Task<ServiceResult> DeleteAsync(Guid id, Guid userId, CancellationToken ct = default)
        {
            var v = await _repo.GetByIdAsync(id, ct);
            if (v is null) return ServiceResult.Failure("Vendor not found.");
            if (await _repo.HasExpensesAsync(id, ct))
                return ServiceResult.Failure("Cannot delete vendor with linked expenses. Remove expenses first.");
            await _repo.DeleteAsync(id, userId, ct);
            return ServiceResult.Success("Vendor deleted.");
        }

        public async Task<ServiceResult<IEnumerable<SelectItemDto>>> GetSelectListAsync(Guid companyId, CancellationToken ct = default)
        {
            var items = await _repo.GetByCompanyAsync(companyId, null, ct);
            var list = items.Where(v => v.IsActive).Select(v => new SelectItemDto { Value = v.Id.ToString(), Text = v.Name });
            return ServiceResult<IEnumerable<SelectItemDto>>.Success(list);
        }

        private static VendorDto Map(Vendor v) => new()
        {
            Id = v.Id,
            Name = v.Name,
            Email = v.Email,
            Phone = v.Phone,
            Address = v.Address,
            City = v.City,
            Country = v.Country,
            ContactPerson = v.ContactPerson,
            TaxNumber = v.TaxNumber,
            PaymentTerms = v.PaymentTerms,
            IsActive = v.IsActive,
            CreatedAt = v.CreatedAt
        };
    }
}
