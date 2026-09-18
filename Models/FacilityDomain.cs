using System.ComponentModel.DataAnnotations.Schema;

namespace TrustedTransit.Api.Models
{
    /// <summary>
    /// A verified email domain for a facility's staff. Anyone signing in with a verified email
    /// at this domain auto-joins the facility. A domain can belong to at most one facility.
    /// </summary>
    public class FacilityDomain
    {
        public Guid Id { get; set; } = Guid.NewGuid();

        [ForeignKey("Facility")]
        public Guid FacilityId { get; set; }
        public Facility? Facility { get; set; }

        /// <summary>Lowercased, no "@" — see BaseController.NormalizeEmailDomain.</summary>
        public string Domain { get; set; } = string.Empty;

        /// <summary>The admin's email at the time they added this domain (proof they hold it).</summary>
        public string AddedByEmail { get; set; } = string.Empty;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}
