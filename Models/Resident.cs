using System.ComponentModel.DataAnnotations.Schema;

namespace TrustedTransit.Api.Models
{
    public class Resident
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        
        [ForeignKey("Facility")]
        public Guid FacilityId { get; set; }
        public Facility Facility { get; set; }
        
        public string FirstName { get; set; }
        public string LastName { get; set; }
        public string Phone { get; set; }
        public DateTime DateOfBirth { get; set; }
        public string MobilityRequirements { get; set; }
        public string MedicalInfo { get; set; }
        public string Notes { get; set; }
        public string FamilyEmail { get; set; }
        public string Status { get; set; } = "active";
        
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    }
}