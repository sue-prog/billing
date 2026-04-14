using ClimbBilling.Web.Data;
using ClimbBilling.Web.Models.Entities;
using ClimbBilling.Web.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;

namespace ClimbBilling.Web.Controllers;

[Authorize]
public class InstructorsController : Controller
{
    private readonly BillingDbContext _db;
    private readonly StripeService _stripe;

    public InstructorsController(BillingDbContext db, StripeService stripe)
    {
        _db = db;
        _stripe = stripe;
    }

    // GET /Instructors/Setup  — first-run profile creation
    [HttpGet]
    public IActionResult Setup() => View(new InstructorSetupModel());

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Setup(InstructorSetupModel model)
    {
        if (!ModelState.IsValid) return View(model);

        var instructor = new Instructor
        {
            DisplayName = model.DisplayName,
            Email = model.Email,
            Phone = model.Phone,
            BusinessName = model.BusinessName,
            TmsUserId = User.FindFirst("sub")?.Value
                        ?? User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value
        };
        _db.Instructors.Add(instructor);
        await _db.SaveChangesAsync();

        TempData["Success"] = "Profile created! Next, connect your Stripe account to accept payments.";
        return RedirectToAction("Profile");
    }

    // GET /Instructors/Profile
    [HttpGet]
    public async Task<IActionResult> Profile()
    {
        var instructor = await GetCurrentInstructorAsync();
        if (instructor == null) return RedirectToAction("Setup");

        return View(new InstructorProfileModel
        {
            Id = instructor.Id,
            DisplayName = instructor.DisplayName,
            Email = instructor.Email,
            Phone = instructor.Phone,
            BusinessName = instructor.BusinessName,
            StripeOnboardingComplete = instructor.StripeOnboardingComplete,
            StripeConnectAccountId = instructor.StripeConnectAccountId,
            SubscriptionStatus = instructor.SubscriptionStatus.ToString(),
            SubscriptionCurrentPeriodEnd = instructor.SubscriptionCurrentPeriodEnd
        });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Profile(InstructorProfileModel model)
    {
        if (!ModelState.IsValid) return View(model);

        var instructor = await GetCurrentInstructorAsync();
        if (instructor == null) return NotFound();

        instructor.DisplayName = model.DisplayName;
        instructor.Email = model.Email;
        instructor.Phone = model.Phone;
        instructor.BusinessName = model.BusinessName;
        await _db.SaveChangesAsync();

        TempData["Success"] = "Profile updated.";
        return RedirectToAction("Profile");
    }

    // GET /Instructors/StripeOnboard — start Connect onboarding
    [HttpGet]
    public async Task<IActionResult> StripeOnboard()
    {
        var instructor = await GetCurrentInstructorAsync();
        if (instructor == null) return RedirectToAction("Setup");

        var returnUrl = Url.Action("StripeOnboardReturn", "Instructors", null, Request.Scheme)!;
        var refreshUrl = Url.Action("StripeOnboard", "Instructors", null, Request.Scheme)!;

        try
        {
            var url = await _stripe.CreateConnectOnboardingLinkAsync(instructor, returnUrl, refreshUrl);
            return Redirect(url);
        }
        catch (Exception ex)
        {
            TempData["Error"] = $"Could not start Stripe onboarding: {ex.Message}";
            return RedirectToAction("Profile");
        }
    }

    // GET /Instructors/StripeOnboardReturn
    [HttpGet]
    public async Task<IActionResult> StripeOnboardReturn()
    {
        var instructor = await GetCurrentInstructorAsync();
        if (instructor == null) return RedirectToAction("Setup");

        await _stripe.RefreshOnboardingStatusAsync(instructor);

        TempData[instructor.StripeOnboardingComplete ? "Success" : "Warning"] =
            instructor.StripeOnboardingComplete
                ? "Stripe account connected successfully! You can now send payment links."
                : "Stripe onboarding is incomplete. Please complete all required fields.";

        return RedirectToAction("Profile");
    }

    // GET /Instructors/Subscribe — start subscription checkout
    [HttpGet]
    public async Task<IActionResult> Subscribe()
    {
        var instructor = await GetCurrentInstructorAsync();
        if (instructor == null) return RedirectToAction("Setup");

        var successUrl = Url.Action("Profile", "Instructors", null, Request.Scheme)!;
        var cancelUrl = Url.Action("Profile", "Instructors", null, Request.Scheme)!;

        try
        {
            var url = await _stripe.CreateSubscriptionCheckoutAsync(instructor, successUrl, cancelUrl);
            return Redirect(url);
        }
        catch (Exception ex)
        {
            TempData["Error"] = $"Could not start subscription checkout: {ex.Message}";
            return RedirectToAction("Profile");
        }
    }

    private async Task<Instructor?> GetCurrentInstructorAsync()
    {
        var userId = User.FindFirst("sub")?.Value
                     ?? User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;

        if (userId != null)
            return await _db.Instructors.FirstOrDefaultAsync(i => i.TmsUserId == userId && i.IsActive);

        return await _db.Instructors.FirstOrDefaultAsync(i => i.IsActive);
    }
}

// View models for this controller
public class InstructorSetupModel
{
    [Required, StringLength(200)]
    public string DisplayName { get; set; } = string.Empty;

    [Required, EmailAddress, StringLength(200)]
    public string Email { get; set; } = string.Empty;

    [Phone, StringLength(20)]
    public string? Phone { get; set; }

    [StringLength(200)]
    public string? BusinessName { get; set; }
}

public class InstructorProfileModel : InstructorSetupModel
{
    public int Id { get; set; }
    public bool StripeOnboardingComplete { get; set; }
    public string? StripeConnectAccountId { get; set; }
    public string SubscriptionStatus { get; set; } = string.Empty;
    public DateTime? SubscriptionCurrentPeriodEnd { get; set; }
}
