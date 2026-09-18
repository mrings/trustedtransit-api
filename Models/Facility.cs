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
        public bool CancelAtPeriodEnd { get; set; }
        public string StripeCustomerId { get; set; } = string.Empty;
        public string StripeSubscriptionId { get; set; } = string.Empty;

        // Verified staff email domains — see FacilityDomain. A facility can hold several
        // (e.g. "acme.com" and "acme-senior.com").
        public ICollection<FacilityDomain> Domains { get; set; } = new List<FacilityDomain>();

        // Send ride-status notifications to residents' families.
        public bool NotificationsEnabled { get; set; } = true;
        
        // Relationship configured in TrustedTransitDbContext.OnModelCreating.
        public Guid? ContactUserId { get; set; }
        public User? User { get; set; }
        
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
        
        public ICollection<Ride> Rides { get; set; } = new List<Ride>();
    }
}