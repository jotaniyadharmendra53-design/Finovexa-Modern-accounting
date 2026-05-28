using Dapper;
using InvoiceSaaS.Infrastructure.Data;
using System;
using System.Collections.Generic;
using System.Text;

namespace InvoiceSaaS.Infrastructure.Repositories
{
    internal static class PaginationHelper
    {
        // Builds a COUNT(*) wrapper around any SELECT query
        // by replacing the column list with COUNT(*).
        // Works for simple queries; for joins use dedicated count SQL.
        public static async Task<int> CountAsync(
            IDapperContext dapper, string countSql, object? parameters = null)
        {
            using var conn = dapper.CreateConnection();
            return await conn.ExecuteScalarAsync<int>(countSql, parameters);
        }
    }
}
