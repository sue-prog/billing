namespace ClimbBilling.Web.Models.Entities;

public class Payment
{
    public int Id { get; set; }

    public int InvoiceId { get; set; }
    public Invoice Invoice { get; set; } = null!;

    public decimal Amount { get; set; }

    public PaymentMethod Method { get; set; }

    public PaymentStatus Status { get; set; } = PaymentStatus.Pending;

    public DateTime PaymentDate { get; set; } = DateTime.UtcNow;

    // Stripe fields (populated for online payments)
    public string? StripePaymentIntentId { get; set; }
    public string? StripeChargeId { get; set; }
    public string? StripeTransferId { get; set; }

    /// <summary>Platform fee collected on this payment (per-transaction funding model).</summary>
    public decimal? PlatformFeeAmount { get; set; }

    /// <summary>True when the instructor manually recorded a cash/Venmo/Zelle payment.</summary>
    public bool IsManual { get; set; }

    public string? Notes { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

public enum PaymentMethod
{
    Card,
    ACH,
    Cash,
    Check,
    Venmo,
    Zelle,
    Other
}

public enum PaymentStatus
{
    Pending,
    Completed,
    Failed,
    Refunded
}
