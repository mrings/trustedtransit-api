using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TrustedTransit.Api.Data;
using TrustedTransit.Api.Models;

namespace TrustedTransit.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class FacilitiesController : BaseController
    {
        private readonly TrustedTransitDbContext _context;
        private readonly ILogger<FacilitiesController> _logger;

        public FacilitiesController(TrustedTransitDbContext context, ILogger<FacilitiesController> logger)
        {
            _context = context;
            _logger = logger;
        }

        // Your own facility if you're linked to one; otherwise the facilities with no
        // members yet (the ones you can claim via POST /users/me/facility).
        [HttpGet]
        public async Task<ActionResult<IEnumerable<FacilityDto>>> GetFacilities()
        {
            var myFacilityId = await CurrentFacilityIdAsync(_context);

            IQueryable<Facility> q;
            if (myFacilityId != null)
                q = _context.Facilities.Where(f => f.Id == myFacilityId);
            else
                q = _context.Facilities.Where(f => !_context.Users.Any(u => u.FacilityId == f.Id));

            var facilities = await q
                .Select(f => new FacilityDto
                {
                    Id = f.Id,
                    Name = f.Name,
                    Address = f.Address,
                    City = f.City,
                    State = f.State,
                    Phone = f.Phone,
                    SubscriptionTier = f.SubscriptionTier,
                    SubscriptionStatus = f.SubscriptionStatus
                })
                .ToListAsync();

            return Ok(facilities);
        }

        // Your own facility only.
        [HttpGet("{id:guid}")]
        public async Task<ActionResult<FacilityDetailDto>> GetFacility(Guid id)
        {
            var myFacilityId = await CurrentFacilityIdAsync(_context);
            if (myFacilityId != id)
                return Forbid();

            var facility = await _context.Facilities.FirstOrDefaultAsync(f => f.Id == id);
            if (facility == null)
                return NotFound();

            var domains = await _context.FacilityDomains
                .Where(d => d.FacilityId == id)
                .OrderBy(d => d.CreatedAt)
                .Select(d => d.Domain)
                .ToListAsync();

            return Ok(new FacilityDetailDto
            {
                Id = facility.Id,
                Name = facility.Name,
                Address = facility.Address,
                City = facility.City,
                State = facility.State,
                Zip = facility.Zip,
                Phone = facility.Phone,
                Domains = domains,
                SubscriptionTier = facility.SubscriptionTier,
                SubscriptionStatus = facility.SubscriptionStatus
            });
        }

        // Sign-up: an authenticated user with no facility creates one and becomes its admin.
        [HttpPost]
        public async Task<ActionResult<FacilityDto>> CreateFacility([FromBody] CreateFacilityRequest request)
        {
            var me = await CurrentUserAsync(_context);
            if (me == null)
                return Unauthorized();
            if (me.FacilityId != null)
                return BadRequest("Your account is already linked to a facility.");

            var facility = new Facility
            {
                Name = request.Name ?? string.Empty,
                Address = request.Address ?? string.Empty,
                City = request.City ?? string.Empty,
                State = request.State ?? string.Empty,
                Zip = request.Zip ?? string.Empty,
                Phone = request.Phone ?? string.Empty,
                SubscriptionTier = "starter",
                SubscriptionStatus = "trial"
            };

            _context.Facilities.Add(facility);
            await AssignFacilityAsync(_context, me, facility.Id);
            await _context.SaveChangesAsync();

            _logger.LogInformation("Facility {FacilityId} created by {UserId}", facility.Id, me.Id);

            return CreatedAtAction(nameof(GetFacility), new { id = facility.Id }, new FacilityDto
            {
                Id = facility.Id,
                Name = facility.Name
            });
        }

        // Admin of this facility only.
        [HttpPatch("{id:guid}")]
        public async Task<IActionResult> UpdateFacility(Guid id, [FromBody] UpdateFacilityRequest request)
        {
            var me = await CurrentUserAsync(_context);
            if (me == null)
                return Unauthorized();
            if (me.Role != Roles.Admin || me.FacilityId != id)
                return Forbid();

            var facility = await _context.Facilities.FindAsync(id);
            if (facility == null)
                return NotFound();

            facility.Name = request.Name ?? facility.Name;
            facility.Address = request.Address ?? facility.Address;
            facility.City = request.City ?? facility.City;
            facility.State = request.State ?? facility.State;
            facility.Zip = request.Zip ?? facility.Zip;
            facility.Phone = request.Phone ?? facility.Phone;
            facility.SubscriptionTier = request.SubscriptionTier ?? facility.SubscriptionTier;
            facility.UpdatedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();
            _logger.LogInformation("Facility {FacilityId} updated", id);

            return NoContent();
        }

        // Your facility's verified staff email domains.
        [HttpGet("{id:guid}/domains")]
        public async Task<ActionResult<IEnumerable<FacilityDomainDto>>> GetDomains(Guid id)
        {
            var myFacilityId = await CurrentFacilityIdAsync(_context);
            if (myFacilityId != id)
                return Forbid();

            var domains = await _context.FacilityDomains
                .Where(d => d.FacilityId == id)
                .OrderBy(d => d.CreatedAt)
                .Select(d => new FacilityDomainDto
                {
                    Id = d.Id,
                    Domain = d.Domain,
                    AddedByEmail = d.AddedByEmail,
                    CreatedAt = d.CreatedAt
                })
                .ToListAsync();

            return Ok(domains);
        }

        /// <summary>
        /// Admin: claim a domain for this facility. Ownership proof is that the admin is
        /// currently signed in with a verified email at that exact domain — so only someone
        /// who actually holds an address there (i.e. the business, not a stranger) can claim it.
        /// </summary>
        [HttpPost("{id:guid}/domains")]
        public async Task<ActionResult<FacilityDomainDto>> AddDomain(Guid id, [FromBody] AddFacilityDomainRequest request)
        {
            var me = await CurrentUserAsync(_context);
            if (me == null)
                return Unauthorized();
            if (me.Role != Roles.Admin || me.FacilityId != id)
                return Forbid();

            var domain = NormalizeEmailDomain(request.Domain);
            if (domain == null)
                return BadRequest("Enter a domain, e.g. yourcompany.com.");
            if (IsConsumerEmailDomain(domain))
                return BadRequest("Personal email providers (gmail.com, etc.) can't be used.");

            if (string.IsNullOrEmpty(me.Email) || !IsCurrentEmailVerified())
                return BadRequest("Your email isn't verified — sign out and back in, then try again.");

            var myDomain = EmailDomainOf(me.Email);
            if (!string.Equals(myDomain, domain, StringComparison.OrdinalIgnoreCase))
                return BadRequest(
                    $"To claim \"{domain}\", you must be signed in with a verified email at that domain " +
                    $"(you're signed in as {me.Email}). This proves your organization owns it.");

            if (await _context.FacilityDomains.AnyAsync(d => d.Domain == domain))
                return BadRequest("That domain is already registered to a facility.");

            var facilityDomain = new FacilityDomain { FacilityId = id, Domain = domain, AddedByEmail = me.Email };
            _context.FacilityDomains.Add(facilityDomain);
            await _context.SaveChangesAsync();
            _logger.LogInformation("Facility {FacilityId} claimed domain {Domain}", id, domain);

            return Ok(new FacilityDomainDto
            {
                Id = facilityDomain.Id,
                Domain = facilityDomain.Domain,
                AddedByEmail = facilityDomain.AddedByEmail,
                CreatedAt = facilityDomain.CreatedAt
            });
        }

        // Admin: stop auto-joining staff at this domain.
        [HttpDelete("{id:guid}/domains/{domainId:guid}")]
        public async Task<IActionResult> RemoveDomain(Guid id, Guid domainId)
        {
            var me = await CurrentUserAsync(_context);
            if (me == null)
                return Unauthorized();
            if (me.Role != Roles.Admin || me.FacilityId != id)
                return Forbid();

            var domain = await _context.FacilityDomains.FirstOrDefaultAsync(d => d.Id == domainId && d.FacilityId == id);
            if (domain == null)
                return NotFound();

            _context.FacilityDomains.Remove(domain);
            await _context.SaveChangesAsync();
            _logger.LogInformation("Facility {FacilityId} removed domain {Domain}", id, domain.Domain);

            return NoContent();
        }
    }

    public class FacilityDto
    {
        public Guid Id { get; set; }
        public string Name { get; set; }
        public string Address { get; set; }
        public string City { get; set; }
        public string State { get; set; }
        public string Phone { get; set; }
        public string SubscriptionTier { get; set; }
        public string SubscriptionStatus { get; set; }
    }

    public class FacilityDetailDto
    {
        public Guid Id { get; set; }
        public string Name { get; set; }
        public string Address { get; set; }
        public string City { get; set; }
        public string State { get; set; }
        public string Zip { get; set; }
        public string Phone { get; set; }
        public List<string> Domains { get; set; } = new();
        public string SubscriptionTier { get; set; }
        public string SubscriptionStatus { get; set; }
    }

    public class FacilityDomainDto
    {
        public Guid Id { get; set; }
        public string Domain { get; set; } = "";
        public string AddedByEmail { get; set; } = "";
        public DateTime CreatedAt { get; set; }
    }

    public class CreateFacilityRequest
    {
        public string? Name { get; set; }
        public string? Address { get; set; }
        public string? City { get; set; }
        public string? State { get; set; }
        public string? Zip { get; set; }
        public string? Phone { get; set; }
    }

    public class UpdateFacilityRequest
    {
        public string? Name { get; set; }
        public string? Address { get; set; }
        public string? City { get; set; }
        public string? State { get; set; }
        public string? Zip { get; set; }
        public string? Phone { get; set; }
        public string? SubscriptionTier { get; set; }
    }

    public class AddFacilityDomainRequest
    {
        public string? Domain { get; set; }
    }
}
