using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TrustedTransit.Api.Data;
using TrustedTransit.Api.Models;

namespace TrustedTransit.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class SubscriptionController : BaseController
    {
        private readonly TrustedTransitDbContext _context;
        private readonly ILogger<SubscriptionController> _logger;

        public SubscriptionController(TrustedTransitDbContext context, ILogger<SubscriptionController> logger)
        {
            _context = context;
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

            return Ok(BuildDto(facility, me.Role == Roles.Admin));
        }

        // Admin: switch plan tier. Doesn't change trial/active status by itself.
        [HttpPut]
        public async Task<IActionResult> ChangePlan([FromBody] ChangePlanRequest request)
        {
            var (facility, error) = await GetAdminFacilityAsync();
            if (error != null) return error;
            if (!Plans.IsValid(request.Tier))
                return BadRequest("Unknown plan.");

            facility!.SubscriptionTier = request.Tier!;
            facility.UpdatedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();
            _logger.LogInformation("Facility {FacilityId} plan -> {Tier}", facility.Id, request.Tier);
            return NoContent();
        }

        // Admin: start a paid subscription (from trial or canceled).
        [HttpPost("activate")]
        public async Task<IActionResult> Activate()
        {
            var (facility, error) = await GetAdminFacilityAsync();
            if (error != null) return error;

            facility!.SubscriptionStatus = "active";
            facility.SubscriptionRenewsAt = DateTime.UtcNow.AddMonths(1);
            facility.UpdatedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();
            _logger.LogInformation("Facility {FacilityId} subscription activated", facility.Id);
            return NoContent();
        }

        // Admin: cancel. Access continues until SubscriptionRenewsAt (no hard gating yet).
        [HttpPost("cancel")]
        public async Task<IActionResult> Cancel()
        {
            var (facility, error) = await GetAdminFacilityAsync();
            if (error != null) return error;

            facility!.SubscriptionStatus = "canceled";
            facility.UpdatedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();
            _logger.LogInformation("Facility {FacilityId} subscription canceled", facility.Id);
            return NoContent();
        }

        // --- helpers ----------------------------------------------------------

        private async Task<(Facility?, IActionResult?)> GetAdminFacilityAsync()
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

        private static SubscriptionDto BuildDto(Facility f, bool isAdmin)
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
                CanManage = isAdmin,
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
        public bool CanManage { get; set; }
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
}
