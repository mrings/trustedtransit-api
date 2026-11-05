namespace TrustedTransit.Api.Models
{
    /// <summary>
    /// A record of one super-admin action — who did it, to what, and when. Written only by
    /// SupportController; nothing else performs actions that need auditing yet.
    /// </summary>
    public class AuditLog
    {
        public Guid Id { get; set; } = Guid.NewGuid();

        public Guid ActorUserId { get; set; }
        public string ActorEmail { get; set; } = string.Empty;

        public string Action { get; set; } = string.Empty;      // e.g. impersonate_facility, vet_driver
        public string TargetType { get; set; } = string.Empty;  // facility | driver | transport_company
        public Guid? TargetId { get; set; }
        public string Detail { get; set; } = string.Empty;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}
