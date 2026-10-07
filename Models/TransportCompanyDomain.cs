using System.ComponentModel.DataAnnotations.Schema;

namespace TrustedTransit.Api.Models
{
    /// <summary>
    /// A verified email domain for a transport company's drivers. Anyone signing in with a
    /// verified email at this domain auto-joins the company's roster as a driver. A domain can
    /// belong to at most one company (and is tracked separately from FacilityDomain — the two
    /// pools don't cross-check each other).
    /// </summary>
    public class TransportCompanyDomain
    {
        public Guid Id { get; set; } = Guid.NewGuid();

        [ForeignKey("TransportCompany")]
        public Guid TransportCompanyId { get; set; }
        public TransportCompany? TransportCompany { get; set; }

        /// <summary>Lowercased, no "@" — see BaseController.NormalizeEmailDomain.</summary>
        public string Domain { get; set; } = string.Empty;

        /// <summary>The admin's email at the time they added this domain (proof they hold it).</summary>
        public string AddedByEmail { get; set; } = string.Empty;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}
