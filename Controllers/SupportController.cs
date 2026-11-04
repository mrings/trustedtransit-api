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

        [HttpGet("drivers")]
        public async Task<ActionResult<IEnumerable<SupportDriverDto>>> GetDrivers()
        {
            if (!await IsSuperAdminAsync(_context))
                return Forbid();

            var drivers = await _context.Drivers
                .Include(d => d.TransportCompany)
                .OrderBy(d => d.BackgroundCheckStatus == "pending" ? 0 : 1)
                .ThenBy(d => d.FirstName).ThenBy(d => d.LastName)
                .Select(d => new SupportDriverDto
                {
                    Id = d.Id,
                    FirstName = d.FirstName,
                    LastName = d.LastName,
                    Phone = d.Phone,
                    VehicleType = d.VehicleType,
                    VehiclePlate = d.VehiclePlate,
                    Status = d.Status,
                    BackgroundCheckStatus = d.BackgroundCheckStatus,
                    BackgroundCheckDate = d.BackgroundCheckDate,
                    TransportCompanyName = d.TransportCompany != null ? d.TransportCompany.Name : null,
                    HasLogin = d.UserId != null,
                })
                .ToListAsync();

            return Ok(drivers);
        }

        // Vets a driver's background check — the only path that ever sets this; no company or
        // facility admin can self-approve their own drivers.
        [HttpPost("drivers/{id:guid}/vet")]
        public async Task<IActionResult> VetDriver(Guid id, [FromBody] VetRequest request)
        {
            if (!await IsSuperAdminAsync(_context))
                return Forbid();
            if (request.Status is not ("pending" or "approved" or "rejected"))
                return BadRequest("Status must be pending, approved, or rejected.");

            var driver = await _context.Drivers.FindAsync(id);
            if (driver == null)
                return NotFound();

            driver.BackgroundCheckStatus = request.Status;
            driver.BackgroundCheckDate = DateTime.UtcNow;
            driver.UpdatedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();

            return NoContent();
        }

        [HttpGet("companies")]
        public async Task<ActionResult<IEnumerable<SupportCompanyDto>>> GetCompanies()
        {
            if (!await IsSuperAdminAsync(_context))
                return Forbid();

            var companies = await _context.TransportCompanies
                .OrderBy(c => c.Status == "pending" ? 0 : 1)
                .ThenBy(c => c.Name)
                .Select(c => new SupportCompanyDto
                {
                    Id = c.Id,
                    Name = c.Name,
                    City = c.City,
                    State = c.State,
                    Status = c.Status,
                    DomainCount = _context.TransportCompanyDomains.Count(d => d.TransportCompanyId == c.Id),
                    DriverCount = _context.Drivers.Count(d => d.TransportCompanyId == c.Id),
                    CreatedAt = c.CreatedAt,
                })
                .ToListAsync();

            return Ok(companies);
        }

        // Vets a transport company — the only path that ever sets Status away from its "pending"
        // default.
        [HttpPost("companies/{id:guid}/vet")]
        public async Task<IActionResult> VetCompany(Guid id, [FromBody] VetRequest request)
        {
            if (!await IsSuperAdminAsync(_context))
                return Forbid();
            if (request.Status is not ("pending" or "active" or "rejected"))
                return BadRequest("Status must be pending, active, or rejected.");

            var company = await _context.TransportCompanies.FindAsync(id);
            if (company == null)
                return NotFound();

            company.Status = request.Status;
            company.UpdatedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();

            return NoContent();
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

    public class SupportDriverDto
    {
        public Guid Id { get; set; }
        public string FirstName { get; set; } = "";
        public string LastName { get; set; } = "";
        public string Phone { get; set; } = "";
        public string VehicleType { get; set; } = "";
        public string VehiclePlate { get; set; } = "";
        public string Status { get; set; } = "";
        public string BackgroundCheckStatus { get; set; } = "";
        public DateTime? BackgroundCheckDate { get; set; }
        public string? TransportCompanyName { get; set; }
        public bool HasLogin { get; set; }
    }

    public class SupportCompanyDto
    {
        public Guid Id { get; set; }
        public string Name { get; set; } = "";
        public string City { get; set; } = "";
        public string State { get; set; } = "";
        public string Status { get; set; } = "";
        public int DomainCount { get; set; }
        public int DriverCount { get; set; }
        public DateTime CreatedAt { get; set; }
    }

    public class VetRequest
    {
        public string Status { get; set; } = "";
    }
}
