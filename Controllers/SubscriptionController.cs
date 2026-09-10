using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TrustedTransit.Api.Data;
using TrustedTransit.Api.Models;
using TrustedTransit.Api.Services;

namespace TrustedTransit.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class SubscriptionController : BaseController
    {
        private readonly TrustedTransitDbContext _context;
        private readonly StripeBillingService _stripe;
        private readonly ILogger<SubscriptionController> _logger;

        public SubscriptionController(TrustedTransitDbContext context, StripeBillingService stripe, ILogger<SubscriptionController> logger)
        {
            _context = context;
            _stripe = stripe;
            _logger = logger;
        }

        [HttpGet]
        public async Task<ActionResult<SubscriptionDto>> Get()
        {
            var me = await CurrentUserAsync(_context);
            if (me?.FacilityId == null)
                return Ok(new SubscriptionDto { Plans = PlanList() });

            var facility = await _context.Facilities.FirstAsync(f => f.Id == me.FacilityId);
            await BackfillTrialAsync(facility);

            return Ok(BuildDto(facility, me.Role == Roles.Admin, _stripe.Enabled));
        }

        // Admin: begin a paid subscription via Stripe Checkout. Returns { url } to redirect to.
        [HttpPost("checkout")]
        public async Task<ActionResult<UrlDto>> Checkout([FromBody] ChangePlanRequest request)
        {
            var (facility, error) = await GetAdminFacilityAsync();
            if (error != null) return error;
            var plan = Plans.Get(request.Tier);
            if (plan == null) return BadRequest("Unknown plan.");
            if (!_stripe.Enabled) return StatusCode(503, "Billing isn't configured.");

            var url = await _stripe.CreateCheckoutUrlAsync(facility!, plan, RequestOrigin());
            await _context.SaveChangesAsync();  // persists a newly-created StripeCustomerId
            return Ok(new UrlDto { Url = url });
        }

        // Admin: open the Stripe Billing Portal (update card, cancel, invoices).
        [HttpPost("portal")]
        public async Task<ActionResult<UrlDto>> Portal()
        {
            var (facility, error) = await GetAdminFacilityAsync();
            if (error != null) return error;
            if (!_stripe.Enabled) return StatusCode(503, "Billing isn't configured.");
            if (string.IsNullOrEmpty(facility!.StripeCustomerId))
                return BadRequest("No billing account yet — subscribe first.");

            var url = await _stripe.CreatePortalUrlAsync(facility, RequestOrigin());
            return Ok(new UrlDto { Url = url });
        }

        // Admin: change the plan of an active subscription (proration via Stripe).
        [HttpPut]
        public async Task<IActionResult> ChangePlan([FromBody] ChangePlanRequest request)
        {
            var (facility, error) = await GetAdminFacilityAsync();
            if (error != null) return error;
            var plan = Plans.Get(request.Tier);
            if (plan == null) return BadRequest("Unknown plan.");

            if (_stripe.Enabled && !string.IsNullOrEmpty(facility!.StripeSubscriptionId)
                && facility.SubscriptionStatus is "active" or "past_due")
            {
                await _stripe.UpdatePlanAsync(facility, plan);
                // Webhook will sync the tier; set it now for immediate feedback.
            }

            facility!.SubscriptionTier = plan.Key;
            facility.UpdatedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();
            _logger.LogInformation("Facility {FacilityId} plan -> {Tier}", facility.Id, plan.Key);
            return NoContent();
        }

        // Admin: cancel at period end (Stripe when connected, otherwise local).
        [HttpPost("cancel")]
        public async Task<IActionResult> Cancel()
        {
            var (facility, error) = await GetAdminFacilityAsync();
            if (error != null) return error;

            if (_stripe.Enabled && !string.IsNullOrEmpty(facility!.StripeSubscriptionId))
                await _stripe.CancelAsync(facility);

            facility!.CancelAtPeriodEnd = true;
            if (facility.SubscriptionStatus == "trial")
                facility.SubscriptionStatus = "canceled";
            facility.UpdatedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();
            _logger.LogInformation("Facility {FacilityId} subscription canceled", facility.Id);
            return NoContent();
        }

        // Admin: undo a pending cancellation.
        [HttpPost("resume")]
        public async Task<IActionResult> Resume()
        {
            var (facility, error) = await GetAdminFacilityAsync();
            if (error != null) return error;

            if (_stripe.Enabled && !string.IsNullOrEmpty(facility!.StripeSubscriptionId))
                await _stripe.ResumeAsync(facility);

            facility!.CancelAtPeriodEnd = false;
            if (facility.SubscriptionStatus == "canceled" && !string.IsNullOrEmpty(facility.StripeSubscriptionId))
                facility.SubscriptionStatus = "active";
            facility.UpdatedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();
            return NoContent();
        }

        // --- helpers ----------------------------------------------------------

        private string RequestOrigin() =>
            Request.Headers.Origin.ToString() is { Length: > 0 } o ? o.TrimEnd('/') : "https://trustedtransit-portal.vercel.app";

        private async Task<(Facility?, ActionResult?)> GetAdminFacilityAsync()
        {
            var me = await CurrentUserAsync(_context);
            if (me == null) return (null, Unauthorized());
            if (me.Role != Roles.Admin || me.FacilityId == null) return (null, StatusCode(403, "Admins only."));
            var facility = await _context.Facilities.FirstAsync(f => f.Id == me.FacilityId);
            await BackfillTrialAsync(facility);
            return (facility, null);
        }

        private async Task BackfillTrialAsync(Facility facility)
        {
            if (facility.SubscriptionStatus == "trial" && facility.TrialEndsAt == null)
            {
                facility.TrialEndsAt = facility.CreatedAt.AddDays(Plans.TrialDays);
                await _context.SaveChangesAsync();
            }
        }

        private static SubscriptionDto BuildDto(Facility f, bool isAdmin, bool billingEnabled)
        {
            var plan = Plans.Get(f.SubscriptionTier);
            int? trialDaysLeft = f.TrialEndsAt.HasValue
                ? (int)Math.Ceiling((f.TrialEndsAt.Value - DateTime.UtcNow).TotalDays)
                : null;
            return new SubscriptionDto
            {
                Tier = f.SubscriptionTier,
                PlanName = plan?.Name ?? f.SubscriptionTier,
                MonthlyPriceCents = plan?.MonthlyPriceCents ?? 0,
                Status = f.SubscriptionStatus,
                TrialEndsAt = f.TrialEndsAt,
                TrialDaysLeft = trialDaysLeft,
                RenewsAt = f.SubscriptionRenewsAt,
                CancelAtPeriodEnd = f.CancelAtPeriodEnd,
                HasStripeSubscription = !string.IsNullOrEmpty(f.StripeSubscriptionId),
                CanManage = isAdmin,
                BillingEnabled = billingEnabled,
                Plans = PlanList(),
            };
        }

        private static List<PlanDto> PlanList() =>
            Plans.All.Select(p => new PlanDto
            {
                Key = p.Key,
                Name = p.Name,
                MonthlyPriceCents = p.MonthlyPriceCents,
                Description = p.Description,
                Features = p.Features,
            }).ToList();
    }

    public class SubscriptionDto
    {
        public string Tier { get; set; } = "";
        public string PlanName { get; set; } = "";
        public int MonthlyPriceCents { get; set; }
        public string Status { get; set; } = "";
        public DateTime? TrialEndsAt { get; set; }
        public int? TrialDaysLeft { get; set; }
        public DateTime? RenewsAt { get; set; }
        public bool CancelAtPeriodEnd { get; set; }
        public bool HasStripeSubscription { get; set; }
        public bool CanManage { get; set; }
        public bool BillingEnabled { get; set; }
        public List<PlanDto> Plans { get; set; } = new();
    }

    public class PlanDto
    {
        public string Key { get; set; } = "";
        public string Name { get; set; } = "";
        public int MonthlyPriceCents { get; set; }
        public string Description { get; set; } = "";
        public string[] Features { get; set; } = System.Array.Empty<string>();
    }

    public class ChangePlanRequest
    {
        public string? Tier { get; set; }
    }

    public class UrlDto
    {
        public string Url { get; set; } = "";
    }
}
