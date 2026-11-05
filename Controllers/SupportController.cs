using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TrustedTransit.Api.Data;
using TrustedTransit.Api.Models;
using TrustedTransit.Api.Services;

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
        private readonly NotificationService _notifications;

        public SupportController(TrustedTransitDbContext context, NotificationService notifications)
        {
            _context = context;
            _notifications = notifications;
        }

        // Every super-admin action gets a row here — nothing in this controller does anything
        // destructive or privileged without one. Written to _context but not saved; the caller
        // saves alongside its own change so both land in the same transaction.
        private void LogAction(User actor, string action, string targetType, Guid? targetId, string detail)
        {
            _context.AuditLogs.Add(new AuditLog
            {
                ActorUserId = actor.Id,
                ActorEmail = actor.Email,
                Action = action,
                TargetType = targetType,
                TargetId = targetId,
                Detail = detail,
            });
        }

        [HttpGet("audit-log")]
        public async Task<ActionResult<IEnumerable<AuditLogDto>>> GetAuditLog()
        {
            if (!await IsSuperAdminAsync(_context))
                return Forbid();

            var entries = await _context.AuditLogs
                .OrderByDescending(a => a.CreatedAt)
                .Take(200)
                .Select(a => new AuditLogDto
                {
                    Id = a.Id,
                    ActorEmail = a.ActorEmail,
                    Action = a.Action,
                    TargetType = a.TargetType,
                    TargetId = a.TargetId,
                    Detail = a.Detail,
                    CreatedAt = a.CreatedAt,
                })
                .ToListAsync();

            return Ok(entries);
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

            var me = (await CurrentUserAsync(_context))!;
            var driver = await _context.Drivers.FindAsync(id);
            if (driver == null)
                return NotFound();

            var previousStatus = driver.BackgroundCheckStatus;
            driver.BackgroundCheckStatus = request.Status;
            driver.BackgroundCheckDate = DateTime.UtcNow;
            driver.UpdatedAt = DateTime.UtcNow;

            LogAction(me, "vet_driver", "driver", driver.Id,
                $"{previousStatus} -> {request.Status} ({driver.FirstName} {driver.LastName})");
            await _context.SaveChangesAsync();

            if (previousStatus != request.Status)
                await NotifyDriverVettingAsync(driver, request.Status);

            return NoContent();
        }

        // Tells the driver their background check changed — to their login email if they have
        // one, otherwise the invite email an admin set on the placeholder row.
        private async Task NotifyDriverVettingAsync(Driver driver, string status)
        {
            if (status is not ("approved" or "rejected"))
                return;

            var email = driver.UserId != null
                ? await _context.Users.Where(u => u.Id == driver.UserId).Select(u => u.Email).FirstOrDefaultAsync()
                : driver.Email;
            if (string.IsNullOrWhiteSpace(email))
                return;

            var body = status == "approved"
                ? $"Good news, {driver.FirstName} — your TrustedTransit background check has been approved. You're now eligible to be assigned rides."
                : $"Hi {driver.FirstName}, your TrustedTransit background check wasn't approved, so you can't be assigned rides at this time. Contact your transport company for details.";

            _notifications.EnqueueEmail(email, "TrustedTransit: background check update", body + "\n\n— TrustedTransit");
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

            var me = (await CurrentUserAsync(_context))!;
            var company = await _context.TransportCompanies.FindAsync(id);
            if (company == null)
                return NotFound();

            var previousStatus = company.Status;
            company.Status = request.Status;
            company.UpdatedAt = DateTime.UtcNow;

            LogAction(me, "vet_company", "transport_company", company.Id,
                $"{previousStatus} -> {request.Status} ({company.Name})");
            await _context.SaveChangesAsync();

            if (previousStatus != request.Status)
                await NotifyCompanyVettingAsync(company, request.Status);

            return NoContent();
        }

        // Tells the company's admin their vetting status changed. A company has no single
        // "contact email" field in use today — whoever registered it (POST /api/companies) is
        // its admin, found by role, same as everywhere else in this app.
        private async Task NotifyCompanyVettingAsync(TransportCompany company, string status)
        {
            if (status is not ("active" or "rejected"))
                return;

            var email = await _context.Users
                .Where(u => u.TransportCompanyId == company.Id && u.Role == Roles.Admin)
                .Select(u => u.Email)
                .FirstOrDefaultAsync();
            if (string.IsNullOrWhiteSpace(email))
                return;

            var body = status == "active"
                ? $"Good news — {company.Name} has been approved on TrustedTransit. Your drivers can now be assigned rides once each one's own background check is approved."
                : $"{company.Name}'s TrustedTransit application wasn't approved. Contact us if you have questions.";

            _notifications.EnqueueEmail(email, "TrustedTransit: company status update", body + "\n\n— TrustedTransit");
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
            var facility = await _context.Facilities.FindAsync(id);
            if (facility == null)
                return NotFound();

            me.FacilityId = id;
            me.TransportCompanyId = null;
            me.Role = Roles.Admin;
            me.UpdatedAt = DateTime.UtcNow;

            LogAction(me, "impersonate_facility", "facility", id, facility.Name);
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
            var exitedFacilityId = me.FacilityId;
            me.FacilityId = null;
            me.UpdatedAt = DateTime.UtcNow;

            if (exitedFacilityId != null)
                LogAction(me, "exit_impersonation", "facility", exitedFacilityId, "");
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

    public class AuditLogDto
    {
        public Guid Id { get; set; }
        public string ActorEmail { get; set; } = "";
        public string Action { get; set; } = "";
        public string TargetType { get; set; } = "";
        public Guid? TargetId { get; set; }
        public string Detail { get; set; } = "";
        public DateTime CreatedAt { get; set; }
    }
}
