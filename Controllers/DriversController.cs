using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TrustedTransit.Api.Data;
using TrustedTransit.Api.Models;

namespace TrustedTransit.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class DriversController : BaseController
    {
        private readonly TrustedTransitDbContext _context;
        private readonly ILogger<DriversController> _logger;

        public DriversController(TrustedTransitDbContext context, ILogger<DriversController> logger)
        {
            _context = context;
            _logger = logger;
        }

        [HttpGet]
        public async Task<ActionResult<IEnumerable<DriverDto>>> GetDrivers([FromQuery] string? status = null)
        {
            _logger.LogInformation("GetDrivers called");

            var query = _context.Drivers.AsQueryable();

            if (!string.IsNullOrEmpty(status))
                query = query.Where(d => d.Status == status);

            var drivers = await query
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
                    LastLocationUpdate = d.LastLocationUpdate
                })
                .ToListAsync();

            return Ok(drivers);
        }

        // The driver-role caller's own record (get-or-created).
        [HttpGet("me")]
        public async Task<ActionResult<DriverDetailDto>> GetMyDriver()
        {
            var driver = await GetOrCreateCurrentDriverAsync(_context);
            if (driver == null)
                return StatusCode(403, "Not a driver account.");
            return Ok(ToDetailDto(driver));
        }

        [HttpPatch("me")]
        public async Task<IActionResult> UpdateMyDriver([FromBody] UpdateDriverRequest request)
        {
            var driver = await GetOrCreateCurrentDriverAsync(_context);
            if (driver == null)
                return StatusCode(403, "Not a driver account.");

            driver.FirstName = request.FirstName ?? driver.FirstName;
            driver.LastName = request.LastName ?? driver.LastName;
            driver.Phone = request.Phone ?? driver.Phone;
            driver.VehicleType = request.VehicleType ?? driver.VehicleType;
            driver.VehiclePlate = request.VehiclePlate ?? driver.VehiclePlate;
            driver.UpdatedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();
            return NoContent();
        }

        [HttpPost("me/location")]
        public async Task<IActionResult> UpdateMyLocation([FromBody] UpdateLocationRequest request)
        {
            var driver = await GetOrCreateCurrentDriverAsync(_context);
            if (driver == null)
                return StatusCode(403, "Not a driver account.");

            driver.LocationLat = request.Latitude;
            driver.LocationLng = request.Longitude;
            driver.LastLocationUpdate = DateTime.UtcNow;
            await _context.SaveChangesAsync();
            return NoContent();
        }

        [HttpGet("{id}")]
        public async Task<ActionResult<DriverDetailDto>> GetDriver(Guid id)
        {
            var driver = await _context.Drivers
                .FirstOrDefaultAsync(d => d.Id == id);

            if (driver == null)
                return NotFound();

            return Ok(ToDetailDto(driver));
        }

        private static DriverDetailDto ToDetailDto(Driver d) => new()
        {
            Id = d.Id,
            FirstName = d.FirstName,
            LastName = d.LastName,
            Phone = d.Phone,
            VehicleType = d.VehicleType,
            VehiclePlate = d.VehiclePlate,
            BackgroundCheckStatus = d.BackgroundCheckStatus,
            Rating = d.Rating,
            Status = d.Status,
            LocationLat = d.LocationLat,
            LocationLng = d.LocationLng,
            LastLocationUpdate = d.LastLocationUpdate,
        };

        // Admin only. Drivers aren't facility-scoped yet (shared provider pool).
        [HttpPost]
        public async Task<ActionResult<DriverDto>> CreateDriver([FromBody] CreateDriverRequest request)
        {
            if (await CheckAdminAsync(_context) is { } err) return err;

            var driver = new Driver
            {
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

            _logger.LogInformation("Driver {DriverId} created", driver.Id);

            return CreatedAtAction(nameof(GetDriver), new { id = driver.Id }, new DriverDto
            {
                Id = driver.Id,
                FirstName = driver.FirstName,
                LastName = driver.LastName
            });
        }

        // Admin only.
        [HttpPatch("{id}")]
        public async Task<IActionResult> UpdateDriver(Guid id, [FromBody] UpdateDriverRequest request)
        {
            if (await CheckAdminAsync(_context) is { } err) return err;

            var driver = await _context.Drivers.FindAsync(id);
            if (driver == null)
                return NotFound();

            driver.FirstName = request.FirstName ?? driver.FirstName;
            driver.LastName = request.LastName ?? driver.LastName;
            driver.Phone = request.Phone ?? driver.Phone;
            driver.VehicleType = request.VehicleType ?? driver.VehicleType;
            driver.VehiclePlate = request.VehiclePlate ?? driver.VehiclePlate;
            driver.Status = request.Status ?? driver.Status;
            driver.UpdatedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();
            _logger.LogInformation("Driver {DriverId} updated", id);

            return NoContent();
        }

        // Admin only. Unassigns the driver from any rides, then deletes.
        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteDriver(Guid id)
        {
            if (await CheckAdminAsync(_context) is { } err) return err;

            var driver = await _context.Drivers.FindAsync(id);
            if (driver == null)
                return NotFound();

            var assignedRides = await _context.Rides.Where(r => r.DriverId == id).ToListAsync();
            foreach (var ride in assignedRides)
                ride.DriverId = null;

            _context.Drivers.Remove(driver);
            await _context.SaveChangesAsync();
            _logger.LogInformation("Driver {DriverId} deleted ({RideCount} rides unassigned)", id, assignedRides.Count);

            return NoContent();
        }

        // Admin for now; a driver updating their own location comes with the driver app.
        [HttpPatch("{id}/location")]
        public async Task<IActionResult> UpdateDriverLocation(Guid id, [FromBody] UpdateLocationRequest request)
        {
            if (await CheckAdminAsync(_context) is { } err) return err;

            var driver = await _context.Drivers.FindAsync(id);
            if (driver == null)
                return NotFound();

            driver.LocationLat = request.Latitude;
            driver.LocationLng = request.Longitude;
            driver.LastLocationUpdate = DateTime.UtcNow;

            await _context.SaveChangesAsync();

            return NoContent();
        }
    }

    public class DriverDto
    {
        public Guid Id { get; set; }
        public string FirstName { get; set; }
        public string LastName { get; set; }
        public string Phone { get; set; }
        public string VehicleType { get; set; }
        public string VehiclePlate { get; set; }
        public decimal Rating { get; set; }
        public string Status { get; set; }
        public decimal? LocationLat { get; set; }
        public decimal? LocationLng { get; set; }
        public DateTime? LastLocationUpdate { get; set; }
    }

    public class DriverDetailDto
    {
        public Guid Id { get; set; }
        public string FirstName { get; set; }
        public string LastName { get; set; }
        public string Phone { get; set; }
        public string VehicleType { get; set; }
        public string VehiclePlate { get; set; }
        public string BackgroundCheckStatus { get; set; }
        public decimal Rating { get; set; }
        public string Status { get; set; }
        public decimal? LocationLat { get; set; }
        public decimal? LocationLng { get; set; }
        public DateTime? LastLocationUpdate { get; set; }
    }

    public class CreateDriverRequest
    {
        public string? FirstName { get; set; }
        public string? LastName { get; set; }
        public string? Phone { get; set; }
        public string? VehicleType { get; set; }
        public string? VehiclePlate { get; set; }
    }

    public class UpdateDriverRequest
    {
        public string? FirstName { get; set; }
        public string? LastName { get; set; }
        public string? Phone { get; set; }
        public string? VehicleType { get; set; }
        public string? VehiclePlate { get; set; }
        public string? Status { get; set; }
    }

    public class UpdateLocationRequest
    {
        public decimal Latitude { get; set; }
        public decimal Longitude { get; set; }
    }
}