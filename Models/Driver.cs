using System.ComponentModel.DataAnnotations.Schema;

namespace TrustedTransit.Api.Models
{
    public class Driver
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        
        // Null until the driver has a login account.
        [ForeignKey("User")]
        public Guid? UserId { get; set; }
        public User? User { get; set; }

        // Null = independent driver, not affiliated with any NEMT company. Facilities assign
        // from the same shared pool either way — this is purely for the company's own roster
        // management and display, not a scheduling restriction.
        [ForeignKey("TransportCompany")]
        public Guid? TransportCompanyId { get; set; }
        public TransportCompany? TransportCompany { get; set; }

        public string FirstName { get; set; } = string.Empty;
        public string LastName { get; set; } = string.Empty;
        public string Phone { get; set; } = string.Empty;
        public string VehicleType { get; set; } = string.Empty;
        public string VehiclePlate { get; set; } = string.Empty;
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