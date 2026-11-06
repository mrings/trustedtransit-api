using System.ComponentModel.DataAnnotations.Schema;

namespace TrustedTransit.Api.Models
{
    /// <summary>
    /// A facility's own pick from the shared, vetted driver/company pool — "we work with this
    /// one." Exactly one of <see cref="DriverId"/> (an independent driver) or
    /// <see cref="TransportCompanyId"/> (that company's whole, dynamic roster) is set. Replaces
    /// the old "any admin can see/edit every driver platform-wide" behavior: a facility's
    /// Drivers tab now shows only what it has explicitly added here.
    /// </summary>
    public class FacilityProvider
    {
        public Guid Id { get; set; } = Guid.NewGuid();

        [ForeignKey("Facility")]
        public Guid FacilityId { get; set; }
        public Facility? Facility { get; set; }

        [ForeignKey("Driver")]
        public Guid? DriverId { get; set; }
        public Driver? Driver { get; set; }

        [ForeignKey("TransportCompany")]
        public Guid? TransportCompanyId { get; set; }
        public TransportCompany? TransportCompany { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}
