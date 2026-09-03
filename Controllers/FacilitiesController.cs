using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TrustedTransit.Api.Data;
using TrustedTransit.Api.Models;
using Microsoft.AspNetCore.Authorization;

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
        [AllowAnonymous]
        [HttpGet]
        public async Task<ActionResult<IEnumerable<FacilityDto>>> GetFacilities()
        {
            _logger.LogInformation("GetFacilities called");

            var facilities = await _context.Facilities
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

        [HttpGet("{id}")]
        public async Task<ActionResult<FacilityDetailDto>> GetFacility(Guid id)
        {
            var facility = await _context.Facilities
                .FirstOrDefaultAsync(f => f.Id == id);

            if (facility == null)
                return NotFound();

            return Ok(new FacilityDetailDto
            {
                Id = facility.Id,
                Name = facility.Name,
                Address = facility.Address,
                City = facility.City,
                State = facility.State,
                Zip = facility.Zip,
                Phone = facility.Phone,
                SubscriptionTier = facility.SubscriptionTier,
                SubscriptionStatus = facility.SubscriptionStatus
            });
        }
        
        [AllowAnonymous]
        [HttpPost]
        public async Task<ActionResult<FacilityDto>> CreateFacility([FromBody] CreateFacilityRequest request)
        {
            var facility = new Facility
            {
                Name = request.Name,
                Address = request.Address,
                City = request.City,
                State = request.State,
                Zip = request.Zip,
                Phone = request.Phone,
                SubscriptionTier = "starter",
                SubscriptionStatus = "trial"
            };

            _context.Facilities.Add(facility);
            await _context.SaveChangesAsync();

            _logger.LogInformation("Facility {FacilityId} created", facility.Id);

            return CreatedAtAction(nameof(GetFacility), new { id = facility.Id }, new FacilityDto
            {
                Id = facility.Id,
                Name = facility.Name
            });
        }

        [HttpPatch("{id}")]
        public async Task<IActionResult> UpdateFacility(Guid id, [FromBody] UpdateFacilityRequest request)
        {
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
        public string SubscriptionTier { get; set; }
        public string SubscriptionStatus { get; set; }
    }

    public class CreateFacilityRequest
    {
        public string Name { get; set; }
        public string Address { get; set; }
        public string City { get; set; }
        public string State { get; set; }
        public string Zip { get; set; }
        public string Phone { get; set; }
    }

    public class UpdateFacilityRequest
    {
        public string Name { get; set; }
        public string Address { get; set; }
        public string City { get; set; }
        public string State { get; set; }
        public string Zip { get; set; }
        public string Phone { get; set; }
        public string SubscriptionTier { get; set; }
    }
}