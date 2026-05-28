using InvoiceSaaS.Application.Common;
using InvoiceSaaS.Application.DTOs.Dashboard;
using System;
using System.Collections.Generic;
using System.Text;

namespace InvoiceSaaS.Application.Services.Interfaces
{
    public interface IDashboardService
    {
        Task<ServiceResult<DashboardStatsDto>> GetStatsAsync(Guid? companyId, CancellationToken ct = default);

        // SuperAdmin platform dashboard (new)
        Task<ServiceResult<SuperAdminStatsDto>> GetSuperAdminStatsAsync(
            CancellationToken ct = default);
    }
}
