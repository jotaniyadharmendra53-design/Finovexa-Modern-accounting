using System;
using System.Collections.Generic;
using System.Text;

namespace InvoiceSaaS.Application.Services.Interfaces
{
    public interface IExchangeRateService
    {
        Task<decimal> GetRateAsync(string from, string to);
    }
}
