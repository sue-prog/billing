using ClimbBilling.Web.Services;
using Microsoft.AspNetCore.Mvc;

namespace ClimbBilling.Web.Controllers;

/// <summary>
/// Receives Stripe webhook events. This endpoint must be excluded from CSRF and auth
/// middleware because Stripe calls it directly.
/// Register in Stripe Dashboard: POST https://yourdomain.com/stripe/webhook
/// </summary>
[ApiController]
[Route("stripe")]
public class StripeWebhookController : ControllerBase
{
    private readonly StripeService _stripe;
    private readonly ILogger<StripeWebhookController> _logger;

    public StripeWebhookController(StripeService stripe, ILogger<StripeWebhookController> logger)
    {
        _stripe = stripe;
        _logger = logger;
    }

    [HttpPost("webhook")]
    public async Task<IActionResult> Webhook()
    {
        string json;
        using (var reader = new StreamReader(HttpContext.Request.Body))
            json = await reader.ReadToEndAsync();

        if (!Request.Headers.TryGetValue("Stripe-Signature", out var signature))
        {
            _logger.LogWarning("Stripe webhook received without Stripe-Signature header.");
            return BadRequest("Missing Stripe-Signature.");
        }

        try
        {
            await _stripe.HandleWebhookAsync(json, signature.ToString());
            return Ok();
        }
        catch (Stripe.StripeException ex)
        {
            _logger.LogError(ex, "Stripe webhook signature validation failed.");
            return BadRequest("Webhook signature validation failed.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing Stripe webhook.");
            return StatusCode(500, "Internal error processing webhook.");
        }
    }
}
