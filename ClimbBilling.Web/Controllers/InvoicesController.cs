using ClimbBilling.Web.Data;
using ClimbBilling.Web.Models.Entities;
using ClimbBilling.Web.Models.ViewModels;
using ClimbBilling.Web.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ClimbBilling.Web.Controllers;

[Authorize]
public class InvoicesController : Controller
{
    private readonly BillingDbContext _db;
    private readonly InvoiceService _invoiceService;
    private readonly StripeService _stripe;

    public InvoicesController(BillingDbContext db, InvoiceService invoiceService, StripeService stripe)
    {
        _db = db;
        _invoiceService = invoiceService;
        _stripe = stripe;
    }

    // GET /Invoices
    public async Task<IActionResult> Index(string? status, int? year)
    {
        var instructor = await GetCurrentInstructorAsync();
        if (instructor == null) return RedirectToAction("Setup", "Instructors");

        var query = _db.Invoices
            .Include(i => i.LineItems)
            .Include(i => i.Payments)
            .Include(i => i.Student)
            .Where(i => i.InstructorId == instructor.Id);

        if (!string.IsNullOrEmpty(status) && Enum.TryParse<InvoiceStatus>(status, out var statusEnum))
            query = query.Where(i => i.Status == statusEnum);

        if (year.HasValue)
            query = query.Where(i => i.InvoiceDate.Year == year.Value);

        var invoices = await query.OrderByDescending(i => i.InvoiceDate).ToListAsync();

        var availableYears = await _db.Invoices
            .Where(i => i.InstructorId == instructor.Id)
            .Select(i => i.InvoiceDate.Year)
            .Distinct()
            .OrderByDescending(y => y)
            .ToListAsync();

        var vm = new InvoiceListViewModel
        {
            StatusFilter = status,
            YearFilter = year,
            AvailableYears = availableYears,
            Invoices = invoices.Select(i => new InvoiceSummaryRow
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

    // GET /Invoices/Create
    [HttpGet]
    public async Task<IActionResult> Create()
    {
        var instructor = await GetCurrentInstructorAsync();
        if (instructor == null) return RedirectToAction("Setup", "Instructors");

        var vm = await BuildCreateViewModelAsync(instructor);
        return View(vm);
    }

    // POST /Invoices/Create
    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(CreateInvoiceViewModel vm)
    {
        var instructor = await GetCurrentInstructorAsync();
        if (instructor == null) return RedirectToAction("Setup", "Instructors");

        // Remove items with zero quantity
        vm.LineItems = vm.LineItems.Where(li => li.Quantity > 0 && li.UnitPrice > 0).ToList();
        if (!vm.LineItems.Any())
            ModelState.AddModelError("LineItems", "At least one line item is required.");

        if (!ModelState.IsValid)
        {
            var refreshed = await BuildCreateViewModelAsync(instructor);
            vm.Students = refreshed.Students;
            vm.Rates = refreshed.Rates;
            return View(vm);
        }

        var lineItems = vm.LineItems.Select(li => new InvoiceLineItem
        {
            Description = li.Description,
            Quantity = li.Quantity,
            UnitPrice = li.UnitPrice,
            Type = li.Type,
            ServiceDate = li.ServiceDate,
            TmsReservationId = li.TmsReservationId
        });

        var invoice = await _invoiceService.CreateInvoiceAsync(
            instructor.Id, vm.StudentId, vm.InvoiceDate, vm.DueDate, vm.Notes, lineItems);

        TempData["Success"] = $"Invoice {invoice.InvoiceNumber} created.";
        return RedirectToAction("Detail", new { id = invoice.Id });
    }

    // GET /Invoices/Detail/5
    [HttpGet]
    public async Task<IActionResult> Detail(int id)
    {
        var instructor = await GetCurrentInstructorAsync();
        var invoice = await _db.Invoices
            .Include(i => i.LineItems)
            .Include(i => i.Payments)
            .Include(i => i.Student)
            .Include(i => i.Instructor)
            .FirstOrDefaultAsync(i => i.Id == id && i.InstructorId == instructor!.Id);

        if (invoice == null) return NotFound();

        var vm = new InvoiceDetailViewModel
        {
            Id = invoice.Id,
            InvoiceNumber = invoice.InvoiceNumber,
            InstructorName = invoice.Instructor.DisplayName,
            InstructorBusinessName = invoice.Instructor.BusinessName,
            InstructorEmail = invoice.Instructor.Email,
            StudentName = invoice.Student.Name,
            StudentEmail = invoice.Student.Email,
            InvoiceDate = invoice.InvoiceDate,
            DueDate = invoice.DueDate,
            Status = invoice.Status.ToString(),
            Notes = invoice.Notes,
            PaymentLinkUrl = invoice.StripePaymentLinkUrl,
            StripeConnected = invoice.Instructor.StripeOnboardingComplete,
            TotalAmount = invoice.TotalAmount,
            AmountPaid = invoice.AmountPaid,
            BalanceDue = invoice.BalanceDue,
            LineItems = invoice.LineItems.Select(li => new LineItemDetailRow
            {
                Description = li.Description,
                Type = li.Type.ToString(),
                Quantity = li.Quantity,
                UnitPrice = li.UnitPrice,
                LineTotal = li.LineTotal,
                ServiceDate = li.ServiceDate
            }).ToList(),
            Payments = invoice.Payments.OrderByDescending(p => p.PaymentDate).Select(p => new PaymentDetailRow
            {
                Id = p.Id,
                Amount = p.Amount,
                Method = p.Method.ToString(),
                Status = p.Status.ToString(),
                PaymentDate = p.PaymentDate,
                IsManual = p.IsManual,
                Notes = p.Notes
            }).ToList()
        };

        return View(vm);
    }

    // POST /Invoices/Send/5 — marks as Sent and optionally generates payment link
    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Send(int id)
    {
        var instructor = await GetCurrentInstructorAsync();
        var invoice = await _db.Invoices
            .Include(i => i.LineItems)
            .Include(i => i.Payments)
            .Include(i => i.Instructor)
            .FirstOrDefaultAsync(i => i.Id == id && i.InstructorId == instructor!.Id);

        if (invoice == null) return NotFound();
        if (invoice.Status == InvoiceStatus.Void)
        {
            TempData["Error"] = "Cannot send a voided invoice.";
            return RedirectToAction("Detail", new { id });
        }

        invoice.Status = InvoiceStatus.Sent;

        // Create payment link if instructor has Stripe connected
        if (instructor!.StripeOnboardingComplete && string.IsNullOrEmpty(invoice.StripePaymentLinkUrl))
        {
            try
            {
                var (linkId, linkUrl) = await _stripe.CreatePaymentLinkAsync(invoice);
                invoice.StripePaymentLinkId = linkId;
                invoice.StripePaymentLinkUrl = linkUrl;
            }
            catch (Exception ex)
            {
                TempData["Warning"] = $"Invoice marked sent but payment link creation failed: {ex.Message}";
            }
        }

        await _db.SaveChangesAsync();
        TempData["Success"] = "Invoice sent. Share the payment link with your student.";
        return RedirectToAction("Detail", new { id });
    }

    // POST /Invoices/Void/5
    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Void(int id)
    {
        var instructor = await GetCurrentInstructorAsync();
        var invoice = await _db.Invoices.FirstOrDefaultAsync(i => i.Id == id && i.InstructorId == instructor!.Id);
        if (invoice == null) return NotFound();

        invoice.Status = InvoiceStatus.Void;
        await _db.SaveChangesAsync();
        TempData["Success"] = "Invoice voided.";
        return RedirectToAction("Detail", new { id });
    }

    // GET /Invoices/LogPayment/5
    [HttpGet]
    public async Task<IActionResult> LogPayment(int id)
    {
        var instructor = await GetCurrentInstructorAsync();
        var invoice = await _db.Invoices
            .Include(i => i.LineItems)
            .Include(i => i.Payments)
            .FirstOrDefaultAsync(i => i.Id == id && i.InstructorId == instructor!.Id);
        if (invoice == null) return NotFound();

        return View(new LogManualPaymentViewModel
        {
            InvoiceId = id,
            InvoiceNumber = invoice.InvoiceNumber,
            BalanceDue = invoice.BalanceDue,
            Amount = invoice.BalanceDue
        });
    }

    // POST /Invoices/LogPayment
    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> LogPayment(LogManualPaymentViewModel vm)
    {
        if (!ModelState.IsValid) return View(vm);

        var instructor = await GetCurrentInstructorAsync();
        var invoice = await _db.Invoices
            .Include(i => i.LineItems)
            .Include(i => i.Payments)
            .FirstOrDefaultAsync(i => i.Id == vm.InvoiceId && i.InstructorId == instructor!.Id);
        if (invoice == null) return NotFound();

        _db.Payments.Add(new Payment
        {
            InvoiceId = vm.InvoiceId,
            Amount = vm.Amount,
            Method = vm.Method,
            Status = PaymentStatus.Completed,
            PaymentDate = vm.PaymentDate.ToUniversalTime(),
            IsManual = true,
            Notes = vm.Notes
        });

        await _db.SaveChangesAsync();
        await _invoiceService.RecalculateAndSaveStatusAsync(vm.InvoiceId);

        TempData["Success"] = $"Payment of ${vm.Amount:F2} logged.";
        return RedirectToAction("Detail", new { id = vm.InvoiceId });
    }

    private async Task<CreateInvoiceViewModel> BuildCreateViewModelAsync(Instructor instructor)
    {
        var config = await _db.PlatformConfigs.FirstOrDefaultAsync();
        return new CreateInvoiceViewModel
        {
            DueDate = DateTime.Today.AddDays(config?.DefaultPaymentDueDays ?? 30),
            Students = await _db.Students
                .Where(s => s.InstructorId == instructor.Id && s.IsActive)
                .OrderBy(s => s.Name)
                .Select(s => new StudentSelectItem { Id = s.Id, Name = s.Name, Email = s.Email })
                .ToListAsync(),
            Rates = await _db.Rates
                .Where(r => r.InstructorId == instructor.Id && r.IsActive)
                .OrderBy(r => r.Type).ThenBy(r => r.Label)
                .Select(r => new RateSelectItem { Id = r.Id, Label = r.Label, HourlyRate = r.HourlyRate, Type = r.Type.ToString() })
                .ToListAsync()
        };
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
