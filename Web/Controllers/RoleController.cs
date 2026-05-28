using FluentValidation;
using InvoiceSaaS.Application.DTOs.Roles;
using InvoiceSaaS.Application.Services.Interfaces;
using InvoiceSaaS.Domain.Interfaces;
using InvoiceSaaS.Infrastructure.Identity;
using Microsoft.AspNetCore.Mvc;

namespace InvoiceSaaS.Web.Controllers;

public class RoleController : BaseController
{
    private readonly IRoleService _roleService;
    private readonly IValidator<CreateRoleDto> _createValidator;
    private readonly IValidator<UpdateRoleDto> _updateValidator;

    public RoleController(IRoleService roleService, IValidator<CreateRoleDto> cv,
        IValidator<UpdateRoleDto> uv, ICurrentUserService cu) : base(cu)
    { _roleService = roleService; _createValidator = cv; _updateValidator = uv; }

    [HttpGet]
    [HasPermission("roles.view")]
    public async Task<IActionResult> Index()
    {
        ViewBag.ActiveMenu = "roles";
        ViewData["Title"] = "Roles & Permissions";

        // Company-scoped: each company sees only its own roles
        var companyId = CurrentUser.IsSuperAdmin ? null : CurrentUser.CompanyId;
        var r = await _roleService.GetAllAsync(companyId);
        return View(r.Succeeded ? r.Data : Enumerable.Empty<RoleDto>());
    }

    [HttpGet]
    [HasPermission("roles.view")]
    public async Task<IActionResult> GetAll()
    {
        Guid? companyId = CurrentUser.IsSuperAdmin ? null : CurrentUser.CompanyId;
        var r = await _roleService.GetAllAsync(companyId); 
        return r.Succeeded ? AjaxOk(r.Data) : AjaxFail(r.Errors); 
    }

    [HttpGet]
    [HasPermission("roles.create")]
    public async Task<IActionResult> Create()
    {
        var p = await _roleService.GetAllPermissionsGroupedAsync();
        return PartialView("_RoleFormPartial", new RoleDetailDto { PermissionGroups = p.Data?.ToList() ?? new(), SelectedPermissionIds = new() });
    }

    //[HttpPost]
    //[HasPermission("roles.create")]
    ////[ValidateAntiForgeryToken]
    //public async Task<IActionResult> Create([FromBody] CreateRoleDto dto)
    //{

    //    var v = await _createValidator.ValidateAsync(dto);
    //    if (!v.IsValid) return AjaxFail(v.Errors.Select(e => e.ErrorMessage));
    //    var r = await _roleService.CreateAsync(dto, CurrentUser.UserId!.Value);
    //    return r.Succeeded ? AjaxOk(r.Data, r.Message) : AjaxFail(r.Errors);
    //}

    [HttpPost]
    [HasPermission("roles.create")]
    public async Task<IActionResult> Create([FromBody] CreateRoleDto dto)
    {
        // Always stamp the caller's company — company users cannot
        // create roles for other companies
        if (!CurrentUser.IsSuperAdmin)
            dto.CompanyId = CurrentUser.CompanyId;

        if (!dto.CompanyId.HasValue)
            return AjaxFail("Company context is required to create a role.");

        var v = await _createValidator.ValidateAsync(dto);
        if (!v.IsValid) return AjaxFail(v.Errors.Select(e => e.ErrorMessage));

        var r = await _roleService.CreateAsync(dto, CurrentUser.UserId!.Value);
        return r.Succeeded ? AjaxOk(r.Data, r.Message) : AjaxFail(r.Errors);
    }



    [HttpGet]
    [HasPermission("roles.edit")]
    public async Task<IActionResult> Edit(Guid id)
    {
        var r = await _roleService.GetDetailAsync(id);
        if (!r.Succeeded) return Content("<p class='text-danger p-3'>Role not found.</p>");
        return PartialView("_RoleFormPartial", r.Data);
    }

    [HttpPost]
    [HasPermission("roles.edit")]
    //[ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit([FromBody] UpdateRoleDto dto)
    {
        var v = await _updateValidator.ValidateAsync(dto);
        if (!v.IsValid) 
        return AjaxFail(v.Errors.Select(e => e.ErrorMessage));
        var r = await _roleService.UpdateAsync(dto, CurrentUser.UserId!.Value);
        return r.Succeeded ? AjaxOk(r.Data, r.Message) : AjaxFail(r.Errors);
    }

    [HttpPost]
    [HasPermission("roles.delete")]
    //[ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(Guid id)
    {
        var r = await _roleService.DeleteAsync(id, CurrentUser.UserId!.Value);
        return r.Succeeded ? AjaxOk(message: r.Message) : AjaxFail(r.Errors);
    }
}
