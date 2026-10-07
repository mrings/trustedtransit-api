using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TrustedTransit.Api.Data;
using TrustedTransit.Api.Models;

namespace TrustedTransit.Api.Controllers
{
    /// <summary>
    /// NEMT transportation companies — a separate org type from Facility. A company has its own
    /// admin and its own driver roster; it doesn't subscribe/pay TrustedTransit (no billing here).
    /// </summary>
    [ApiController]
    [Route("api/companies")]
    public class TransportCompaniesController : BaseController
    {
        private readonly TrustedTransitDbContext _context;
        private readonly ILogger<TransportCompaniesController> _logger;

        public TransportCompaniesController(TrustedTransitDbContext context, ILogger<TransportCompaniesController> logger)
        {
            _context = context;
            _logger = logger;
        }

        // Your own company only.
        [HttpGet("{id:guid}")]
        public async Task<ActionResult<TransportCompanyDetailDto>> GetCompany(Guid id)
        {
            var myCompanyId = await CurrentTransportCompanyIdAsync(_context);
            if (myCompanyId != id)
                return Forbid();

            var company = await _context.TransportCompanies.FirstOrDefaultAsync(c => c.Id == id);
            if (company == null)
                return NotFound();

            var domains = await _context.TransportCompanyDomains
                .Where(d => d.TransportCompanyId == id)
                .OrderBy(d => d.CreatedAt)
                .Select(d => d.Domain)
                .ToListAsync();

            return Ok(new TransportCompanyDetailDto
            {
                Id = company.Id,
                Name = company.Name,
                Address = company.Address,
                City = company.City,
                State = company.State,
                Zip = company.Zip,
                Phone = company.Phone,
                Status = company.Status,
                Domains = domains
            });
        }

        // Sign-up: an authenticated user with no facility and no company creates one and
        // becomes its admin. (Mutually exclusive with facility staff — one account, one org.)
        [HttpPost]
        public async Task<ActionResult<TransportCompanyDetailDto>> CreateCompany([FromBody] CreateTransportCompanyRequest request)
        {
            var me = await CurrentUserAsync(_context);
            if (me == null)
                return Unauthorized();
            if (me.FacilityId != null)
                return BadRequest("Your account is linked to a facility and can't also run a transport company.");
            if (me.TransportCompanyId != null)
                return BadRequest("Your account is already linked to a transport company.");
            if (string.IsNullOrWhiteSpace(request.Name))
                return BadRequest("Enter a company name.");

            var company = new TransportCompany
            {
                Name = request.Name!.Trim(),
                Address = request.Address ?? string.Empty,
                City = request.City ?? string.Empty,
                State = request.State ?? string.Empty,
                Zip = request.Zip ?? string.Empty,
                Phone = request.Phone ?? string.Empty,
            };

            _context.TransportCompanies.Add(company);
            me.TransportCompanyId = company.Id;
            me.Role = Roles.Admin;
            me.UpdatedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();

            _logger.LogInformation("Transport company {CompanyId} created by {UserId}", company.Id, me.Id);

            return CreatedAtAction(nameof(GetCompany), new { id = company.Id }, new TransportCompanyDetailDto
            {
                Id = company.Id,
                Name = company.Name,
                Status = company.Status
            });
        }

        // Admin of this company only.
        [HttpPatch("{id:guid}")]
        public async Task<IActionResult> UpdateCompany(Guid id, [FromBody] UpdateTransportCompanyRequest request)
        {
            var me = await CurrentUserAsync(_context);
            if (me == null)
                return Unauthorized();
            if (me.Role != Roles.Admin || me.TransportCompanyId != id)
                return Forbid();

            var company = await _context.TransportCompanies.FindAsync(id);
            if (company == null)
                return NotFound();

            company.Name = request.Name ?? company.Name;
            company.Address = request.Address ?? company.Address;
            company.City = request.City ?? company.City;
            company.State = request.State ?? company.State;
            company.Zip = request.Zip ?? company.Zip;
            company.Phone = request.Phone ?? company.Phone;
            company.UpdatedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();
            _logger.LogInformation("Transport company {CompanyId} updated", id);

            return NoContent();
        }

        // Your company's verified driver email domains.
        [HttpGet("{id:guid}/domains")]
        public async Task<ActionResult<IEnumerable<TransportCompanyDomainDto>>> GetDomains(Guid id)
        {
            var myCompanyId = await CurrentTransportCompanyIdAsync(_context);
            if (myCompanyId != id)
                return Forbid();

            var domains = await _context.TransportCompanyDomains
                .Where(d => d.TransportCompanyId == id)
                .OrderBy(d => d.CreatedAt)
                .Select(d => new TransportCompanyDomainDto
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
        /// Admin: claim a domain for this company. Same ownership proof as facility domains —
        /// the admin must currently be signed in with a verified email at that exact domain.
        /// </summary>
        [HttpPost("{id:guid}/domains")]
        public async Task<ActionResult<TransportCompanyDomainDto>> AddDomain(Guid id, [FromBody] AddTransportCompanyDomainRequest request)
        {
            var me = await CurrentUserAsync(_context);
            if (me == null)
                return Unauthorized();
            if (me.Role != Roles.Admin || me.TransportCompanyId != id)
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
                    $"(you're signed in as {me.Email}). This proves your company owns it.");

            if (await _context.TransportCompanyDomains.AnyAsync(d => d.Domain == domain))
                return BadRequest("That domain is already registered to a transport company.");

            var companyDomain = new TransportCompanyDomain { TransportCompanyId = id, Domain = domain, AddedByEmail = me.Email };
            _context.TransportCompanyDomains.Add(companyDomain);
            await _context.SaveChangesAsync();
            _logger.LogInformation("Transport company {CompanyId} claimed domain {Domain}", id, domain);

            return Ok(new TransportCompanyDomainDto
            {
                Id = companyDomain.Id,
                Domain = companyDomain.Domain,
                AddedByEmail = companyDomain.AddedByEmail,
                CreatedAt = companyDomain.CreatedAt
            });
        }

        // Admin: stop auto-joining drivers at this domain.
        [HttpDelete("{id:guid}/domains/{domainId:guid}")]
        public async Task<IActionResult> RemoveDomain(Guid id, Guid domainId)
        {
            var me = await CurrentUserAsync(_context);
            if (me == null)
                return Unauthorized();
            if (me.Role != Roles.Admin || me.TransportCompanyId != id)
                return Forbid();

            var domain = await _context.TransportCompanyDomains.FirstOrDefaultAsync(d => d.Id == domainId && d.TransportCompanyId == id);
            if (domain == null)
                return NotFound();

            _context.TransportCompanyDomains.Remove(domain);
            await _context.SaveChangesAsync();
            _logger.LogInformation("Transport company {CompanyId} removed domain {Domain}", id, domain.Domain);

            return NoContent();
        }

        // Admin: this company's driver roster.
        [HttpGet("{id:guid}/drivers")]
        public async Task<ActionResult<IEnumerable<DriverDto>>> GetCompanyDrivers(Guid id)
        {
            var me = await CurrentUserAsync(_context);
            if (me == null)
                return Unauthorized();
            if (me.Role != Roles.Admin || me.TransportCompanyId != id)
                return Forbid();

            var drivers = await _context.Drivers
                .Where(d => d.TransportCompanyId == id)
                .OrderBy(d => d.FirstName).ThenBy(d => d.LastName)
                .Select(d => new DriverDto
                {
                    Id = d.Id,
                    FirstName = d.FirstName,
                    LastName = d.LastName,
                    Phone = d.Phone,
                    VehicleType = d.VehicleType,
                    VehiclePlate = d.VehiclePlate,
                    Rating = d.Rating,
                    Status = d.Status,
                    LocationLat = d.LocationLat,
                    LocationLng = d.LocationLng,
                    LastLocationUpdate = d.LastLocationUpdate,
                    HasLogin = d.UserId != null,
                })
                .ToListAsync();

            return Ok(drivers);
        }

        // Admin: add a driver placeholder (no login) directly to this company's roster.
        [HttpPost("{id:guid}/drivers")]
        public async Task<ActionResult<DriverDto>> CreateCompanyDriver(Guid id, [FromBody] CreateDriverRequest request)
        {
            var me = await CurrentUserAsync(_context);
            if (me == null)
                return Unauthorized();
            if (me.Role != Roles.Admin || me.TransportCompanyId != id)
                return Forbid();

            var driver = new Driver
            {
                TransportCompanyId = id,
                FirstName = request.FirstName ?? string.Empty,
                LastName = request.LastName ?? string.Empty,
                Phone = request.Phone ?? string.Empty,
                VehicleType = request.VehicleType ?? string.Empty,
                VehiclePlate = request.VehiclePlate ?? string.Empty,
                BackgroundCheckStatus = "pending",
                Rating = 0,
                Status = "active"
            };

            _context.Drivers.Add(driver);
            await _context.SaveChangesAsync();
            _logger.LogInformation("Driver {DriverId} added to company {CompanyId} roster", driver.Id, id);

            return Ok(new DriverDto { Id = driver.Id, FirstName = driver.FirstName, LastName = driver.LastName });
        }
    }

    public class TransportCompanyDetailDto
    {
        public Guid Id { get; set; }
        public string Name { get; set; } = "";
        public string Address { get; set; } = "";
        public string City { get; set; } = "";
        public string State { get; set; } = "";
        public string Zip { get; set; } = "";
        public string Phone { get; set; } = "";
        public string Status { get; set; } = "";
        public List<string> Domains { get; set; } = new();
    }

    public class TransportCompanyDomainDto
    {
        public Guid Id { get; set; }
        public string Domain { get; set; } = "";
        public string AddedByEmail { get; set; } = "";
        public DateTime CreatedAt { get; set; }
    }

    public class CreateTransportCompanyRequest
    {
        public string? Name { get; set; }
        public string? Address { get; set; }
        public string? City { get; set; }
        public string? State { get; set; }
        public string? Zip { get; set; }
        public string? Phone { get; set; }
    }

    public class UpdateTransportCompanyRequest
    {
        public string? Name { get; set; }
        public string? Address { get; set; }
        public string? City { get; set; }
        public string? State { get; set; }
        public string? Zip { get; set; }
        public string? Phone { get; set; }
    }

    public class AddTransportCompanyDomainRequest
    {
        public string? Domain { get; set; }
    }
}
