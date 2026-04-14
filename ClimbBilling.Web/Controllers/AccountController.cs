using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace ClimbBilling.Web.Controllers;

/// <summary>
/// Development-only login stub. In production this is replaced by the shared
/// Climb TMS OIDC flow (configured via appsettings Authentication:Oidc).
/// </summary>
public class AccountController : Controller
{
    private readonly IWebHostEnvironment _env;

    public AccountController(IWebHostEnvironment env)
    {
        _env = env;
    }

    [HttpGet]
    public IActionResult Login(string? returnUrl = null)
    {
        ViewData["ReturnUrl"] = returnUrl;
        return View();
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Login(string email, string name, string? returnUrl = null)
    {
        // In production, authentication goes through the Climb TMS OIDC provider.
        // This stub allows development without a live IdP.
        if (!_env.IsDevelopment())
            return Forbid();

        if (string.IsNullOrEmpty(email))
        {
            ModelState.AddModelError("", "Email is required.");
            return View();
        }

        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, email),
            new(ClaimTypes.Email, email),
            new(ClaimTypes.Name, string.IsNullOrEmpty(name) ? email : name),
            new("sub", email)
        };

        var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
        await HttpContext.SignInAsync(
            CookieAuthenticationDefaults.AuthenticationScheme,
            new ClaimsPrincipal(identity));

        return LocalRedirect(returnUrl ?? "/");
    }

    [Authorize]
    public async Task<IActionResult> Logout()
    {
        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        return RedirectToAction("Login");
    }
}
