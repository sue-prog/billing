using ClimbBilling.Web.Data;
using ClimbBilling.Web.Models.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;

namespace ClimbBilling.Web.Controllers;

[Authorize]
public class StudentsController : Controller
{
    private readonly BillingDbContext _db;

    public StudentsController(BillingDbContext db)
    {
        _db = db;
    }

    public async Task<IActionResult> Index()
    {
        var instructor = await GetCurrentInstructorAsync();
        if (instructor == null) return RedirectToAction("Setup", "Instructors");

        var students = await _db.Students
            .Where(s => s.InstructorId == instructor.Id && s.IsActive)
            .OrderBy(s => s.Name)
            .Select(s => new StudentListRow
            {
                Id = s.Id,
                Name = s.Name,
                Email = s.Email,
                Phone = s.Phone,
                InvoiceCount = s.Invoices.Count,
                TotalBilled = s.Invoices.SelectMany(i => i.LineItems).Sum(li => (decimal?)li.Quantity * li.UnitPrice) ?? 0
            })
            .ToListAsync();

        return View(students);
    }

    [HttpGet]
    public IActionResult Create() => View(new StudentEditModel());

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(StudentEditModel model)
    {
        if (!ModelState.IsValid) return View(model);

        var instructor = await GetCurrentInstructorAsync();
        if (instructor == null) return RedirectToAction("Setup", "Instructors");

        _db.Students.Add(new Student
        {
            InstructorId = instructor.Id,
            Name = model.Name,
            Email = model.Email,
            Phone = model.Phone,
            Notes = model.Notes
        });
        await _db.SaveChangesAsync();
        TempData["Success"] = $"Student \"{model.Name}\" added.";
        return RedirectToAction("Index");
    }

    [HttpGet]
    public async Task<IActionResult> Edit(int id)
    {
        var instructor = await GetCurrentInstructorAsync();
        var student = await _db.Students.FirstOrDefaultAsync(s => s.Id == id && s.InstructorId == instructor!.Id);
        if (student == null) return NotFound();

        return View(new StudentEditModel
        {
            Id = id,
            Name = student.Name,
            Email = student.Email,
            Phone = student.Phone,
            Notes = student.Notes
        });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, StudentEditModel model)
    {
        if (!ModelState.IsValid) return View(model);

        var instructor = await GetCurrentInstructorAsync();
        var student = await _db.Students.FirstOrDefaultAsync(s => s.Id == id && s.InstructorId == instructor!.Id);
        if (student == null) return NotFound();

        student.Name = model.Name;
        student.Email = model.Email;
        student.Phone = model.Phone;
        student.Notes = model.Notes;
        await _db.SaveChangesAsync();

        TempData["Success"] = "Student updated.";
        return RedirectToAction("Index");
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Deactivate(int id)
    {
        var instructor = await GetCurrentInstructorAsync();
        var student = await _db.Students.FirstOrDefaultAsync(s => s.Id == id && s.InstructorId == instructor!.Id);
        if (student == null) return NotFound();

        student.IsActive = false;
        await _db.SaveChangesAsync();
        TempData["Success"] = "Student removed.";
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

public class StudentListRow
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string? Phone { get; set; }
    public int InvoiceCount { get; set; }
    public decimal TotalBilled { get; set; }
}

public class StudentEditModel
{
    public int Id { get; set; }

    [Required, StringLength(200)]
    public string Name { get; set; } = string.Empty;

    [Required, EmailAddress, StringLength(200)]
    public string Email { get; set; } = string.Empty;

    [Phone, StringLength(20)]
    public string? Phone { get; set; }

    [StringLength(500)]
    public string? Notes { get; set; }
}
