using System.ComponentModel.DataAnnotations.Schema;

namespace TrustedTransit.Api.Models
{
    /// <summary>
    /// A non-emergency medical transportation (NEMT) company — a separate business from any
    /// facility, with its own admin and its own roster of drivers (see Driver.TransportCompanyId).
    /// Unlike Facility, a company doesn't subscribe/pay TrustedTransit — it's a supplier, not a
    /// paying customer, so there's no billing here.
    /// </summary>
    public class TransportCompany
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public string Name { get; set; } = string.Empty;
        public string Address { get; set; } = string.Empty;
        public string City { get; set; } = string.Empty;
        public string State { get; set; } = string.Empty;
        public string Zip { get; set; } = string.Empty;
        public string Phone { get; set; } = string.Empty;

        // pending | active | rejected. New companies start pending; a super admin vets them
        // (SupportController). Only "active" companies accept new drivers via domain auto-match
        // (BaseController.MatchTransportCompanyByEmailDomainAsync) — vetting gates onboarding,
        // not the company's own ability to set up its profile/roster while under review.
        public string Status { get; set; } = "pending";

        // Verified staff (driver) email domains — see TransportCompanyDomain.
        public ICollection<TransportCompanyDomain> Domains { get; set; } = new List<TransportCompanyDomain>();

        [ForeignKey("User")]
        public Guid? ContactUserId { get; set; }
        public User? User { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

        public ICollection<Driver> Drivers { get; set; } = new List<Driver>();
    }
}
