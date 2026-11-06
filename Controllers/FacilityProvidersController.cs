using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TrustedTransit.Api.Data;
using TrustedTransit.Api.Models;
using TrustedTransit.Api.Services;

namespace TrustedTransit.Api.Controllers
{
    /// <summary>
    /// A facility's own picks from the shared, vetted driver/company pool — see
    /// <see cref="Models.FacilityProvider"/>. Replaces the old "Add Driver" flow, which let any
    /// facility admin type a brand-new, unvetted driver straight into the shared pool
    /// (<c>POST /api/drivers</c>, now removed) and let them see/edit every other facility's and
    /// company's drivers. This controller only ever surfaces drivers/companies that are already
    /// vetted elsewhere (SupportController) — it can link to them, never create or edit them.
    /// </summary>
    [ApiController]
    [Route("api/facilities/{facilityId:guid}/providers")]
    public class FacilityProvidersController : BaseController
    {
        private readonly TrustedTransitDbContext _context;
        private readonly GeocodingService _geocoding;

        public FacilityProvidersController(TrustedTransitDbContext context, GeocodingService geocoding)
        {
            _context = context;
            _geocoding = geocoding;
        }

        private async Task<ActionResult?> CheckFacilityAdminAsync(Guid facilityId)
        {
            var me = await CurrentUserAsync(_context);
            if (me == null) return Unauthorized();
            if (me.Role != Roles.Admin || me.FacilityId != facilityId) return Forbid();
            return null;
        }

        // Read-only viewing (the Drivers tab) is open to any staff member of the facility, same
        // as the old unscoped GET /api/drivers was; only adding/removing providers is admin-only.
        private async Task<ActionResult?> CheckFacilityMemberAsync(Guid facilityId)
        {
            var me = await CurrentUserAsync(_context);
            if (me == null) return Unauthorized();
            if (me.FacilityId != facilityId) return Forbid();
            return null;
        }

        private async Task<(decimal Lat, decimal Lng)?> EnsureGeocodedAsync(Facility facility)
        {
            if (facility.Lat != null && facility.Lng != null)
                return (facility.Lat.Value, facility.Lng.Value);

            var coords = await _geocoding.GeocodeAsync(facility.Address, facility.City, facility.State, facility.Zip);
            if (coords == null) return null;

            facility.Lat = coords.Value.Lat;
            facility.Lng = coords.Value.Lng;
            await _context.SaveChangesAsync();
            return coords;
        }

        private async Task<(decimal Lat, decimal Lng)?> EnsureGeocodedAsync(TransportCompany company)
        {
            if (company.Lat != null && company.Lng != null)
                return (company.Lat.Value, company.Lng.Value);

            var coords = await _geocoding.GeocodeAsync(company.Address, company.City, company.State, company.Zip);
            if (coords == null) return null;

            company.Lat = coords.Value.Lat;
            company.Lng = coords.Value.Lng;
            await _context.SaveChangesAsync();
            return coords;
        }

        // The facility's current picks: independent drivers added directly, plus every driver
        // on the roster of any company the facility has added (dynamic — new hires at that
        // company show up automatically, same as the company's own roster view).
        [HttpGet]
        public async Task<ActionResult<FacilityProvidersDto>> GetProviders(Guid facilityId)
        {
            if (await CheckFacilityMemberAsync(facilityId) is { } err) return err;

            var links = await _context.FacilityProviders
                .Where(p => p.FacilityId == facilityId)
                .Include(p => p.Driver)
                .Include(p => p.TransportCompany)
                .ToListAsync();

            var independentDrivers = links
                .Where(p => p.Driver != null)
                .Select(p => ToLinkedDriverDto(p.Id, p.Driver!))
                .ToList();

            var companies = new List<LinkedCompanyDto>();
            foreach (var p in links.Where(p => p.TransportCompany != null))
            {
                var roster = await _context.Drivers
                    .Where(d => d.TransportCompanyId == p.TransportCompanyId)
                    .ToListAsync();

                companies.Add(new LinkedCompanyDto
                {
                    ProviderLinkId = p.Id,
                    CompanyId = p.TransportCompany!.Id,
                    Name = p.TransportCompany.Name,
                    Status = p.TransportCompany.Status,
                    Drivers = roster.Select(d => ToLinkedDriverDto(null, d)).ToList(),
                });
            }

            return Ok(new FacilityProvidersDto { IndependentDrivers = independentDrivers, Companies = companies });
        }

        // Vetted independent drivers and companies within radiusMiles of the facility, not
        // already added. Distance for an independent driver comes from their last reported GPS
        // fix (LocationLat/Lng) — one who's never shared a location can't be placed, so they
        // won't show up here until they have. A company's distance comes from geocoding its
        // registered address instead, since companies don't report GPS.
        [HttpGet("nearby")]
        public async Task<ActionResult<NearbyProvidersDto>> GetNearby(Guid facilityId, [FromQuery] double radiusMiles = 25)
        {
            if (await CheckFacilityAdminAsync(facilityId) is { } err) return err;
            if (radiusMiles is <= 0 or > 500)
                return BadRequest("Enter a radius between 1 and 500 miles.");

            var facility = await _context.Facilities.FindAsync(facilityId);
            if (facility == null) return NotFound();

            var origin = await EnsureGeocodedAsync(facility);
            if (origin == null)
                return BadRequest("Couldn't determine your facility's location from its address — check it under Admin settings.");

            var linkedDriverIds = await _context.FacilityProviders
                .Where(p => p.FacilityId == facilityId && p.DriverId != null)
                .Select(p => p.DriverId!.Value)
                .ToListAsync();
            var linkedCompanyIds = await _context.FacilityProviders
                .Where(p => p.FacilityId == facilityId && p.TransportCompanyId != null)
                .Select(p => p.TransportCompanyId!.Value)
                .ToListAsync();

            var candidateDrivers = await _context.Drivers
                .Where(d => d.TransportCompanyId == null
                    && d.Status == "active" && d.BackgroundCheckStatus == "approved"
                    && d.LocationLat != null && d.LocationLng != null
                    && !linkedDriverIds.Contains(d.Id))
                .ToListAsync();

            var nearbyDrivers = candidateDrivers
                .Select(d => new
                {
                    d,
                    dist = GeocodingService.DistanceMiles(
                        (double)origin.Value.Lat, (double)origin.Value.Lng,
                        (double)d.LocationLat!.Value, (double)d.LocationLng!.Value)
                })
                .Where(x => x.dist <= radiusMiles)
                .OrderBy(x => x.dist)
                .Select(x => new NearbyDriverDto
                {
                    Id = x.d.Id,
                    FirstName = x.d.FirstName,
                    LastName = x.d.LastName,
                    Phone = x.d.Phone,
                    VehicleType = x.d.VehicleType,
                    DistanceMiles = Math.Round(x.dist, 1),
                })
                .ToList();

            var candidateCompanies = await _context.TransportCompanies
                .Where(c => c.Status == "active" && !linkedCompanyIds.Contains(c.Id))
                .ToListAsync();

            var nearbyCompanies = new List<NearbyCompanyDto>();
            foreach (var c in candidateCompanies)
            {
                var coords = await EnsureGeocodedAsync(c);
                if (coords == null) continue;

                var dist = GeocodingService.DistanceMiles(
                    (double)origin.Value.Lat, (double)origin.Value.Lng,
                    (double)coords.Value.Lat, (double)coords.Value.Lng);
                if (dist > radiusMiles) continue;

                nearbyCompanies.Add(new NearbyCompanyDto
                {
                    Id = c.Id,
                    Name = c.Name,
                    City = c.City,
                    State = c.State,
                    DriverCount = await _context.Drivers.CountAsync(d => d.TransportCompanyId == c.Id),
                    DistanceMiles = Math.Round(dist, 1),
                });
            }

            return Ok(new NearbyProvidersDto
            {
                Drivers = nearbyDrivers,
                Companies = nearbyCompanies.OrderBy(c => c.DistanceMiles).ToList(),
            });
        }

        [HttpPost]
        public async Task<IActionResult> AddProvider(Guid facilityId, [FromBody] AddProviderRequest request)
        {
            if (await CheckFacilityAdminAsync(facilityId) is { } err) return err;

            if (request.DriverId == null && request.TransportCompanyId == null)
                return BadRequest("Specify a driverId or a transportCompanyId.");
            if (request.DriverId != null && request.TransportCompanyId != null)
                return BadRequest("Specify only one of driverId or transportCompanyId.");

            if (request.DriverId != null)
            {
                var driver = await _context.Drivers.FindAsync(request.DriverId.Value);
                if (driver == null) return NotFound("Driver not found.");
                if (driver.TransportCompanyId != null)
                    return BadRequest("That driver belongs to a transport company — add the company instead.");
                if (driver.Status != "active" || driver.BackgroundCheckStatus != "approved")
                    return BadRequest("Only vetted, active drivers can be added.");
                if (await _context.FacilityProviders.AnyAsync(p => p.FacilityId == facilityId && p.DriverId == driver.Id))
                    return BadRequest("Already added.");

                _context.FacilityProviders.Add(new FacilityProvider { FacilityId = facilityId, DriverId = driver.Id });
            }
            else
            {
                var company = await _context.TransportCompanies.FindAsync(request.TransportCompanyId!.Value);
                if (company == null) return NotFound("Company not found.");
                if (company.Status != "active")
                    return BadRequest("Only vetted (active) companies can be added.");
                if (await _context.FacilityProviders.AnyAsync(p => p.FacilityId == facilityId && p.TransportCompanyId == company.Id))
                    return BadRequest("Already added.");

                _context.FacilityProviders.Add(new FacilityProvider { FacilityId = facilityId, TransportCompanyId = company.Id });
            }

            await _context.SaveChangesAsync();
            return NoContent();
        }

        [HttpDelete("{providerLinkId:guid}")]
        public async Task<IActionResult> RemoveProvider(Guid facilityId, Guid providerLinkId)
        {
            if (await CheckFacilityAdminAsync(facilityId) is { } err) return err;

            var link = await _context.FacilityProviders.FirstOrDefaultAsync(p => p.Id == providerLinkId && p.FacilityId == facilityId);
            if (link == null) return NotFound();

            _context.FacilityProviders.Remove(link);
            await _context.SaveChangesAsync();
            return NoContent();
        }

        private static LinkedDriverDto ToLinkedDriverDto(Guid? providerLinkId, Driver d) => new()
        {
            ProviderLinkId = providerLinkId,
            DriverId = d.Id,
            FirstName = d.FirstName,
            LastName = d.LastName,
            Phone = d.Phone,
            VehicleType = d.VehicleType,
            VehiclePlate = d.VehiclePlate,
            Status = d.Status,
            BackgroundCheckStatus = d.BackgroundCheckStatus,
            LocationLat = d.LocationLat,
            LocationLng = d.LocationLng,
        };
    }

    public class FacilityProvidersDto
    {
        public List<LinkedDriverDto> IndependentDrivers { get; set; } = new();
        public List<LinkedCompanyDto> Companies { get; set; } = new();
    }

    public class LinkedDriverDto
    {
        // Null for a driver shown as part of a linked company's roster — remove the company
        // (its own ProviderLinkId) to drop the whole roster, not individual drivers within it.
        public Guid? ProviderLinkId { get; set; }
        public Guid DriverId { get; set; }
        public string FirstName { get; set; } = "";
        public string LastName { get; set; } = "";
        public string Phone { get; set; } = "";
        public string VehicleType { get; set; } = "";
        public string VehiclePlate { get; set; } = "";
        public string Status { get; set; } = "";
        public string BackgroundCheckStatus { get; set; } = "";
        public decimal? LocationLat { get; set; }
        public decimal? LocationLng { get; set; }
    }

    public class LinkedCompanyDto
    {
        public Guid ProviderLinkId { get; set; }
        public Guid CompanyId { get; set; }
        public string Name { get; set; } = "";
        public string Status { get; set; } = "";
        public List<LinkedDriverDto> Drivers { get; set; } = new();
    }

    public class NearbyProvidersDto
    {
        public List<NearbyDriverDto> Drivers { get; set; } = new();
        public List<NearbyCompanyDto> Companies { get; set; } = new();
    }

    public class NearbyDriverDto
    {
        public Guid Id { get; set; }
        public string FirstName { get; set; } = "";
        public string LastName { get; set; } = "";
        public string Phone { get; set; } = "";
        public string VehicleType { get; set; } = "";
        public double DistanceMiles { get; set; }
    }

    public class NearbyCompanyDto
    {
        public Guid Id { get; set; }
        public string Name { get; set; } = "";
        public string City { get; set; } = "";
        public string State { get; set; } = "";
        public int DriverCount { get; set; }
        public double DistanceMiles { get; set; }
    }

    public class AddProviderRequest
    {
        public Guid? DriverId { get; set; }
        public Guid? TransportCompanyId { get; set; }
    }
}
