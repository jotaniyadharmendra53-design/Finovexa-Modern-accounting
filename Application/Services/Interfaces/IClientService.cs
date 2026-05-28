using InvoiceSaaS.Application.Common;
using InvoiceSaaS.Application.DTOs.Clients;
using InvoiceSaaS.Application.DTOs.Common;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace InvoiceSaaS.Application.Services.Interfaces
{
    public interface IClientService
    {
        Task<ServiceResult<ClientDto>> GetByIdAsync(Guid id, CancellationToken ct = default);
        Task<ServiceResult<IEnumerable<ClientDto>>> GetByCompanyAsync(Guid companyId, string? search = null, string? status = null, int page = 1, int pageSize = 20, CancellationToken ct = default);
        Task<ServiceResult<ClientDto>> CreateAsync(CreateClientDto dto, Guid companyId, Guid createdBy, CancellationToken ct = default);
        Task<ServiceResult<ClientDto>> UpdateAsync(UpdateClientDto dto, Guid updatedBy, CancellationToken ct = default);
        Task<ServiceResult> DeleteAsync(Guid id, Guid deletedBy, CancellationToken ct = default);
        Task<ServiceResult<IEnumerable<SelectItemDto>>> GetSelectListAsync(Guid companyId, CancellationToken ct = default);
        Task<ServiceResult<string>> GetCurrencyAsync(Guid clientId, CancellationToken ct = default);
    }
}
