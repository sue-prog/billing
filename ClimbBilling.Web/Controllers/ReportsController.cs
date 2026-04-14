using ClimbBilling.Web.Data;
using ClimbBilling.Web.Models.Entities;
using ClimbBilling.Web.Models.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ClimbBilling.Web.Controllers;

[Authorize]
public class ReportsController : Controller
{
    private readonly BillingDbContext _db;

    public ReportsController(BillingDbContext db)
    {
        _db = db;
    }

    // GET /Reports/Monthly?year=2024&month=3
    public async Task<IActionResult> Monthly(int? year, int? month)
    {
        var instructor = await GetCurrentInstructorAsync();
        if (instructor == null) return RedirectToAction("Setup", "Instructors");

        var now = DateTime.UtcNow;
        int selectedYear = year ?? now.Year;
        int selectedMonth = month ?? now.Month;

        var availableYears = await _db.Invoices
            .Where(i => i.InstructorId == instructor.Id)
            .Select(i => i.InvoiceDate.Year)
            .Distinct()
            .OrderByDescending(y => y)
            .ToListAsync();

        if (!availableYears.Any()) availableYears.Add(now.Year);

        var invoices = await _db.Invoices
            .Include(i => i.LineItems)
            .Include(i => i.Payments)
            .Include(i => i.Student)
            .Where(i => i.InstructorId == instructor.Id
                        && i.InvoiceDate.Year == selectedYear
                        && i.InvoiceDate.Month == selectedMonth
                        && i.Status != InvoiceStatus.Void)
            .OrderBy(i => i.InvoiceDate)
            .ToListAsync();

        decimal TotalInvoiced = invoices.Sum(i => i.TotalAmount);
        decimal TotalCollected = invoices.SelectMany(i => i.Payments)
            .Where(p => p.Status == PaymentStatus.Completed)
            .Sum(p => p.Amount);

        decimal InstructionRevenue = invoices
            .SelectMany(i => i.LineItems)
            .Where(li => li.Type is LineItemType.Instruction or LineItemType.GroundInstruction)
            .Sum(li => li.LineTotal);

        decimal AircraftRentalRevenue = invoices
            .SelectMany(i => i.LineItems)
            .Where(li => li.Type is LineItemType.AircraftRental or LineItemType.SimulatorRental)
            .Sum(li => li.LineTotal);

        decimal OtherRevenue = invoices
            .SelectMany(i => i.LineItems)
            .Where(li => li.Type is LineItemType.Other or LineItemType.Discount)
            .Sum(li => li.LineTotal);

        var payments = invoices.SelectMany(i => i.Payments).Where(p => p.Status == PaymentStatus.Completed).ToList();

        var vm = new MonthlyReportViewModel
        {
            Year = selectedYear,
            Month = selectedMonth,
            SelectedYear = selectedYear,
            SelectedMonth = selectedMonth,
            AvailableYears = availableYears,

            TotalInvoiced = TotalInvoiced,
            TotalCollected = TotalCollected,
            TotalOutstanding = TotalInvoiced - TotalCollected,
            PlatformFeesCharged = invoices.SelectMany(i => i.Payments).Sum(p => p.PlatformFeeAmount ?? 0),

            InstructionRevenue = InstructionRevenue,
            AircraftRentalRevenue = AircraftRentalRevenue,
            OtherRevenue = OtherRevenue,

            CollectedByCard = payments.Where(p => p.Method == PaymentMethod.Card).Sum(p => p.Amount),
            CollectedByACH = payments.Where(p => p.Method == PaymentMethod.ACH).Sum(p => p.Amount),
            CollectedByManual = payments.Where(p => p.IsManual).Sum(p => p.Amount),

            InvoiceCount = invoices.Count,
            PaidInvoiceCount = invoices.Count(i => i.Status == InvoiceStatus.Paid),
            StudentCount = invoices.Select(i => i.StudentId).Distinct().Count(),

            Invoices = invoices.Select(i => new MonthlyInvoiceRow
            {
                InvoiceNumber = i.InvoiceNumber,
                StudentName = i.Student.Name,
                InvoiceDate = i.InvoiceDate,
                TotalAmount = i.TotalAmount,
                AmountPaid = i.AmountPaid,
                Status = i.Status.ToString()
            }).ToList()
        };

        return View(vm);
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
