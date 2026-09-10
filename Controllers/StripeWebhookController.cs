using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Stripe;
using Stripe.Checkout;
using TrustedTransit.Api.Data;
using TrustedTransit.Api.Services;

namespace TrustedTransit.Api.Controllers
{
    [ApiController]
    [Route("api/stripe")]
    [AllowAnonymous]
    public class StripeWebhookController : ControllerBase
    {
        private readonly TrustedTransitDbContext _context;
        private readonly StripeBillingService _stripe;
        private readonly ILogger<StripeWebhookController> _logger;

        public StripeWebhookController(TrustedTransitDbContext context, StripeBillingService stripe, ILogger<StripeWebhookController> logger)
        {
            _context = context;
            _stripe = stripe;
            _logger = logger;
        }

        [HttpPost("webhook")]
        public async Task<IActionResult> Webhook()
        {
            if (string.IsNullOrEmpty(_stripe.WebhookSecret))
                return StatusCode(503, "Webhook secret not configured.");

            var json = await new StreamReader(Request.Body).ReadToEndAsync();
            Event stripeEvent;
            try
            {
                stripeEvent = EventUtility.ConstructEvent(
                    json, Request.Headers["Stripe-Signature"], _stripe.WebhookSecret);
            }
            catch (StripeException e)
            {
                _logger.LogWarning("Stripe webhook signature check failed: {Message}", e.Message);
                return BadRequest();
            }

            switch (stripeEvent.Type)
            {
                case "checkout.session.completed":
                {
                    var session = (Session)stripeEvent.Data.Object;
                    if (Guid.TryParse(session.ClientReferenceId, out var facilityId))
                    {
                        var facility = await _context.Facilities.FirstOrDefaultAsync(f => f.Id == facilityId);
                        if (facility != null && !string.IsNullOrEmpty(session.SubscriptionId))
                        {
                            facility.StripeCustomerId = session.CustomerId ?? facility.StripeCustomerId;
                            var sub = await new SubscriptionService().GetAsync(session.SubscriptionId);
                            StripeBillingService.Apply(facility, sub);
                            await _context.SaveChangesAsync();
                            _logger.LogInformation("Facility {FacilityId} subscribed ({SubId})", facilityId, sub.Id);
                        }
                    }
                    break;
                }
                case "customer.subscription.updated":
                case "customer.subscription.deleted":
                {
                    var sub = (Subscription)stripeEvent.Data.Object;
                    var facility = await _context.Facilities
                        .FirstOrDefaultAsync(f => f.StripeCustomerId == sub.CustomerId);
                    if (facility != null)
                    {
                        StripeBillingService.Apply(facility, sub);
                        if (stripeEvent.Type == "customer.subscription.deleted")
                            facility.SubscriptionStatus = "canceled";
                        await _context.SaveChangesAsync();
                        _logger.LogInformation("Facility {FacilityId} subscription {Type} -> {Status}",
                            facility.Id, stripeEvent.Type, facility.SubscriptionStatus);
                    }
                    break;
                }
            }

            return Ok();
        }
    }
}
