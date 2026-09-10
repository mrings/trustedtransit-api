using System.ComponentModel.DataAnnotations.Schema;

namespace TrustedTransit.Api.Models
{
    public class Resident
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        
        [ForeignKey("Facility")]
        public Guid FacilityId { get; set; }
        public Facility? Facility { get; set; }

        public string FirstName { get; set; } = string.Empty;
        public string LastName { get; set; } = string.Empty;
        public string Phone { get; set; } = string.Empty;
        public DateTime DateOfBirth { get; set; }
        public string MobilityRequirements { get; set; } = string.Empty;
        public string MedicalInfo { get; set; } = string.Empty;
        public string Notes { get; set; } = string.Empty;
        public string FamilyEmail { get; set; } = string.Empty;
        public string FamilyPhone { get; set; } = string.Empty;
        public string Status { get; set; } = "active";
        
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    }
}