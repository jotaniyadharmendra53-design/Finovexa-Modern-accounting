using InvoiceSaaS.Application.Common;
using InvoiceSaaS.Application.DTOs;
using InvoiceSaaS.Application.DTOs.Common;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace InvoiceSaaS.Application.Services.Interfaces
{
    public interface IProductService
    {
        Task<ServiceResult<IEnumerable<ProductDto>>> GetByCompanyAsync(Guid companyId, string? search = null, int page = 1, int pageSize = 20, CancellationToken ct = default);
        Task<ServiceResult<ProductDto>> GetByIdAsync(Guid id, CancellationToken ct = default);
        Task<ServiceResult<ProductDto>> SaveAsync(SaveProductDto dto, Guid companyId, Guid userId, CancellationToken ct = default);
        Task<ServiceResult> DeleteAsync(Guid id, Guid userId, CancellationToken ct = default);
        Task<ServiceResult<IEnumerable<SelectItemDto>>> GetSelectListAsync(Guid companyId, CancellationToken ct = default);
    }
}
