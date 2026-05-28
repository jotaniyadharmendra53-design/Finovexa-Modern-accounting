using InvoiceSaaS.Application.Services.Interfaces;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Text;

namespace InvoiceSaaS.Application.Services.Implementations
{
    public class ExchangeRateService : IExchangeRateService
    {
        private readonly HttpClient _http;

        public ExchangeRateService(HttpClient http)
        {
            _http = http;
        }

        public async Task<decimal> GetRateAsync(string from, string to)
        {
            var url = $"https://v6.exchangerate-api.com/v6/b142b482378754ff59d5f592/latest/{from}";

            var response = await _http.GetStringAsync(url);
            dynamic data = JsonConvert.DeserializeObject(response);

            return (decimal)data.conversion_rates[to];
        }
    }
}
