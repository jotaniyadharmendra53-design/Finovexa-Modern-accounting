using System;
using System.Collections.Generic;
using System.Text;

namespace InvoiceSaaS.Application.Common
{
    // ═══════════════════════════════════════════════════════════
    //  ApiResponse — standard JSON response sent to jQuery/Ajax
    // ═══════════════════════════════════════════════════════════
    public class ApiResponse
    {
        public bool Success { get; set; }
        public string? Message { get; set; }
        public object? Data { get; set; }
        public List<string> Errors { get; set; } = new();

        public static ApiResponse Ok(object? data = null, string? message = null)
            => new() { Success = true, Data = data, Message = message };

        public static ApiResponse Fail(string error)
            => new() { Success = false, Errors = new List<string> { error } };

        public static ApiResponse Fail(IEnumerable<string> errors)
            => new() { Success = false, Errors = errors.ToList() };

        public static ApiResponse From(ServiceResult result, object? data = null)
            => new()
            {
                Success = result.Succeeded,
                Message = result.Message,
                Data = data,
                Errors = result.Errors
            };
    }
}
