using InvoiceSaaS.Domain.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace InvoiceSaaS.Web.Controllers;

[AllowAnonymous]
public class HomeController : Controller
{
    private readonly ILogger<HomeController> _logger;

    public HomeController(ILogger<HomeController> logger)
    {
        _logger = logger;
    }

    public IActionResult Index() => RedirectToAction("Index", "Dashboard");

    [Route("Home/Error")]
    public IActionResult Error()
    {
        var feature = HttpContext.Features.Get<IExceptionHandlerPathFeature>();
        if (feature?.Error != null)
            _logger.LogError(feature.Error, "Unhandled error at {Path}", feature.Path);

        ViewData["Title"] = "Error";
        return View();
    }

    [Route("Home/AccessDenied")]
    public IActionResult AccessDenied() => RedirectToAction("AccessDenied", "Account");
}
