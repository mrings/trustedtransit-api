using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TrustedTransit.Api.Data;

namespace TrustedTransit.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class DashboardController : BaseController
    {
        private readonly TrustedTransitDbContext _context;

        public DashboardController(TrustedTransitDbContext context)
        {
            _context = context;
        }

        [HttpGet]
        public async Task<ActionResult<DashboardDto>> Get()
        {
            var facilityId = await CurrentFacilityIdAsync(_context);
            if (facilityId == null)
                return Ok(new DashboardDto());

            var now = DateTime.UtcNow;
            var todayEnd = now.Date.AddDays(1);
            var monthStart = new DateTime(now.Year, now.Month, 1, 0, 0, 0, DateTimeKind.Utc);

            var residents = _context.Residents.Where(r => r.FacilityId == facilityId);
            var rides = _context.Rides.Where(r => r.FacilityId == facilityId);
            var completedThisMonth = rides.Where(r => r.Status == "completed" && r.CompletedAt >= monthStart);

            return Ok(new DashboardDto
            {
                Residents = await residents.CountAsync(r => r.Status == "active"),
                ActiveDrivers = await _context.Drivers.CountAsync(d => d.Status == "active"),
                UpcomingRides = await rides.CountAsync(r =>
                    (r.Status == "scheduled" || r.Status == "assigned") && r.ScheduledPickupTime >= now),
                RidesToday = await rides.CountAsync(r =>
                    r.ScheduledPickupTime >= now.Date && r.ScheduledPickupTime < todayEnd),
                CompletedThisMonth = await completedThisMonth.CountAsync(),
                BilledThisMonth = await completedThisMonth.SumAsync(r => (decimal?)r.TotalCharge) ?? 0m,
            });
        }
    }

    public class DashboardDto
    {
        public int Residents { get; set; }
        public int ActiveDrivers { get; set; }
        public int UpcomingRides { get; set; }
        public int RidesToday { get; set; }
        public int CompletedThisMonth { get; set; }
        public decimal BilledThisMonth { get; set; }
    }
}
