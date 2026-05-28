using InvoiceSaaS.Domain.Entities;
using System;
using System.Collections.Generic;
using System.Text;

namespace InvoiceSaaS.Domain.Interfaces
{
    public interface IExpenseRepository
    {
        Task<Expense?> GetByIdAsync(Guid id, CancellationToken ct = default);
        Task<IEnumerable<Expense>> GetByCompanyAsync(Guid companyId, ExpenseFilterDto filter, CancellationToken ct = default);
        Task AddAsync(Expense expense, CancellationToken ct = default);
        Task UpdateAsync(Expense expense, CancellationToken ct = default);
        Task DeleteAsync(Guid id, Guid deletedBy, CancellationToken ct = default);
        Task<string> GetNextNumberAsync(Guid companyId, CancellationToken ct = default);
        Task<decimal> GetTotalByCompanyAsync(Guid companyId, DateTime from, DateTime to, CancellationToken ct = default);

        Task MarkAsPaidAsync(Guid expenseId, Guid updatedBy, CancellationToken ct = default);
        Task<IEnumerable<Expense>> GetUnpaidByCompanyAsync(Guid companyId, CancellationToken ct = default);
    }
}
