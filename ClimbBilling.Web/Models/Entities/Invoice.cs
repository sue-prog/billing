namespace ClimbBilling.Web.Models.Entities;

public class Invoice
{
    public int Id { get; set; }

    public string InvoiceNumber { get; set; } = string.Empty;  // e.g., INV-2024-0001

    public int InstructorId { get; set; }
    public Instructor Instructor { get; set; } = null!;

    public int StudentId { get; set; }
    public Student Student { get; set; } = null!;

    public DateTime InvoiceDate { get; set; } = DateTime.UtcNow;
    public DateTime? DueDate { get; set; }

    public InvoiceStatus Status { get; set; } = InvoiceStatus.Draft;

    public string? Notes { get; set; }

    // Stripe Payment Link (for card/ACH online payment)
    public string? StripePaymentLinkId { get; set; }
    public string? StripePaymentLinkUrl { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? PaidAt { get; set; }

    public ICollection<InvoiceLineItem> LineItems { get; set; } = new List<InvoiceLineItem>();
    public ICollection<Payment> Payments { get; set; } = new List<Payment>();

    public decimal TotalAmount => LineItems.Sum(li => li.LineTotal);
    public decimal AmountPaid => Payments
        .Where(p => p.Status == PaymentStatus.Completed)
        .Sum(p => p.Amount);
    public decimal BalanceDue => TotalAmount - AmountPaid;
}

public enum InvoiceStatus
{
    Draft,
    Sent,
    PartiallyPaid,
    Paid,
    Void
}
