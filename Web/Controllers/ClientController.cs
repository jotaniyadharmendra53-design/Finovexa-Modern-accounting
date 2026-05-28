using Dapper;
using FluentValidation;
using InvoiceSaaS.Application.DTOs;
using InvoiceSaaS.Application.DTOs.Clients;
using InvoiceSaaS.Application.Services.Interfaces;
using InvoiceSaaS.Domain.Interfaces;
using InvoiceSaaS.Infrastructure.Data;
using InvoiceSaaS.Infrastructure.Identity;
using Microsoft.AspNetCore.Mvc;
using System.Net.NetworkInformation;

namespace InvoiceSaaS.Web.Controllers;

public class ClientController : BaseController
{
    private readonly IClientService _cs;
    private readonly IValidator<CreateClientDto> _cv; private readonly IValidator<UpdateClientDto> _uv;
    private readonly IDapperContext _dapper;

    public ClientController(IClientService cs, IValidator<CreateClientDto> cv,
        IValidator<UpdateClientDto> uv, IDapperContext dapper, ICurrentUserService cu) : base(cu)
    { _cs = cs; _cv = cv; _uv = uv; _dapper = dapper; }


    public static class ClientCountExtension
    {
        public static async Task<int> CountByCompanyAsync(
            IDapperContext dapper, Guid companyId, string? search = null, string? status = null)
        {
            using var conn = dapper.CreateConnection();
            bool? isActive = status == null ? null : status == "1";

            const string sql = """
            SELECT COUNT(*) FROM dbo.Clients
            WHERE  CompanyId = @CId AND IsDeleted = 0
            AND    (@IsActive IS NULL OR IsActive = @IsActive)
            AND    (@Search IS NULL
                    OR Name  LIKE '%' + @Search + '%'
                    OR Email LIKE '%' + @Search + '%'
                    OR Phone LIKE '%' + @Search + '%')
            """;
            return await conn.ExecuteScalarAsync<int>(sql, new { CId = companyId, Search = search, IsActive = isActive });
        }
    }

    [HttpGet]
    public async Task<IActionResult> GetCurrency(Guid clientId)
    {
        var r = await _cs.GetCurrencyAsync(clientId);
        if (!r.Succeeded) 
            return AjaxFail("Client not found.");
        return AjaxOk(new { currency = r.Data });
    }


    [HttpGet]
    [HasPermission("clients.view")]
    public async Task<IActionResult> Index(string? search = null, string? status = null , int page = 1, int pageSize = 20)
    {
        ViewBag.ActiveMenu = "clients"; ViewData["Title"] = "Clients"; ViewBag.Search = search;
        if (!CurrentUser.CompanyId.HasValue)
            return View(Enumerable.Empty<ClientDto>());

        var totalCount = await ClientCountExtension.CountByCompanyAsync(
       _dapper, CurrentUser.CompanyId.Value, search, status);
        ViewBag.Page = page;
        ViewBag.PageSize = pageSize;
        ViewBag.Status = status;
        ViewBag.TotalCount = totalCount;
        ViewBag.TotalPages = pageSize > 0 ? (int)Math.Ceiling((double)totalCount / pageSize) : 0;

        var r = await _cs.GetByCompanyAsync(CurrentUser.CompanyId.Value, search, status, page, pageSize);
        return View(r.Succeeded ? r.Data : Enumerable.Empty<ClientDto>());
    }

    [HttpGet]
    [HasPermission("clients.view")]
    public async Task<IActionResult> GetAll(string? search = null)
    {
        if (!CurrentUser.CompanyId.HasValue) return AjaxOk(Array.Empty<object>());
        var r = await _cs.GetByCompanyAsync(CurrentUser.CompanyId.Value, search);
        return r.Succeeded ? AjaxOk(r.Data) : AjaxFail(r.Errors);
    }

    [HttpGet]
    public async Task<IActionResult> SelectList()
    {
        if (!CurrentUser.CompanyId.HasValue) return AjaxOk(Array.Empty<object>());
        var r = await _cs.GetSelectListAsync(CurrentUser.CompanyId.Value);
        return r.Succeeded ? AjaxOk(r.Data) : AjaxFail(r.Errors);
    }

    [HttpGet]
    [HasPermission("clients.create")]
    public IActionResult Create() => PartialView("_ClientFormPartial", new CreateClientDto());

    [HttpPost]
    [HasPermission("clients.create")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create([FromBody] CreateClientDto dto)
    {
        if (!CurrentUser.CompanyId.HasValue) return AjaxFail("Company not found.");
        var v = await _cv.ValidateAsync(dto);
        if (!v.IsValid) return AjaxFail(v.Errors.Select(e => e.ErrorMessage));
        var r = await _cs.CreateAsync(dto, CurrentUser.CompanyId.Value, CurrentUser.UserId!.Value);
        return r.Succeeded ? AjaxOk(r.Data, r.Message) : AjaxFail(r.Errors);
    }

    [HttpGet]
    [HasPermission("clients.edit")]
    public async Task<IActionResult> Edit(Guid id)
    {
        var r = await _cs.GetByIdAsync(id);
        if (!r.Succeeded) return Content("<p class='text-danger p-3'>Client not found.</p>");
        var c = r.Data!;
        return PartialView("_ClientFormPartial", new UpdateClientDto { Id = c.Id, Name = c.Name, Email = c.Email, Phone = c.Phone, Address = c.Address, City = c.City, State = c.State, Country = c.Country, PostalCode = c.PostalCode, TaxNumber = c.TaxNumber, Notes = c.Notes, IsActive = c.IsActive, CurrencyCode = c.CurrencyCode });
    }

    [HttpPost]
    [HasPermission("clients.edit")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit([FromBody] UpdateClientDto dto)
    {
        var v = await _uv.ValidateAsync(dto);
        if (!v.IsValid) return AjaxFail(v.Errors.Select(e => e.ErrorMessage));
        var r = await _cs.UpdateAsync(dto, CurrentUser.UserId!.Value);
        return r.Succeeded ? AjaxOk(r.Data, r.Message) : AjaxFail(r.Errors);
    }

    [HttpDelete]
    [HasPermission("clients.delete")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(Guid id)
    { var r = await _cs.DeleteAsync(id, CurrentUser.UserId!.Value); 
        return r.Succeeded ? AjaxOk(message: r.Message) : AjaxFail(r.Errors); }
}
