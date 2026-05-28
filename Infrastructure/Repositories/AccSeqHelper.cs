using Dapper;
using InvoiceSaaS.Infrastructure.Data;
using System;
using System.Collections.Generic;
using System.Data;
using System.Text;

namespace InvoiceSaaS.Infrastructure.Repositories
{
    internal static class AccSeqHelper
    {
        public static async Task<string> GetNextAsync(IDapperContext dapper, Guid companyId, string prefix)
        {
            using var conn = dapper.CreateConnection();
            var p = new DynamicParameters();
            p.Add("CompanyId", companyId);
            p.Add("Prefix", prefix);
            p.Add("Number", dbType: DbType.String, size: 30, direction: ParameterDirection.Output);
            await conn.ExecuteAsync("dbo.sp_GetNextAccountingNumber", p, commandType: CommandType.StoredProcedure);
            return p.Get<string>("Number");
        }
    }
}
