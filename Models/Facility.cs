namespace TrustedTransit.Api.Models
{
    public class Facility
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public string Name { get; set; } = string.Empty;
        public string Address { get; set; } = string.Empty;
        public string City { get; set; } = string.Empty;
        public string State { get; set; } = string.Empty;
        public string Zip { get; set; } = string.Empty;
        public string Phone { get; set; } = string.Empty;
        public string SubscriptionTier { get; set; } = "starter";       // Plans.All keys
        public string SubscriptionStatus { get; set; } = "trial";       // trial | active | canceled | past_due
        public DateTime? TrialEndsAt { get; set; }
        public DateTime? SubscriptionRenewsAt { get; set; }
        public string StripeCustomerId { get; set; } = string.Empty;

        // Verified email domain of this facility's staff (e.g. "sunriseseniorliving.com"),
        // lowercased, no "@". Unique across facilities; null = no auto-join by domain.
        public string? EmailDomain { get; set; }
        
        // Relationship configured in TrustedTransitDbContext.OnModelCreating.
        public Guid? ContactUserId { get; set; }
        public User? User { get; set; }
        
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
        
        public ICollection<Ride> Rides { get; set; } = new List<Ride>();
    }
}