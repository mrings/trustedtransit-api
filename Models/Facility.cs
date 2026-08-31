using System.ComponentModel.DataAnnotations.Schema;

namespace TrustedTransit.Api.Models
{
    public class Facility
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public string Name { get; set; }
        public string Address { get; set; }
        public string City { get; set; }
        public string State { get; set; }
        public string Zip { get; set; }
        public string Phone { get; set; }
        public string SubscriptionTier { get; set; } = "starter";
        public string SubscriptionStatus { get; set; } = "trial";
        public string StripeCustomerId { get; set; }
        
        [ForeignKey("User")]
        public Guid? ContactUserId { get; set; }
        public User User { get; set; }
        
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
        
        public ICollection<Ride> Rides { get; set; } = new List<Ride>();
    }
}