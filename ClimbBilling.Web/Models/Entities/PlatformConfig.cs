namespace ClimbBilling.Web.Models.Entities;

/// <summary>
/// Single-row table. Controls platform-wide monetization settings.
/// The platform can charge instructors via subscription, per-transaction fee, or both.
/// </summary>
public class PlatformConfig
{
    public int Id { get; set; }

    // --- Option A: Subscription ---
    public bool SubscriptionEnabled { get; set; }
    public decimal SubscriptionMonthlyPrice { get; set; } = 9.99m;
    public string? StripeSubscriptionProductId { get; set; }
    public string? StripeSubscriptionPriceId { get; set; }

    // --- Option B: Per-transaction platform fee ---
    public bool PerTransactionFeeEnabled { get; set; }
    /// <summary>Percentage as a decimal fraction, e.g., 0.005 = 0.5%.</summary>
    public decimal PerTransactionFeePercent { get; set; } = 0.005m;
    /// <summary>Optional fixed fee per transaction in addition to the percentage.</summary>
    public decimal PerTransactionFeeFixed { get; set; } = 0.00m;

    // --- Stripe Platform Settings ---
    public string? StripePlatformPublishableKey { get; set; }
    public string? StripeWebhookSecret { get; set; }

    // --- Defaults for new instructors ---
    public int DefaultPaymentDueDays { get; set; } = 30;
    public string? DefaultInvoiceFooterText { get; set; }

    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
