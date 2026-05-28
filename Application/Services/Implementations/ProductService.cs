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
    public class ProductService : IProductService
    {
        private readonly IProductRepository _repo;
        private readonly ILogger<ProductService> _log;
        public ProductService(IProductRepository repo, ILogger<ProductService> log) { _repo = repo; _log = log; }

        public async Task<ServiceResult<IEnumerable<ProductDto>>> GetByCompanyAsync(Guid companyId, string? search = null, int page = 1, int pageSize = 20, CancellationToken ct = default)
        {
            var items = await _repo.GetByCompanyAsync(companyId, search, ct);
            var paged = items.Skip(Math.Max(0, (page - 1) * pageSize)).Take(pageSize);
            return ServiceResult<IEnumerable<ProductDto>>.Success(paged.Select(Map));
        }

        public async Task<ServiceResult<ProductDto>> GetByIdAsync(Guid id, CancellationToken ct = default)
        {
            var p = await _repo.GetByIdAsync(id, ct);
            return p is null ? ServiceResult<ProductDto>.Failure("Product not found.")
                             : ServiceResult<ProductDto>.Success(Map(p));
        }

        public async Task<ServiceResult<ProductDto>> SaveAsync(SaveProductDto dto, Guid companyId, Guid userId, CancellationToken ct = default)
        {
            try
            {
                if (!string.IsNullOrEmpty(dto.SKU))
                {
                    var skuExists = await _repo.SkuExistsAsync(dto.SKU, companyId, dto.Id, ct);
                    if (skuExists) return ServiceResult<ProductDto>.Failure("A product with this SKU already exists.");
                }

                if (dto.Id.HasValue)
                {
                    var existing = await _repo.GetByIdAsync(dto.Id.Value, ct);
                    if (existing is null) return ServiceResult<ProductDto>.Failure("Product not found.");
                    existing.Name = dto.Name.Trim();
                    existing.SKU = dto.SKU?.Trim();
                    existing.Type = (ProductType)dto.Type;
                    existing.SalePrice = dto.SalePrice;
                    existing.CostPrice = dto.CostPrice;
                    existing.TaxRate = dto.TaxRate;
                    existing.Unit = dto.Unit?.Trim();
                    existing.Description = dto.Description?.Trim();
                    existing.IsActive = dto.IsActive;
                    existing.UpdatedAt = DateTime.UtcNow;
                    existing.UpdatedBy = userId;
                    await _repo.UpdateAsync(existing, ct);
                    return ServiceResult<ProductDto>.Success(Map(existing), "Product updated.");
                }

                var product = new Product
                {
                    CompanyId = companyId,
                    Name = dto.Name.Trim(),
                    SKU = dto.SKU?.Trim(),
                    Type = (ProductType)dto.Type,
                    SalePrice = dto.SalePrice,
                    CostPrice = dto.CostPrice,
                    TaxRate = dto.TaxRate,
                    Unit = dto.Unit?.Trim(),
                    Description = dto.Description?.Trim(),
                    IsActive = dto.IsActive,
                    CreatedBy = userId
                };
                await _repo.AddAsync(product, ct);
                return ServiceResult<ProductDto>.Success(Map(product), "Product created.");
            }
            catch (Exception ex) { _log.LogError(ex, "SaveProduct"); return ServiceResult<ProductDto>.Failure($"Save failed: {ex.InnerException?.Message ?? ex.Message}"); }
        }

        public async Task<ServiceResult> DeleteAsync(Guid id, Guid userId, CancellationToken ct = default)
        {
            var p = await _repo.GetByIdAsync(id, ct);
            if (p is null) return ServiceResult.Failure("Product not found.");
            await _repo.DeleteAsync(id, userId, ct);
            return ServiceResult.Success("Product deleted.");
        }

        public async Task<ServiceResult<IEnumerable<SelectItemDto>>> GetSelectListAsync(Guid companyId, CancellationToken ct = default)
        {
            var items = await _repo.GetByCompanyAsync(companyId, null, ct);
            var list = items.Where(p => p.IsActive).Select(p => new SelectItemDto
            {
                Value = p.Id.ToString(),
                Text = p.Name + (string.IsNullOrEmpty(p.SKU) ? "" : $" [{p.SKU}]"),
                Extra = p.SalePrice.ToString("F2") + "|" + p.TaxRate.ToString("F2")
            });
            return ServiceResult<IEnumerable<SelectItemDto>>.Success(list);
        }

        private static ProductDto Map(Product p) => new()
        {
            Id = p.Id,
            Name = p.Name,
            SKU = p.SKU,
            Type = (int)p.Type,
            TypeName = p.Type.ToString(),
            SalePrice = p.SalePrice,
            CostPrice = p.CostPrice,
            TaxRate = p.TaxRate,
            Unit = p.Unit,
            Description = p.Description,
            IsActive = p.IsActive,
            CreatedAt = p.CreatedAt
        };
    }
}
