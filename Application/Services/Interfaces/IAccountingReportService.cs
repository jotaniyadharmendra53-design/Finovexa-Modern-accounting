using InvoiceSaaS.Application.Common;
using InvoiceSaaS.Application.DTOs;
using InvoiceSaaS.Application.DTOs.Accounting;
using System;
using System.Collections.Generic;
using System.Text;

namespace InvoiceSaaS.Application.Services.Interfaces
{
    public interface IAccountingReportService
    {
        Task<ServiceResult<PnLReportDto>> GetPnLAsync(Guid companyId, AccountingFilterDto filter, CancellationToken ct = default);
        Task<ServiceResult<CashFlowDto>> GetCashFlowAsync(Guid companyId, AccountingFilterDto filter, CancellationToken ct = default);
        Task<ServiceResult<IEnumerable<AgingReportDto>>> GetReceivablesAgingAsync(Guid companyId, CancellationToken ct = default);
        Task<ServiceResult<IEnumerable<AgingReportDto>>> GetPayablesAgingAsync(Guid companyId, CancellationToken ct = default);

    }
}
