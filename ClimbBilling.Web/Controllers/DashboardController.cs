using ClimbBilling.Web.Data;
using ClimbBilling.Web.Models.Entities;
using ClimbBilling.Web.Models.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ClimbBilling.Web.Controllers;

[Authorize]
public class DashboardController : Controller
{
    private readonly BillingDbContext _db;

    public DashboardController(BillingDbContext db)
    {
        _db = db;
    }

    public async Task<IActionResult> Index()
    {
        var instructor = await GetCurrentInstructorAsync();
        if (instructor == null) return RedirectToAction("Setup", "Instructors");

        var now = DateTime.UtcNow;
        var invoices = await _db.Invoices
            .Include(i => i.LineItems)
            .Include(i => i.Payments)
            .Include(i => i.Student)
            .Where(i => i.InstructorId == instructor.Id && i.Status != InvoiceStatus.Void)
            .OrderByDescending(i => i.InvoiceDate)
            .ToListAsync();

        var platformConfig = await _db.PlatformConfigs.FirstOrDefaultAsync();

        var vm = new DashboardViewModel
        {
            InstructorName = instructor.DisplayName,
            StripeConnected = instructor.StripeOnboardingComplete,
            SubscriptionActive = instructor.SubscriptionStatus == SubscriptionStatus.Active
                                 || instructor.SubscriptionStatus == SubscriptionStatus.Trialing,

            OutstandingBalance = invoices
                .Where(i => i.Status is InvoiceStatus.Sent or InvoiceStatus.PartiallyPaid)
                .Sum(i => i.BalanceDue),

            PaidThisMonth = invoices
                .SelectMany(i => i.Payments)
                .Where(p => p.Status == PaymentStatus.Completed
                            && p.PaymentDate.Year == now.Year
                            && p.PaymentDate.Month == now.Month)
                .Sum(p => p.Amount),

            PaidThisYear = invoices
                .SelectMany(i => i.Payments)
                .Where(p => p.Status == PaymentStatus.Completed && p.PaymentDate.Year == now.Year)
                .Sum(p => p.Amount),

            OpenInvoiceCount = invoices.Count(i => i.Status is InvoiceStatus.Sent or InvoiceStatus.PartiallyPaid),

            OverdueInvoiceCount = invoices.Count(i =>
                i.Status is InvoiceStatus.Sent or InvoiceStatus.PartiallyPaid
                && i.DueDate.HasValue && i.DueDate.Value < DateTime.Today),

            RecentInvoices = invoices.Take(10).Select(i => new InvoiceSummaryRow
            {
                Id = i.Id,
                InvoiceNumber = i.InvoiceNumber,
                StudentName = i.Student.Name,
                InvoiceDate = i.InvoiceDate,
                DueDate = i.DueDate,
                TotalAmount = i.TotalAmount,
                BalanceDue = i.BalanceDue,
                Status = i.Status.ToString()
            }).ToList()
        };

        return View(vm);
    }

    private async Task<Instructor?> GetCurrentInstructorAsync()
    {
        // In production: resolve from the shared TMS identity claim
        // For now: return the first active instructor (demo mode)
        var userId = User.FindFirst("sub")?.Value
                     ?? User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;

        if (userId != null)
            return await _db.Instructors.FirstOrDefaultAsync(i => i.TmsUserId == userId && i.IsActive);

        // Demo fallback — first instructor in DB
        return await _db.Instructors.FirstOrDefaultAsync(i => i.IsActive);
    }
}
