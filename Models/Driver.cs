using System.ComponentModel.DataAnnotations.Schema;

namespace TrustedTransit.Api.Models
{
    public class Driver
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        
        [ForeignKey("User")]
        public Guid UserId { get; set; }
        public User User { get; set; }
        
        public string FirstName { get; set; }
        public string LastName { get; set; }
        public string Phone { get; set; }
        public string VehicleType { get; set; }
        public string VehiclePlate { get; set; }
        public string BackgroundCheckStatus { get; set; } = "pending";
        public DateTime? BackgroundCheckDate { get; set; }
        public decimal Rating { get; set; } = 0;
        public string Status { get; set; } = "active";
        
        public decimal? LocationLat { get; set; }
        public decimal? LocationLng { get; set; }
        public DateTime? LastLocationUpdate { get; set; }
        
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    }
}