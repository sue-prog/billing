namespace ClimbBilling.Web.Models.Entities;

public class Instructor
{
    public int Id { get; set; }

    /// <summary>Links to the shared Climb TMS user identity.</summary>
    public string? TmsUserId { get; set; }

    public string DisplayName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string? Phone { get; set; }
    public string? BusinessName { get; set; }

    // Stripe Connect
    public string? StripeConnectAccountId { get; set; }
    public bool StripeOnboardingComplete { get; set; }

    // Subscription (platform funding option A)
    public string? StripeSubscriptionId { get; set; }
    public SubscriptionStatus SubscriptionStatus { get; set; } = SubscriptionStatus.None;
    public DateTime? SubscriptionCurrentPeriodEnd { get; set; }

    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public ICollection<Rate> Rates { get; set; } = new List<Rate>();
    public ICollection<Student> Students { get; set; } = new List<Student>();
    public ICollection<Invoice> Invoices { get; set; } = new List<Invoice>();
}

public enum SubscriptionStatus
{
    None,
    Active,
    PastDue,
    Canceled,
    Trialing
}
