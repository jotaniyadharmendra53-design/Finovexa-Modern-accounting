using InvoiceSaaS.Application.Common;
using InvoiceSaaS.Application.DTOs;
using InvoiceSaaS.Domain.Interfaces;
using System;
using System.Collections.Generic;
using System.Text;

namespace InvoiceSaaS.Application.Services.Interfaces
{
    public interface IExpenseService
    {
        Task<ServiceResult<IEnumerable<ExpenseDto>>> GetByCompanyAsync(Guid companyId, ExpenseFilterDto filter, CancellationToken ct = default);
        Task<ServiceResult<ExpenseDto>> GetByIdAsync(Guid id, CancellationToken ct = default);
        Task<ServiceResult<ExpenseDto>> SaveAsync(SaveExpenseDto dto, Guid companyId, Guid userId, CancellationToken ct = default);
        Task<ServiceResult> DeleteAsync(Guid id, Guid userId, CancellationToken ct = default);
        Task<ServiceResult<IEnumerable<ExpenseDto>>> GetUnpaidByCompanyAsync(Guid companyId, CancellationToken ct = default);
    }
}
