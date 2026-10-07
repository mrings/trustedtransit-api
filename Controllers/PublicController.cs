using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TrustedTransit.Api.Data;

namespace TrustedTransit.Api.Controllers
{
    /// <summary>
    /// Unauthenticated endpoints for family members holding a ride's tracking link
    /// (<see cref="Models.Ride.TrackingToken"/>). Deliberately does not inherit
    /// <see cref="BaseController"/> — it carries no [Authorize] and must stay that way.
    /// The token itself (128 random bits) is the only credential; nothing here is guessable
    /// or enumerable from a facility/resident/ride id.
    /// </summary>
    [ApiController]
    [Route("api/public")]
    public class PublicController : ControllerBase
    {
        private static readonly TimeSpan LocationFreshness = TimeSpan.FromMinutes(30);
        private readonly TrustedTransitDbContext _context;

        public PublicController(TrustedTransitDbContext context)
        {
            _context = context;
        }

        [HttpGet("rides/{token}")]
        public async Task<ActionResult<PublicRideDto>> GetRideByToken(string token)
        {
            if (string.IsNullOrWhiteSpace(token))
                return NotFound();

            var ride = await _context.Rides
                .Include(r => r.Resident)
                .Include(r => r.Driver)
                .FirstOrDefaultAsync(r => r.TrackingToken == token);
            if (ride == null)
                return NotFound();

            var driver = ride.Driver;
            var showLocation = driver?.LocationLat != null && driver.LocationLng != null
                && driver.LastLocationUpdate != null
                && DateTime.UtcNow - driver.LastLocationUpdate < LocationFreshness
                && ride.Status is "scheduled" or "assigned" or "in_progress";

            return Ok(new PublicRideDto
            {
                ResidentFirstName = ride.Resident?.FirstName ?? "",
                Status = ride.Status,
                ScheduledPickupTime = ride.ScheduledPickupTime,
                PickupAddress = ride.PickupAddress,
                DestinationAddress = ride.DestinationAddress,
                AppointmentType = ride.AppointmentType,
                DriverName = driver == null ? null : $"{driver.FirstName} {driver.LastName}".Trim(),
                VehicleType = driver?.VehicleType,
                VehiclePlate = driver?.VehiclePlate,
                LocationLat = showLocation ? driver!.LocationLat : null,
                LocationLng = showLocation ? driver!.LocationLng : null,
                LastLocationUpdate = showLocation ? driver!.LastLocationUpdate : null,
            });
        }
    }

    public class PublicRideDto
    {
        public string ResidentFirstName { get; set; } = "";
        public string Status { get; set; } = "";
        public DateTime ScheduledPickupTime { get; set; }
        public string PickupAddress { get; set; } = "";
        public string DestinationAddress { get; set; } = "";
        public string AppointmentType { get; set; } = "";
        public string? DriverName { get; set; }
        public string? VehicleType { get; set; }
        public string? VehiclePlate { get; set; }
        public decimal? LocationLat { get; set; }
        public decimal? LocationLng { get; set; }
        public DateTime? LastLocationUpdate { get; set; }
    }
}
