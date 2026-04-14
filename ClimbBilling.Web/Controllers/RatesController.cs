using ClimbBilling.Web.Data;
using ClimbBilling.Web.Models.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;

namespace ClimbBilling.Web.Controllers;

[Authorize]
public class RatesController : Controller
{
    private readonly BillingDbContext _db;

    public RatesController(BillingDbContext db)
    {
        _db = db;
    }

    public async Task<IActionResult> Index()
    {
        var instructor = await GetCurrentInstructorAsync();
        if (instructor == null) return RedirectToAction("Setup", "Instructors");

        var rates = await _db.Rates
            .Where(r => r.InstructorId == instructor.Id)
            .OrderBy(r => r.Type).ThenBy(r => r.Label)
            .ToListAsync();

        return View(rates);
    }

    [HttpGet]
    public IActionResult Create() => View(new RateEditModel());

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(RateEditModel model)
    {
        if (!ModelState.IsValid) return View(model);

        var instructor = await GetCurrentInstructorAsync();
        if (instructor == null) return RedirectToAction("Setup", "Instructors");

        _db.Rates.Add(new Rate
        {
            InstructorId = instructor.Id,
            Type = model.Type,
            Label = model.Label,
            HourlyRate = model.HourlyRate
        });
        await _db.SaveChangesAsync();
        TempData["Success"] = $"Rate \"{model.Label}\" added.";
        return RedirectToAction("Index");
    }

    [HttpGet]
    public async Task<IActionResult> Edit(int id)
    {
        var instructor = await GetCurrentInstructorAsync();
        var rate = await _db.Rates.FirstOrDefaultAsync(r => r.Id == id && r.InstructorId == instructor!.Id);
        if (rate == null) return NotFound();

        return View(new RateEditModel
        {
            Id = id,
            Type = rate.Type,
            Label = rate.Label,
            HourlyRate = rate.HourlyRate,
            IsActive = rate.IsActive
        });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, RateEditModel model)
    {
        if (!ModelState.IsValid) return View(model);

        var instructor = await GetCurrentInstructorAsync();
        var rate = await _db.Rates.FirstOrDefaultAsync(r => r.Id == id && r.InstructorId == instructor!.Id);
        if (rate == null) return NotFound();

        rate.Type = model.Type;
        rate.Label = model.Label;
        rate.HourlyRate = model.HourlyRate;
        rate.IsActive = model.IsActive;
        await _db.SaveChangesAsync();

        TempData["Success"] = "Rate updated.";
        return RedirectToAction("Index");
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id)
    {
        var instructor = await GetCurrentInstructorAsync();
        var rate = await _db.Rates.FirstOrDefaultAsync(r => r.Id == id && r.InstructorId == instructor!.Id);
        if (rate == null) return NotFound();

        rate.IsActive = false;  // soft delete
        await _db.SaveChangesAsync();
        TempData["Success"] = "Rate removed.";
        return RedirectToAction("Index");
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

public class RateEditModel
{
    public int Id { get; set; }

    [Required]
    public RateType Type { get; set; } = RateType.Instruction;

    [Required, StringLength(200)]
    public string Label { get; set; } = string.Empty;

    [Required, Range(0.01, 99999)]
    [Display(Name = "Hourly Rate ($)")]
    public decimal HourlyRate { get; set; }

    public bool IsActive { get; set; } = true;
}
