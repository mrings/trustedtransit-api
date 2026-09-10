using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TrustedTransit.Api.Data;
using TrustedTransit.Api.Models;
using TrustedTransit.Api.Services;

namespace TrustedTransit.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class NotificationsController : BaseController
    {
        private readonly TrustedTransitDbContext _context;
        private readonly NotificationService _notifications;

        public NotificationsController(TrustedTransitDbContext context, NotificationService notifications)
        {
            _context = context;
            _notifications = notifications;
        }

        [HttpGet("settings")]
        public async Task<ActionResult<NotificationSettingsDto>> GetSettings()
        {
            var facility = await CurrentFacilityAsync(_context);
            return Ok(new NotificationSettingsDto
            {
                Enabled = facility?.NotificationsEnabled ?? false,
                EmailChannel = _notifications.EmailEnabled,
                SmsChannel = _notifications.SmsEnabled,
            });
        }

        // Admin: turn family notifications on/off for the facility.
        [HttpPut("settings")]
        public async Task<IActionResult> UpdateSettings([FromBody] NotificationSettingsRequest request)
        {
            var me = await CurrentUserAsync(_context);
            if (me == null) return Unauthorized();
            if (me.Role != Roles.Admin || me.FacilityId == null) return StatusCode(403, "Admins only.");

            var facility = await _context.Facilities.FirstAsync(f => f.Id == me.FacilityId);
            facility.NotificationsEnabled = request.Enabled;
            facility.UpdatedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();
            return NoContent();
        }

        // Delivery history for one ride.
        [HttpGet("ride/{rideId}")]
        public async Task<ActionResult<IEnumerable<RideNotificationDto>>> ForRide(Guid rideId)
        {
            var facilityId = await CurrentFacilityIdAsync(_context);
            if (facilityId == null)
                return Ok(Array.Empty<RideNotificationDto>());

            var items = await _context.RideNotifications
                .Where(n => n.RideId == rideId && n.FacilityId == facilityId)
                .OrderByDescending(n => n.CreatedAt)
                .Select(n => new RideNotificationDto
                {
                    Event = n.Event,
                    Channel = n.Channel,
                    Recipient = n.Recipient,
                    Success = n.Success,
                    Error = n.Error,
                    CreatedAt = n.CreatedAt,
                })
                .ToListAsync();
            return Ok(items);
        }
    }

    public class NotificationSettingsDto
    {
        public bool Enabled { get; set; }
        public bool EmailChannel { get; set; }
        public bool SmsChannel { get; set; }
    }

    public class NotificationSettingsRequest
    {
        public bool Enabled { get; set; }
    }

    public class RideNotificationDto
    {
        public string Event { get; set; } = "";
        public string Channel { get; set; } = "";
        public string Recipient { get; set; } = "";
        public bool Success { get; set; }
        public string Error { get; set; } = "";
        public DateTime CreatedAt { get; set; }
    }
}
