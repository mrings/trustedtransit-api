using Microsoft.AspNetCore.Authorization;
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

        [AllowAnonymous]
        [HttpGet]
        public async Task<ActionResult<IEnumerable<RideDto>>> GetRides([FromQuery] Guid? facilityId, [FromQuery] string status = null)
        {
            _logger.LogInformation("GetRides called");

            var query = _context.Rides.AsQueryable();

            if (facilityId.HasValue)
                query = query.Where(r => r.FacilityId == facilityId);

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

        [AllowAnonymous]
        [HttpGet("{id}")]
        public async Task<ActionResult<RideDetailDto>> GetRide(Guid id)
        {
            var ride = await _context.Rides
                .FirstOrDefaultAsync(r => r.Id == id);

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

        [AllowAnonymous]
        [HttpPost]
        public async Task<ActionResult<RideDto>> CreateRide([FromBody] CreateRideRequest request)
        {
            var ride = new Ride
            {
                FacilityId = request.FacilityId,
                ResidentId = request.ResidentId,
                PickupAddress = request.PickupAddress,
                DestinationAddress = request.DestinationAddress,
                ScheduledPickupTime = request.ScheduledPickupTime,
                AppointmentType = request.AppointmentType,
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

        [AllowAnonymous]
        [HttpPatch("{id}")]
        public async Task<IActionResult> UpdateRideStatus(Guid id, [FromBody] UpdateRideStatusRequest request)
        {
            var ride = await _context.Rides.FindAsync(id);
            if (ride == null)
                return NotFound();

            ride.Status = request.Status;
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
        public Guid FacilityId { get; set; }
        public Guid ResidentId { get; set; }
        public string PickupAddress { get; set; }
        public string DestinationAddress { get; set; }
        public DateTime ScheduledPickupTime { get; set; }
        public string AppointmentType { get; set; }
        public string RideType { get; set; }
    }

    public class UpdateRideStatusRequest
    {
        public string Status { get; set; }
    }
}