namespace TrustedTransit.Api.Models
{
    public class User
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public string Email { get; set; } = string.Empty;
        public string Auth0Id { get; set; } = string.Empty;
        public string Role { get; set; } = "user";
        public string Status { get; set; } = "active";

        // The facility this user belongs to. Populated on first login by matching
        // the user's verified email domain against Facility.EmailDomain; null until matched.
        // Relationship configured in TrustedTransitDbContext.OnModelCreating.
        public Guid? FacilityId { get; set; }
        public Facility? Facility { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    }
}