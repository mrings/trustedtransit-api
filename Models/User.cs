namespace TrustedTransit.Api.Models
{
    public class User
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public string Email { get; set; } = string.Empty;
        public string Auth0Id { get; set; } = string.Empty;
        public string Role { get; set; } = "user";
        public string Status { get; set; } = "active";

        // The facility this user belongs to (staff). Populated on first login by matching
        // the user's verified email domain against a FacilityDomain; null until matched.
        // Relationship configured in TrustedTransitDbContext.OnModelCreating.
        public Guid? FacilityId { get; set; }
        public Facility? Facility { get; set; }

        // The transport company this user administers. Mutually exclusive with FacilityId —
        // a user is either facility staff or NEMT-company staff, never both. Set only by
        // POST /api/companies (the creator becomes admin); never by domain auto-match (that
        // path makes someone a driver on the roster, not a company admin — see Driver below).
        public Guid? TransportCompanyId { get; set; }
        public TransportCompany? TransportCompany { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    }
}