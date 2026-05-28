using System;
using System.Collections.Generic;
using System.Text;

namespace InvoiceSaaS.Application.Common
{
    // ═══════════════════════════════════════════════════════════
    //  ServiceResult — wraps success/failure from service layer
    // ═══════════════════════════════════════════════════════════
    public class ServiceResult
    {
        public bool Succeeded { get; protected set; }
        public string? Message { get; protected set; }
        public List<string> Errors { get; protected set; } = new();

        public static ServiceResult Success(string? message = null)
            => new() { Succeeded = true, Message = message };

        public static ServiceResult Failure(string error)
            => new() { Succeeded = false, Errors = new List<string> { error } };

        public static ServiceResult Failure(IEnumerable<string> errors)
            => new() { Succeeded = false, Errors = errors.ToList() };
    }


    public class ServiceResult<T> : ServiceResult
    {
        public T? Data { get; private set; }

        public static ServiceResult<T> Success(T data, string? message = null)
            => new() { Succeeded = true, Data = data, Message = message };

        public new static ServiceResult<T> Failure(string error)
            => new() { Succeeded = false, Errors = new List<string> { error } };

        public new static ServiceResult<T> Failure(IEnumerable<string> errors)
            => new() { Succeeded = false, Errors = errors.ToList() };
    }
}
