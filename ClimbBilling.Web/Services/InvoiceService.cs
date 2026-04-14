using ClimbBilling.Web.Data;
using ClimbBilling.Web.Models.Entities;
using Microsoft.EntityFrameworkCore;

namespace ClimbBilling.Web.Services;

/// <summary>Invoice creation, numbering, status management.</summary>
public class InvoiceService
{
    private readonly BillingDbContext _db;

    public InvoiceService(BillingDbContext db)
    {
        _db = db;
    }

    public async Task<string> GenerateInvoiceNumberAsync(int instructorId)
    {
        int year = DateTime.UtcNow.Year;
        int count = await _db.Invoices
            .CountAsync(i => i.InstructorId == instructorId && i.InvoiceDate.Year == year);
        return $"INV-{year}-{(count + 1):D4}";
    }

    public async Task<Invoice> CreateInvoiceAsync(
        int instructorId,
        int studentId,
        DateTime invoiceDate,
        DateTime? dueDate,
        string? notes,
        IEnumerable<InvoiceLineItem> lineItems)
    {
        var number = await GenerateInvoiceNumberAsync(instructorId);
        var invoice = new Invoice
        {
            InvoiceNumber = number,
            InstructorId = instructorId,
            StudentId = studentId,
            InvoiceDate = invoiceDate,
            DueDate = dueDate,
            Notes = notes,
            Status = InvoiceStatus.Draft,
            LineItems = lineItems.ToList()
        };
        _db.Invoices.Add(invoice);
        await _db.SaveChangesAsync();
        return invoice;
    }

    public async Task RecalculateAndSaveStatusAsync(int invoiceId)
    {
        var invoice = await _db.Invoices
            .Include(i => i.LineItems)
            .Include(i => i.Payments)
            .FirstOrDefaultAsync(i => i.Id == invoiceId);
        if (invoice == null) return;

        decimal total = invoice.LineItems.Sum(li => li.Quantity * li.UnitPrice);
        decimal paid = invoice.Payments.Where(p => p.Status == PaymentStatus.Completed).Sum(p => p.Amount);

        if (invoice.Status == InvoiceStatus.Void) { /* don't change */ }
        else if (paid >= total && total > 0) { invoice.Status = InvoiceStatus.Paid; invoice.PaidAt = DateTime.UtcNow; }
        else if (paid > 0) invoice.Status = InvoiceStatus.PartiallyPaid;
        else if (invoice.Status != InvoiceStatus.Draft) invoice.Status = InvoiceStatus.Sent;

        await _db.SaveChangesAsync();
    }
}
