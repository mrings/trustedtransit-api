using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TrustedTransit.Api.Data;
using TrustedTransit.Api.Models;

namespace TrustedTransit.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class RideSeriesController : BaseController
    {
        private readonly TrustedTransitDbContext _context;
        private readonly ILogger<RideSeriesController> _logger;

        // How far ahead recurring rides are generated.
        public const int HorizonDays = 56;

        public RideSeriesController(TrustedTransitDbContext context, ILogger<RideSeriesController> logger)
        {
            _context = context;
            _logger = logger;
        }

        [HttpGet]
        public async Task<ActionResult<IEnumerable<RideSeriesDto>>> GetSeries()
        {
            var facilityId = await CurrentFacilityIdAsync(_context);
            if (facilityId == null)
                return Ok(Array.Empty<RideSeriesDto>());

            var series = await _context.RideSeries
                .Where(s => s.FacilityId == facilityId)
                .OrderByDescending(s => s.Active).ThenBy(s => s.PickupTime)
                .Select(s => new RideSeriesDto
                {
                    Id = s.Id,
                    ResidentId = s.ResidentId,
                    ResidentName = s.Resident!.FirstName + " " + s.Resident.LastName,
                    DriverId = s.DriverId,
                    DriverName = s.Driver != null ? s.Driver.FirstName + " " + s.Driver.LastName : null,
                    PickupAddress = s.PickupAddress,
                    DestinationAddress = s.DestinationAddress,
                    AppointmentType = s.AppointmentType,
                    DaysOfWeek = s.DaysOfWeek,
                    PickupTime = s.PickupTime.ToString("HH:mm"),
                    StartDate = s.StartDate.ToString("yyyy-MM-dd"),
                    EndDate = s.EndDate.HasValue ? s.EndDate.Value.ToString("yyyy-MM-dd") : null,
                    Active = s.Active,
                })
                .ToListAsync();

            return Ok(series);
        }

        [HttpPost]
        public async Task<ActionResult<RideSeriesDto>> CreateSeries([FromBody] RideSeriesRequest request)
        {
            var facilityId = await CurrentFacilityIdAsync(_context);
            if (facilityId == null)
                return BadRequest("Your account isn't linked to a facility yet.");

            if (!await _context.Residents.AnyAsync(r => r.Id == request.ResidentId && r.FacilityId == facilityId))
                return BadRequest("Resident not found in this facility.");
            if (request.DriverId.HasValue && !await _context.Drivers.AnyAsync(d => d.Id == request.DriverId))
                return BadRequest("Driver not found.");
            if (!TryParseDays(request.DaysOfWeek, out var days))
                return BadRequest("Pick at least one day of the week.");
            if (!TimeOnly.TryParse(request.PickupTime, out var pickup))
                return BadRequest("Invalid pickup time.");
            if (!DateOnly.TryParse(request.StartDate, out var start))
                return BadRequest("Invalid start date.");
            DateOnly? end = null;
            if (!string.IsNullOrWhiteSpace(request.EndDate))
            {
                if (!DateOnly.TryParse(request.EndDate, out var e))
                    return BadRequest("Invalid end date.");
                if (e < start)
                    return BadRequest("End date is before the start date.");
                end = e;
            }

            var series = new RideSeries
            {
                FacilityId = facilityId.Value,
                ResidentId = request.ResidentId,
                DriverId = request.DriverId,
                PickupAddress = request.PickupAddress ?? string.Empty,
                DestinationAddress = request.DestinationAddress ?? string.Empty,
                AppointmentType = request.AppointmentType ?? string.Empty,
                DaysOfWeek = days,
                PickupTime = pickup,
                StartDate = start,
                EndDate = end,
                Active = true,
            };
            _context.RideSeries.Add(series);

            var today = DateOnly.FromDateTime(DateTime.UtcNow);
            await GenerateAsync(_context, series, today, today.AddDays(HorizonDays));
            await _context.SaveChangesAsync();

            _logger.LogInformation("RideSeries {SeriesId} created for resident {ResidentId}", series.Id, series.ResidentId);
            return CreatedAtAction(nameof(GetSeries), new RideSeriesDto { Id = series.Id });
        }

        [HttpPatch("{id}")]
        public async Task<IActionResult> UpdateSeries(Guid id, [FromBody] RideSeriesRequest request)
        {
            var facilityId = await CurrentFacilityIdAsync(_context);
            var series = await _context.RideSeries.FirstOrDefaultAsync(s => s.Id == id && s.FacilityId == facilityId);
            if (series == null)
                return NotFound();

            if (request.DriverId.HasValue && !await _context.Drivers.AnyAsync(d => d.Id == request.DriverId))
                return BadRequest("Driver not found.");

            if (request.DaysOfWeek != null)
            {
                if (!TryParseDays(request.DaysOfWeek, out var days))
                    return BadRequest("Pick at least one day of the week.");
                series.DaysOfWeek = days;
            }
            if (request.PickupTime != null)
            {
                if (!TimeOnly.TryParse(request.PickupTime, out var pickup))
                    return BadRequest("Invalid pickup time.");
                series.PickupTime = pickup;
            }
            if (request.StartDate != null)
            {
                if (!DateOnly.TryParse(request.StartDate, out var start))
                    return BadRequest("Invalid start date.");
                series.StartDate = start;
            }
            if (request.EndDate != null)
                series.EndDate = string.IsNullOrWhiteSpace(request.EndDate)
                    ? null
                    : DateOnly.TryParse(request.EndDate, out var e) ? e : series.EndDate;

            if (request.DriverIdSet) series.DriverId = request.DriverId;
            series.PickupAddress = request.PickupAddress ?? series.PickupAddress;
            series.DestinationAddress = request.DestinationAddress ?? series.DestinationAddress;
            series.AppointmentType = request.AppointmentType ?? series.AppointmentType;
            if (request.Active.HasValue) series.Active = request.Active.Value;
            series.UpdatedAt = DateTime.UtcNow;

            // Regenerate the future: drop upcoming generated rides that haven't started, then rebuild.
            await ClearFutureAsync(_context, series.Id);
            if (series.Active)
            {
                var today = DateOnly.FromDateTime(DateTime.UtcNow);
                await GenerateAsync(_context, series, today, today.AddDays(HorizonDays));
            }
            await _context.SaveChangesAsync();

            _logger.LogInformation("RideSeries {SeriesId} updated", id);
            return NoContent();
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteSeries(Guid id)
        {
            var facilityId = await CurrentFacilityIdAsync(_context);
            var series = await _context.RideSeries.FirstOrDefaultAsync(s => s.Id == id && s.FacilityId == facilityId);
            if (series == null)
                return NotFound();

            await ClearFutureAsync(_context, id);        // remove upcoming un-started rides
            _context.RideSeries.Remove(series);          // past rides keep their history (RideSeriesId -> null)
            await _context.SaveChangesAsync();

            _logger.LogInformation("RideSeries {SeriesId} deleted", id);
            return NoContent();
        }

        // --- generation ---------------------------------------------------------

        /// <summary>Ensures every active series in the facility has rides generated out to the horizon.</summary>
        public static async Task TopUpAsync(TrustedTransitDbContext db, Guid facilityId)
        {
            var today = DateOnly.FromDateTime(DateTime.UtcNow);
            var horizon = today.AddDays(HorizonDays);
            var series = await db.RideSeries.Where(s => s.FacilityId == facilityId && s.Active).ToListAsync();
            foreach (var s in series)
                await GenerateAsync(db, s, today, horizon);
            if (db.ChangeTracker.HasChanges())
                await db.SaveChangesAsync();
        }

        private static async Task GenerateAsync(TrustedTransitDbContext db, RideSeries series, DateOnly from, DateOnly to)
        {
            if (!series.Active)
                return;
            var days = series.Days().ToHashSet();
            if (days.Count == 0)
                return;

            var start = from > series.StartDate ? from : series.StartDate;
            var end = series.EndDate.HasValue && series.EndDate.Value < to ? series.EndDate.Value : to;
            if (start > end)
                return;

            var startDt = start.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
            var endDt = end.AddDays(1).ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
            var existing = await db.Rides
                .Where(r => r.RideSeriesId == series.Id && r.ScheduledPickupTime >= startDt && r.ScheduledPickupTime < endDt)
                .Select(r => r.ScheduledPickupTime)
                .ToListAsync();
            var taken = existing.Select(dt => DateOnly.FromDateTime(dt)).ToHashSet();

            for (var d = start; d <= end; d = d.AddDays(1))
            {
                if (!days.Contains(d.DayOfWeek) || taken.Contains(d))
                    continue;
                db.Rides.Add(new Ride
                {
                    FacilityId = series.FacilityId,
                    ResidentId = series.ResidentId,
                    DriverId = series.DriverId,
                    RideSeriesId = series.Id,
                    ScheduledPickupTime = d.ToDateTime(series.PickupTime, DateTimeKind.Utc),
                    PickupAddress = series.PickupAddress,
                    DestinationAddress = series.DestinationAddress,
                    AppointmentType = series.AppointmentType,
                    RideType = "recurring",
                    Status = series.DriverId != null ? "assigned" : "scheduled",
                    BaseFare = 10.00m,
                    TotalCharge = 10.00m,
                });
            }
        }

        // Upcoming rides for a series that haven't started yet.
        private static async Task ClearFutureAsync(TrustedTransitDbContext db, Guid seriesId)
        {
            var now = DateTime.UtcNow;
            var future = await db.Rides
                .Where(r => r.RideSeriesId == seriesId
                    && r.ScheduledPickupTime >= now
                    && r.Status != "completed" && r.Status != "in_progress")
                .ToListAsync();
            db.Rides.RemoveRange(future);
        }

        private static bool TryParseDays(string? csv, out string normalized)
        {
            normalized = "";
            var nums = (csv ?? "")
                .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Select(s => int.TryParse(s, out var n) ? n : -1)
                .Where(n => n is >= 0 and <= 6)
                .Distinct()
                .OrderBy(n => n)
                .ToList();
            if (nums.Count == 0)
                return false;
            normalized = string.Join(",", nums);
            return true;
        }
    }

    public class RideSeriesDto
    {
        public Guid Id { get; set; }
        public Guid ResidentId { get; set; }
        public string? ResidentName { get; set; }
        public Guid? DriverId { get; set; }
        public string? DriverName { get; set; }
        public string PickupAddress { get; set; } = "";
        public string DestinationAddress { get; set; } = "";
        public string AppointmentType { get; set; } = "";
        public string DaysOfWeek { get; set; } = "";
        public string PickupTime { get; set; } = "";
        public string StartDate { get; set; } = "";
        public string? EndDate { get; set; }
        public bool Active { get; set; }
    }

    public class RideSeriesRequest
    {
        public Guid ResidentId { get; set; }
        public Guid? DriverId { get; set; }
        public bool DriverIdSet { get; set; }
        public string? PickupAddress { get; set; }
        public string? DestinationAddress { get; set; }
        public string? AppointmentType { get; set; }
        public string? DaysOfWeek { get; set; }
        public string? PickupTime { get; set; }
        public string? StartDate { get; set; }
        public string? EndDate { get; set; }
        public bool? Active { get; set; }
    }
}
