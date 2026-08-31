using System.ComponentModel.DataAnnotations.Schema;

namespace TrustedTransit.Api.Models
{
    public class Ride
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        
        [ForeignKey("Facility")]
        public Guid FacilityId { get; set; }
        public Facility Facility { get; set; }
        
        [ForeignKey("Resident")]
        public Guid ResidentId { get; set; }
        public Resident Resident { get; set; }
        
        [ForeignKey("Driver")]
        public Guid? DriverId { get; set; }
        public Driver Driver { get; set; }
        
        public DateTime ScheduledPickupTime { get; set; }
        public DateTime? ActualPickupTime { get; set; }
        public DateTime? ActualDropoffTime { get; set; }
        
        public string PickupAddress { get; set; }
        public decimal? PickupLat { get; set; }
        public decimal? PickupLng { get; set; }
        
        public string DestinationAddress { get; set; }
        public decimal? DestinationLat { get; set; }
        public decimal? DestinationLng { get; set; }
        
        public string RideType { get; set; } = "one-time";
        public string AppointmentType { get; set; }
        
        public bool WheelchairRequired { get; set; } = false;
        public bool EscortRequired { get; set; } = false;
        public string SpecialInstructions { get; set; }
        
        public string Status { get; set; } = "scheduled";
        
        [Column(TypeName = "numeric(8,2)")]
        public decimal BaseFare { get; set; } = 10.00m;
        
        [Column(TypeName = "numeric(8,2)")]
        public decimal MileageCharge { get; set; } = 0;
        
        [Column(TypeName = "numeric(8,2)")]
        public decimal TotalCharge { get; set; } = 10.00m;
        
        public string PaymentStatus { get; set; } = "pending";
        
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? CompletedAt { get; set; }
    }
}