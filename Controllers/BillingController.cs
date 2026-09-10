using System.Globalization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TrustedTransit.Api.Data;

namespace TrustedTransit.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class BillingController : BaseController
    {
        private readonly TrustedTransitDbContext _context;

        public BillingController(TrustedTransitDbContext context)
        {
            _context = context;
        }

        // GET /api/billing?month=YYYY-MM  (defaults to the current month)
        // Rides completed within the month, with charges and a summary.
        [HttpGet]
        public async Task<ActionResult<BillingDto>> Get([FromQuery] string? month)
        {
            var facilityId = await CurrentFacilityIdAsync(_context);
            if (facilityId == null)
                return Ok(new BillingDto { Month = MonthKey(DateTime.UtcNow) });

            DateTime start;
            if (string.IsNullOrWhiteSpace(month) ||
                !DateTime.TryParseExact(month, "yyyy-MM", CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsed))
                start = new DateTime(DateTime.UtcNow.Year, DateTime.UtcNow.Month, 1, 0, 0, 0, DateTimeKind.Utc);
            else
                start = DateTime.SpecifyKind(new DateTime(parsed.Year, parsed.Month, 1), DateTimeKind.Utc);
            var end = start.AddMonths(1);

            var rides = await _context.Rides
                .Where(r => r.FacilityId == facilityId
                    && r.Status == "completed"
                    && r.CompletedAt >= start && r.CompletedAt < end)
                .OrderBy(r => r.CompletedAt)
                .Select(r => new BillingRideDto
                {
                    Id = r.Id,
                    ResidentName = r.Resident!.FirstName + " " + r.Resident.LastName,
                    ScheduledPickupTime = r.ScheduledPickupTime,
                    CompletedAt = r.CompletedAt,
                    PickupAddress = r.PickupAddress,
                    DestinationAddress = r.DestinationAddress,
                    AppointmentType = r.AppointmentType,
                    BaseFare = r.BaseFare,
                    MileageCharge = r.MileageCharge,
                    TotalCharge = r.TotalCharge,
                    PaymentStatus = r.PaymentStatus,
                })
                .ToListAsync();

            return Ok(new BillingDto
            {
                Month = start.ToString("yyyy-MM"),
                RideCount = rides.Count,
                TotalBilled = rides.Sum(r => r.TotalCharge),
                PaidAmount = rides.Where(r => r.PaymentStatus == "paid").Sum(r => r.TotalCharge),
                OutstandingAmount = rides.Where(r => r.PaymentStatus != "paid").Sum(r => r.TotalCharge),
                Rides = rides,
            });
        }

        private static string MonthKey(DateTime d) => d.ToString("yyyy-MM");
    }

    public class BillingDto
    {
        public string Month { get; set; } = "";
        public int RideCount { get; set; }
        public decimal TotalBilled { get; set; }
        public decimal PaidAmount { get; set; }
        public decimal OutstandingAmount { get; set; }
        public List<BillingRideDto> Rides { get; set; } = new();
    }

    public class BillingRideDto
    {
        public Guid Id { get; set; }
        public string? ResidentName { get; set; }
        public DateTime ScheduledPickupTime { get; set; }
        public DateTime? CompletedAt { get; set; }
        public string PickupAddress { get; set; } = "";
        public string DestinationAddress { get; set; } = "";
        public string AppointmentType { get; set; } = "";
        public decimal BaseFare { get; set; }
        public decimal MileageCharge { get; set; }
        public decimal TotalCharge { get; set; }
        public string PaymentStatus { get; set; } = "";
    }
}
