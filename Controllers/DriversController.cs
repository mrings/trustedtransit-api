using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TrustedTransit.Api.Data;
using TrustedTransit.Api.Models;
using Microsoft.AspNetCore.Authorization;

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
        [AllowAnonymous]
        [HttpGet]
        public async Task<ActionResult<IEnumerable<DriverDto>>> GetDrivers([FromQuery] string status = null)
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
                    Rating = d.Rating,
                    Status = d.Status
                })
                .ToListAsync();

            return Ok(drivers);
        }

        [HttpGet("{id}")]
        public async Task<ActionResult<DriverDetailDto>> GetDriver(Guid id)
        {
            var driver = await _context.Drivers
                .FirstOrDefaultAsync(d => d.Id == id);

            if (driver == null)
                return NotFound();

            return Ok(new DriverDetailDto
            {
                Id = driver.Id,
                FirstName = driver.FirstName,
                LastName = driver.LastName,
                Phone = driver.Phone,
                VehicleType = driver.VehicleType,
                VehiclePlate = driver.VehiclePlate,
                BackgroundCheckStatus = driver.BackgroundCheckStatus,
                Rating = driver.Rating,
                Status = driver.Status,
                LocationLat = driver.LocationLat,
                LocationLng = driver.LocationLng
            });
        }

        [AllowAnonymous]
        [HttpPost]
        public async Task<ActionResult<DriverDto>> CreateDriver([FromBody] CreateDriverRequest request)
        {
            var driver = new Driver
            {
                FirstName = request.FirstName,
                LastName = request.LastName,
                Phone = request.Phone,
                VehicleType = request.VehicleType,
                VehiclePlate = request.VehiclePlate,
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

        [HttpPatch("{id}")]
        public async Task<IActionResult> UpdateDriver(Guid id, [FromBody] UpdateDriverRequest request)
        {
            var driver = await _context.Drivers.FindAsync(id);
            if (driver == null)
                return NotFound();

            driver.FirstName = request.FirstName ?? driver.FirstName;
            driver.LastName = request.LastName ?? driver.LastName;
            driver.Phone = request.Phone ?? driver.Phone;
            driver.Status = request.Status ?? driver.Status;
            driver.UpdatedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();
            _logger.LogInformation("Driver {DriverId} updated", id);

            return NoContent();
        }

        [HttpPatch("{id}/location")]
        public async Task<IActionResult> UpdateDriverLocation(Guid id, [FromBody] UpdateLocationRequest request)
        {
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
        public decimal Rating { get; set; }
        public string Status { get; set; }
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
    }

    public class CreateDriverRequest
    {
        public string FirstName { get; set; }
        public string LastName { get; set; }
        public string Phone { get; set; }
        public string VehicleType { get; set; }
        public string VehiclePlate { get; set; }
    }

    public class UpdateDriverRequest
    {
        public string FirstName { get; set; }
        public string LastName { get; set; }
        public string Phone { get; set; }
        public string Status { get; set; }
    }

    public class UpdateLocationRequest
    {
        public decimal Latitude { get; set; }
        public decimal Longitude { get; set; }
    }
}