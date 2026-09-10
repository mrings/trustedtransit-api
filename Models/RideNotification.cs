using System.ComponentModel.DataAnnotations.Schema;

namespace TrustedTransit.Api.Models
{
    /// <summary>A record of one ride-status notification attempt (per channel/recipient).</summary>
    public class RideNotification
    {
        public Guid Id { get; set; } = Guid.NewGuid();

        [ForeignKey("Ride")]
        public Guid RideId { get; set; }
        public Ride? Ride { get; set; }

        public Guid FacilityId { get; set; }

        public string Event { get; set; } = string.Empty;    // assigned | en_route | completed | cancelled
        public string Channel { get; set; } = string.Empty;  // email | sms
        public string Recipient { get; set; } = string.Empty;
        public bool Success { get; set; }
        public string Error { get; set; } = string.Empty;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}
