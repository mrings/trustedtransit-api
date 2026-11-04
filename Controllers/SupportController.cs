using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TrustedTransit.Api.Data;

namespace TrustedTransit.Api.Controllers
{
    /// <summary>
    /// TrustedTransit platform-staff support tooling: list every facility and "log in" as its
    /// admin regardless of subscription state. Gated by <see cref="Models.User.IsSuperAdmin"/>,
    /// which nothing in the app can set on its own — only a direct DB change.
    /// </summary>
    [ApiController]
    [Route("api/support")]
    public class SupportController : BaseController
    {
        private readonly TrustedTransitDbContext _context;

        public SupportController(TrustedTransitDbContext context)
        {
            _context = context;
        }

        [HttpGet("facilities")]
        public async Task<ActionResult<IEnumerable<SupportFacilityDto>>> GetFacilities()
        {
            if (!await IsSuperAdminAsync(_context))
                return Forbid();

            var facilities = await _context.Facilities
                .OrderBy(f => f.Name)
                .Select(f => new SupportFacilityDto
                {
                    Id = f.Id,
                    Name = f.Name,
                    City = f.City,
                    State = f.State,
                    SubscriptionTier = f.SubscriptionTier,
                    SubscriptionStatus = f.SubscriptionStatus,
                    StaffCount = _context.Users.Count(u => u.FacilityId == f.Id),
                    ResidentCount = _context.Residents.Count(r => r.FacilityId == f.Id),
                    CreatedAt = f.CreatedAt,
                })
                .ToListAsync();

            return Ok(facilities);
        }

        // Switches the caller's own FacilityId to the target facility, as its admin — this is
        // "logging in as" that facility for support purposes. Entitlement checks elsewhere
        // already bypass for IsSuperAdmin, so an expired subscription doesn't block anything.
        [HttpPost("facilities/{id:guid}/impersonate")]
        public async Task<IActionResult> Impersonate(Guid id)
        {
            if (!await IsSuperAdminAsync(_context))
                return Forbid();

            var me = (await CurrentUserAsync(_context))!;
            if (!await _context.Facilities.AnyAsync(f => f.Id == id))
                return NotFound();

            me.FacilityId = id;
            me.TransportCompanyId = null;
            me.Role = Roles.Admin;
            me.UpdatedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();

            return NoContent();
        }

        // Leaves whatever facility the caller was impersonating, back to the platform-level view.
        [HttpPost("exit")]
        public async Task<IActionResult> Exit()
        {
            if (!await IsSuperAdminAsync(_context))
                return Forbid();

            var me = (await CurrentUserAsync(_context))!;
            me.FacilityId = null;
            me.UpdatedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();

            return NoContent();
        }
    }

    public class SupportFacilityDto
    {
        public Guid Id { get; set; }
        public string Name { get; set; } = "";
        public string City { get; set; } = "";
        public string State { get; set; } = "";
        public string SubscriptionTier { get; set; } = "";
        public string SubscriptionStatus { get; set; } = "";
        public int StaffCount { get; set; }
        public int ResidentCount { get; set; }
        public DateTime CreatedAt { get; set; }
    }
}
