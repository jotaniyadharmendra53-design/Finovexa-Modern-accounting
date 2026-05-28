using Dapper;
using FluentValidation;
using InvoiceSaaS.Application.DTOs;
using InvoiceSaaS.Application.DTOs.Common;
using InvoiceSaaS.Application.DTOs.Users;
using InvoiceSaaS.Application.Services.Interfaces;
using InvoiceSaaS.Domain.Interfaces;
using InvoiceSaaS.Infrastructure.Data;
using InvoiceSaaS.Infrastructure.Identity;
using Microsoft.AspNetCore.Mvc;

namespace InvoiceSaaS.Web.Controllers;

public class UserController : BaseController
{
    private readonly IUserService _us;
    private readonly IRoleService _rs;
    private readonly IValidator<CreateUserDto> _cv; private readonly IValidator<UpdateUserDto> _uv;

    public UserController(IUserService us, IRoleService rs, IValidator<CreateUserDto> cv,
        IValidator<UpdateUserDto> uv, ICurrentUserService cu) : base(cu)
    { _us = us; _rs = rs; _cv = cv; _uv = uv; }


    // ── Count for User ───────────────────────────────────────
    public static class UserCountExtension
    {
        public static async Task<int> CountByCompanyAsync(
            IDapperContext dapper, Guid? companyId)
        {
            using var conn = dapper.CreateConnection();
            return await conn.ExecuteScalarAsync<int>("""
            SELECT COUNT(DISTINCT u.Id) FROM dbo.Users u
            LEFT JOIN dbo.UserCompanies uc ON uc.UserId = u.Id
            WHERE u.IsDeleted = 0
            AND   (@CId IS NULL OR uc.CompanyId = @CId)
            """, new { CId = companyId });
        }
    }


    //[HttpGet]
    //[HasPermission("users.view")]
    //public async Task<IActionResult> Index()
    //{
    //    ViewBag.ActiveMenu = "users"; 
    //    ViewData["Title"] = "Users";

    //    var cid = CurrentUser.IsSuperAdmin ? null : CurrentUser.CompanyId;
    //    var r = await _us.GetAllAsync(cid);

    //    // Load all active roles for the role filter dropdown
    //    var roles = await _rs.GetSelectListAsync();
    //    ViewBag.Roles = roles.Data ?? Enumerable.Empty<SelectItemDto>();


    //    return View(r.Succeeded ? r.Data : Enumerable.Empty<UserListItemDto>());
    //}

    [HttpGet]
    [HasPermission("users.view")]
    public async Task<IActionResult> Index(int page = 1, int pageSize = 20)
    {
        ViewBag.ActiveMenu = "users"; ViewData["Title"] = "Users";
        Guid? companyId = CurrentUser.IsSuperAdmin ? null : CurrentUser.CompanyId;
        var r = await _us.GetAllAsync(companyId);

        var allItems = ((r.Succeeded ? r.Data : null) ?? Enumerable.Empty<UserListItemDto>()).ToList();
        var totalCount = allItems.Count;
        var paged = allItems.Skip((page - 1) * pageSize).Take(pageSize);

        ViewBag.Page = page;
        ViewBag.PageSize = pageSize;
        ViewBag.TotalCount = totalCount;
        ViewBag.TotalPages = pageSize > 0 ? (int)Math.Ceiling((double)totalCount / pageSize) : 0;

        var roles = await _rs.GetSelectListAsync(companyId);
        ViewBag.Roles = roles.Data ?? Enumerable.Empty<SelectItemDto>();
        return View(paged);
    }


    [HttpGet]
    [HasPermission("users.view")]
    public async Task<IActionResult> GetAll()
    { var r = await _us.GetAllAsync(CurrentUser.IsSuperAdmin ? null : CurrentUser.CompanyId); return r.Succeeded ? AjaxOk(r.Data) : AjaxFail(r.Errors); }

    [HttpGet]
    [HasPermission("users.create")]
    public async Task<IActionResult> Create()
    {
        var roles = await _rs.GetSelectListAsync();
        ViewBag.Roles = roles.Data ?? Enumerable.Empty<SelectItemDto>();
        return PartialView("_UserFormPartial", new CreateUserDto());
    }

    [HttpPost]
    [HasPermission("users.create")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create([FromBody] CreateUserDto dto)
    {
        // Enforce: company users can only create users for their own company
        if (!CurrentUser.IsSuperAdmin)
            dto.CompanyId = CurrentUser.CompanyId;

        if (!dto.CompanyId.HasValue)
            return AjaxFail("Company context is required to create a user.");

        var v = await _cv.ValidateAsync(dto);
        if (!v.IsValid) return AjaxFail(v.Errors.Select(e => e.ErrorMessage));

        var r = await _us.CreateAsync(dto, CurrentUser.UserId!.Value);
        return r.Succeeded ? AjaxOk(r.Data, r.Message) : AjaxFail(r.Errors);
    }



    [HttpGet]
    [HasPermission("users.edit")]
    public async Task<IActionResult> Edit(Guid id)
    {
        var ur = await _us.GetByIdAsync(id);
        if (!ur.Succeeded) 
            return Content("<p class='text-danger p-3'>User not found.</p>");
        var roles = await _rs.GetSelectListAsync();
        ViewBag.Roles = roles.Data ?? Enumerable.Empty<SelectItemDto>();
        var u = ur.Data!;
        return PartialView("_UserFormPartial", new UpdateUserDto { Id = u.Id, FullName = u.FullName, Email = u.Email, Phone = u.Phone, RoleId = u.RoleId, CompanyId = u.CompanyId, IsActive = u.IsActive });
    }

    [HttpPost]
    [HasPermission("users.edit")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit([FromBody] UpdateUserDto dto)
    {
        var v = await _uv.ValidateAsync(dto);
        if (!v.IsValid) return AjaxFail(v.Errors.Select(e => e.ErrorMessage));
        var r = await _us.UpdateAsync(dto, CurrentUser.UserId!.Value);
        return r.Succeeded ? AjaxOk(r.Data, r.Message) : AjaxFail(r.Errors);
    }

    [HttpDelete]
    [HasPermission("users.delete")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(Guid id)
    { var r = await _us.DeleteAsync(id, CurrentUser.UserId!.Value); 
        return r.Succeeded ? AjaxOk(message: r.Message) : AjaxFail(r.Errors); }

    [HttpPost]
    [HasPermission("users.edit")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ToggleActive(Guid id)
    { var r = await _us.ToggleActiveAsync(id, CurrentUser.UserId!.Value); 
        return r.Succeeded ? AjaxOk(message: r.Message) : AjaxFail(r.Errors); }
}
