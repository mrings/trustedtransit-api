using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TrustedTransit.Api.Data;
using TrustedTransit.Api.Models;

namespace TrustedTransit.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class RidesController : BaseController
    {
        private readonly TrustedTransitDbContext _context;
        private readonly ILogger<RidesController> _logger;

        public RidesController(TrustedTransitDbContext context, ILogger<RidesController> logger)
        {
            _context = context;
            _logger = logger;
        }

        [HttpGet]
        public async Task<ActionResult<IEnumerable<RideDto>>> GetRides([FromQuery] string? status = null)
        {
            var facilityId = await CurrentFacilityIdAsync(_context);
            if (facilityId == null)
                return Ok(Array.Empty<RideDto>());

            var query = _context.Rides.Where(r => r.FacilityId == facilityId);

            if (!string.IsNullOrEmpty(status))
                query = query.Where(r => r.Status == status);

            var rides = await query
                .Select(r => new RideDto
                {
                    Id = r.Id,
                    FacilityId = r.FacilityId,
                    ResidentId = r.ResidentId,
                    DriverId = r.DriverId,
                    ScheduledPickupTime = r.ScheduledPickupTime,
                    PickupAddress = r.PickupAddress,
                    DestinationAddress = r.DestinationAddress,
                    Status = r.Status
                })
                .ToListAsync();

            return Ok(rides);
        }

        [HttpGet("{id}")]
        public async Task<ActionResult<RideDetailDto>> GetRide(Guid id)
        {
            var facilityId = await CurrentFacilityIdAsync(_context);
            var ride = await _context.Rides
                .FirstOrDefaultAsync(r => r.Id == id && r.FacilityId == facilityId);

            if (ride == null)
                return NotFound();

            return Ok(new RideDetailDto
            {
                Id = ride.Id,
                FacilityId = ride.FacilityId,
                ResidentId = ride.ResidentId,
                DriverId = ride.DriverId,
                ScheduledPickupTime = ride.ScheduledPickupTime,
                ActualPickupTime = ride.ActualPickupTime,
                ActualDropoffTime = ride.ActualDropoffTime,
                PickupAddress = ride.PickupAddress,
                DestinationAddress = ride.DestinationAddress,
                AppointmentType = ride.AppointmentType,
                Status = ride.Status,
                BaseFare = ride.BaseFare,
                TotalCharge = ride.TotalCharge
            });
        }

        [HttpPost]
        public async Task<ActionResult<RideDto>> CreateRide([FromBody] CreateRideRequest request)
        {
            var facilityId = await CurrentFacilityIdAsync(_context);
            if (facilityId == null)
                return BadRequest("Your account isn't linked to a facility yet.");

            // The resident must belong to that facility.
            var residentOk = await _context.Residents
                .AnyAsync(r => r.Id == request.ResidentId && r.FacilityId == facilityId);
            if (!residentOk)
                return BadRequest("Resident not found in this facility.");

            var ride = new Ride
            {
                FacilityId = facilityId.Value,
                ResidentId = request.ResidentId,
                PickupAddress = request.PickupAddress ?? string.Empty,
                DestinationAddress = request.DestinationAddress ?? string.Empty,
                ScheduledPickupTime = ToUtc(request.ScheduledPickupTime),
                AppointmentType = request.AppointmentType ?? string.Empty,
                RideType = request.RideType ?? "one-time",
                Status = "scheduled",
                BaseFare = 10.00m,
                TotalCharge = 10.00m
            };

            _context.Rides.Add(ride);
            await _context.SaveChangesAsync();

            _logger.LogInformation("Ride {RideId} created", ride.Id);

            return CreatedAtAction(nameof(GetRide), new { id = ride.Id }, new RideDto
            {
                Id = ride.Id,
                FacilityId = ride.FacilityId,
                ResidentId = ride.ResidentId
            });
        }

        [HttpPatch("{id}")]
        public async Task<IActionResult> UpdateRideStatus(Guid id, [FromBody] UpdateRideStatusRequest request)
        {
            var facilityId = await CurrentFacilityIdAsync(_context);
            var ride = await _context.Rides
                .FirstOrDefaultAsync(r => r.Id == id && r.FacilityId == facilityId);
            if (ride == null)
                return NotFound();

            ride.Status = request.Status ?? ride.Status;
            ride.UpdatedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();
            _logger.LogInformation("Ride {RideId} status updated to {Status}", id, request.Status);

            return NoContent();
        }
    }

    public class RideDto
    {
        public Guid Id { get; set; }
        public Guid FacilityId { get; set; }
        public Guid ResidentId { get; set; }
        public Guid? DriverId { get; set; }
        public DateTime ScheduledPickupTime { get; set; }
        public string PickupAddress { get; set; }
        public string DestinationAddress { get; set; }
        public string Status { get; set; }
    }

    public class RideDetailDto
    {
        public Guid Id { get; set; }
        public Guid FacilityId { get; set; }
        public Guid ResidentId { get; set; }
        public Guid? DriverId { get; set; }
        public DateTime ScheduledPickupTime { get; set; }
        public DateTime? ActualPickupTime { get; set; }
        public DateTime? ActualDropoffTime { get; set; }
        public string PickupAddress { get; set; }
        public string DestinationAddress { get; set; }
        public string AppointmentType { get; set; }
        public string Status { get; set; }
        public decimal BaseFare { get; set; }
        public decimal TotalCharge { get; set; }
    }

    public class CreateRideRequest
    {
        public Guid ResidentId { get; set; }
        public string? PickupAddress { get; set; }
        public string? DestinationAddress { get; set; }
        public DateTime ScheduledPickupTime { get; set; }
        public string? AppointmentType { get; set; }
        public string? RideType { get; set; }
    }

    public class UpdateRideStatusRequest
    {
        public string? Status { get; set; }
    }
}