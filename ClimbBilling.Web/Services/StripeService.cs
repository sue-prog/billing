using ClimbBilling.Web.Data;
using Microsoft.EntityFrameworkCore;
using Stripe;
// Alias our domain types that share names with Stripe SDK types
using DomainInvoice = ClimbBilling.Web.Models.Entities.Invoice;
using DomainPaymentMethod = ClimbBilling.Web.Models.Entities.PaymentMethod;
using ClimbBilling.Web.Models.Entities;

namespace ClimbBilling.Web.Services;

/// <summary>
/// Wraps all Stripe operations: Connect onboarding, Payment Links, webhook handling,
/// and optional subscription billing. Uses Stripe Connect (Express accounts) so the
/// platform can collect an application_fee on each transaction.
/// </summary>
public class StripeService
{
    private readonly BillingDbContext _db;
    private readonly IConfiguration _config;
    private readonly ILogger<StripeService> _logger;

    public StripeService(BillingDbContext db, IConfiguration config, ILogger<StripeService> logger)
    {
        _db = db;
        _config = config;
        _logger = logger;
        StripeConfiguration.ApiKey = _config["Stripe:SecretKey"];
    }

    // ----------------------------------------------------------------
    // Connect Onboarding
    // ----------------------------------------------------------------

    /// <summary>
    /// Creates a Stripe Express Connect account for the instructor and returns
    /// the onboarding URL. The instructor completes KYC on Stripe's hosted page.
    /// </summary>
    public async Task<string> CreateConnectOnboardingLinkAsync(Instructor instructor, string returnUrl, string refreshUrl)
    {
        string accountId = instructor.StripeConnectAccountId ?? await CreateConnectAccountAsync(instructor);

        var linkService = new AccountLinkService();
        var link = await linkService.CreateAsync(new AccountLinkCreateOptions
        {
            Account = accountId,
            RefreshUrl = refreshUrl,
            ReturnUrl = returnUrl,
            Type = "account_onboarding",
        });

        return link.Url;
    }

    private async Task<string> CreateConnectAccountAsync(Instructor instructor)
    {
        var service = new AccountService();
        var account = await service.CreateAsync(new AccountCreateOptions
        {
            Type = "express",
            Country = "US",
            Email = instructor.Email,
            Capabilities = new AccountCapabilitiesOptions
            {
                CardPayments = new AccountCapabilitiesCardPaymentsOptions { Requested = true },
                Transfers = new AccountCapabilitiesTransfersOptions { Requested = true },
                UsBankAccountAchPayments = new AccountCapabilitiesUsBankAccountAchPaymentsOptions { Requested = true },
            },
            BusinessProfile = new AccountBusinessProfileOptions
            {
                Name = instructor.BusinessName ?? instructor.DisplayName,
                Mcc = "7999",  // Recreation services (closest for flight instruction)
                ProductDescription = "Flight instruction and aircraft rental services",
            },
            Metadata = new Dictionary<string, string>
            {
                ["climb_instructor_id"] = instructor.Id.ToString(),
                ["climb_instructor_email"] = instructor.Email
            }
        });

        instructor.StripeConnectAccountId = account.Id;
        await _db.SaveChangesAsync();
        return account.Id;
    }

    /// <summary>Checks whether the connected account has completed onboarding.</summary>
    public async Task<bool> RefreshOnboardingStatusAsync(Instructor instructor)
    {
        if (instructor.StripeConnectAccountId == null) return false;

        var service = new AccountService();
        var account = await service.GetAsync(instructor.StripeConnectAccountId);
        bool complete = account.DetailsSubmitted && !account.Requirements.CurrentlyDue.Any();

        instructor.StripeOnboardingComplete = complete;
        await _db.SaveChangesAsync();
        return complete;
    }

    // ----------------------------------------------------------------
    // Payment Links (card + ACH for students)
    // ----------------------------------------------------------------

    /// <summary>
    /// Creates a Stripe Payment Link for the given invoice. The student clicks
    /// the link, pays via card or ACH, and Stripe routes funds to the instructor
    /// minus the platform application_fee.
    /// </summary>
    public async Task<(string linkId, string linkUrl)> CreatePaymentLinkAsync(DomainInvoice invoice)
    {
        if (invoice.Instructor?.StripeConnectAccountId == null)
            throw new InvalidOperationException("Instructor has not completed Stripe onboarding.");

        var platformConfig = await _db.PlatformConfigs.FirstAsync();
        long amountCents = (long)(invoice.BalanceDue * 100);

        // Calculate platform application fee
        long appFeeCents = 0;
        if (platformConfig.PerTransactionFeeEnabled)
        {
            decimal feeAmount = invoice.BalanceDue * platformConfig.PerTransactionFeePercent
                                + platformConfig.PerTransactionFeeFixed;
            appFeeCents = (long)(feeAmount * 100);
        }

        // Create a one-time Price for this invoice amount
        var priceService = new PriceService();
        var price = await priceService.CreateAsync(new PriceCreateOptions
        {
            Currency = "usd",
            UnitAmount = amountCents,
            ProductData = new PriceProductDataOptions
            {
                Name = $"Flight Services — {invoice.InvoiceNumber}",
            },
        }, new RequestOptions { StripeAccount = invoice.Instructor.StripeConnectAccountId });

        // Create the Payment Link
        var paymentLinkService = new PaymentLinkService();
        var link = await paymentLinkService.CreateAsync(new PaymentLinkCreateOptions
        {
            LineItems = new List<PaymentLinkLineItemOptions>
            {
                new() { Price = price.Id, Quantity = 1 }
            },
            PaymentMethodTypes = new List<string> { "card", "us_bank_account" },
            Metadata = new Dictionary<string, string>
            {
                ["climb_invoice_id"] = invoice.Id.ToString(),
                ["climb_instructor_id"] = invoice.InstructorId.ToString(),
                ["climb_student_id"] = invoice.StudentId.ToString(),
            },
            ApplicationFeeAmount = appFeeCents > 0 ? appFeeCents : null,
            AfterCompletion = new PaymentLinkAfterCompletionOptions
            {
                Type = "hosted_confirmation",
                HostedConfirmation = new PaymentLinkAfterCompletionHostedConfirmationOptions
                {
                    CustomMessage = "Thank you! Your payment has been received. A receipt will be emailed to you."
                }
            }
        }, new RequestOptions { StripeAccount = invoice.Instructor.StripeConnectAccountId });

        return (link.Id, link.Url);
    }

    // ----------------------------------------------------------------
    // Subscriptions (platform funding option A)
    // ----------------------------------------------------------------

    /// <summary>
    /// Creates a Stripe Checkout session for the instructor to subscribe to the platform.
    /// </summary>
    public async Task<string> CreateSubscriptionCheckoutAsync(Instructor instructor, string successUrl, string cancelUrl)
    {
        var platformConfig = await _db.PlatformConfigs.FirstAsync();
        if (string.IsNullOrEmpty(platformConfig.StripeSubscriptionPriceId))
            throw new InvalidOperationException("Platform subscription Price ID is not configured.");

        var options = new Stripe.Checkout.SessionCreateOptions
        {
            PaymentMethodTypes = new List<string> { "card" },
            LineItems = new List<Stripe.Checkout.SessionLineItemOptions>
            {
                new() { Price = platformConfig.StripeSubscriptionPriceId, Quantity = 1 }
            },
            Mode = "subscription",
            SuccessUrl = successUrl,
            CancelUrl = cancelUrl,
            CustomerEmail = instructor.Email,
            Metadata = new Dictionary<string, string>
            {
                ["climb_instructor_id"] = instructor.Id.ToString()
            }
        };

        var service = new Stripe.Checkout.SessionService();
        var session = await service.CreateAsync(options);
        return session.Url ?? throw new InvalidOperationException("Stripe did not return a checkout URL.");
    }

    // ----------------------------------------------------------------
    // Webhook Processing
    // ----------------------------------------------------------------

    public async Task HandleWebhookAsync(string json, string stripeSignature)
    {
        var webhookSecret = _config["Stripe:WebhookSecret"] ?? string.Empty;
        Event stripeEvent;
        try
        {
            stripeEvent = EventUtility.ConstructEvent(json, stripeSignature, webhookSecret);
        }
        catch (StripeException ex)
        {
            _logger.LogWarning("Stripe webhook signature validation failed: {Message}", ex.Message);
            throw;
        }

        _logger.LogInformation("Stripe webhook received: {EventType}", stripeEvent.Type);

        switch (stripeEvent.Type)
        {
            case "payment_intent.succeeded":
                await HandlePaymentIntentSucceededAsync(stripeEvent);
                break;

            case "payment_intent.payment_failed":
                await HandlePaymentIntentFailedAsync(stripeEvent);
                break;

            case "customer.subscription.created":
            case "customer.subscription.updated":
                await HandleSubscriptionUpdatedAsync(stripeEvent);
                break;

            case "customer.subscription.deleted":
                await HandleSubscriptionDeletedAsync(stripeEvent);
                break;

            default:
                _logger.LogDebug("Unhandled Stripe event type: {EventType}", stripeEvent.Type);
                break;
        }
    }

    private async Task HandlePaymentIntentSucceededAsync(Event stripeEvent)
    {
        var paymentIntent = stripeEvent.Data.Object as PaymentIntent;
        if (paymentIntent == null) return;

        // Find the invoice via metadata
        if (!paymentIntent.Metadata.TryGetValue("climb_invoice_id", out var invoiceIdStr)
            || !int.TryParse(invoiceIdStr, out int invoiceId))
            return;

        var invoice = await _db.Invoices
            .Include(i => i.LineItems)
            .Include(i => i.Payments)
            .FirstOrDefaultAsync(i => i.Id == invoiceId);
        if (invoice == null) return;

        // Avoid duplicate processing
        if (invoice.Payments.Any(p => p.StripePaymentIntentId == paymentIntent.Id)) return;

        var platformConfig = await _db.PlatformConfigs.FirstAsync();
        decimal platformFee = 0;
        if (platformConfig.PerTransactionFeeEnabled && paymentIntent.ApplicationFeeAmount.HasValue)
            platformFee = paymentIntent.ApplicationFeeAmount.Value / 100m;

        var payment = new Payment
        {
            InvoiceId = invoiceId,
            Amount = paymentIntent.Amount / 100m,
            Method = paymentIntent.PaymentMethodTypes.Contains("us_bank_account") ? DomainPaymentMethod.ACH : DomainPaymentMethod.Card,
            Status = PaymentStatus.Completed,
            PaymentDate = DateTime.UtcNow,
            StripePaymentIntentId = paymentIntent.Id,
            PlatformFeeAmount = platformFee > 0 ? platformFee : null,
            IsManual = false
        };
        _db.Payments.Add(payment);

        // Update invoice status
        await UpdateInvoiceStatusAsync(invoice);
        await _db.SaveChangesAsync();
    }

    private async Task HandlePaymentIntentFailedAsync(Event stripeEvent)
    {
        var paymentIntent = stripeEvent.Data.Object as PaymentIntent;
        if (paymentIntent == null) return;

        if (!paymentIntent.Metadata.TryGetValue("climb_invoice_id", out var invoiceIdStr)
            || !int.TryParse(invoiceIdStr, out int invoiceId))
            return;

        var existing = await _db.Payments
            .FirstOrDefaultAsync(p => p.StripePaymentIntentId == paymentIntent.Id);
        if (existing != null)
        {
            existing.Status = PaymentStatus.Failed;
            await _db.SaveChangesAsync();
        }
    }

    private async Task HandleSubscriptionUpdatedAsync(Event stripeEvent)
    {
        var sub = stripeEvent.Data.Object as Subscription;
        if (sub == null) return;

        if (!sub.Metadata.TryGetValue("climb_instructor_id", out var idStr)
            || !int.TryParse(idStr, out int instructorId))
            return;

        var instructor = await _db.Instructors.FindAsync(instructorId);
        if (instructor == null) return;

        instructor.StripeSubscriptionId = sub.Id;
        instructor.SubscriptionStatus = sub.Status switch
        {
            "active" => SubscriptionStatus.Active,
            "past_due" => SubscriptionStatus.PastDue,
            "trialing" => SubscriptionStatus.Trialing,
            _ => SubscriptionStatus.None
        };
        instructor.SubscriptionCurrentPeriodEnd = sub.CurrentPeriodEnd;
        await _db.SaveChangesAsync();
    }

    private async Task HandleSubscriptionDeletedAsync(Event stripeEvent)
    {
        var sub = stripeEvent.Data.Object as Subscription;
        if (sub == null) return;

        var instructor = await _db.Instructors
            .FirstOrDefaultAsync(i => i.StripeSubscriptionId == sub.Id);
        if (instructor == null) return;

        instructor.SubscriptionStatus = SubscriptionStatus.Canceled;
        await _db.SaveChangesAsync();
    }

    // ----------------------------------------------------------------
    // Helpers
    // ----------------------------------------------------------------

    private static string BuildInvoiceDescription(DomainInvoice invoice)
    {
        var lines = invoice.LineItems.Select(li => $"{li.Description}: {li.Quantity:F1} hrs @ ${li.UnitPrice:F2}");
        return string.Join("; ", lines);
    }

    private static Task UpdateInvoiceStatusAsync(DomainInvoice invoice)
    {
        decimal totalPaid = invoice.Payments
            .Where(p => p.Status == PaymentStatus.Completed)
            .Sum(p => p.Amount);
        decimal total = invoice.LineItems.Sum(li => li.Quantity * li.UnitPrice);

        if (totalPaid >= total)
        {
            invoice.Status = InvoiceStatus.Paid;
            invoice.PaidAt = DateTime.UtcNow;
        }
        else if (totalPaid > 0)
        {
            invoice.Status = InvoiceStatus.PartiallyPaid;
        }

        return Task.CompletedTask;
    }
}
