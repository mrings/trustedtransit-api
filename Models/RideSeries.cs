using System.ComponentModel.DataAnnotations.Schema;

namespace TrustedTransit.Api.Models
{
    /// <summary>
    /// A recurring-ride pattern. Individual <see cref="Ride"/> rows are generated from it for a
    /// rolling window (see RideSeriesController.TopUpAsync).
    /// </summary>
    public class RideSeries
    {
        public Guid Id { get; set; } = Guid.NewGuid();

        [ForeignKey("Facility")]
        public Guid FacilityId { get; set; }
        public Facility? Facility { get; set; }

        [ForeignKey("Resident")]
        public Guid ResidentId { get; set; }
        public Resident? Resident { get; set; }

        [ForeignKey("Driver")]
        public Guid? DriverId { get; set; }
        public Driver? Driver { get; set; }

        public string PickupAddress { get; set; } = string.Empty;
        public string DestinationAddress { get; set; } = string.Empty;
        public string AppointmentType { get; set; } = string.Empty;

        /// <summary>Days of week the ride runs, as a sorted CSV of ints (0=Sunday … 6=Saturday), e.g. "1,3,5".</summary>
        public string DaysOfWeek { get; set; } = string.Empty;

        /// <summary>Local pickup time of day (treated as UTC for now — facility timezones are a later refinement).</summary>
        public TimeOnly PickupTime { get; set; }

        public DateOnly StartDate { get; set; }
        public DateOnly? EndDate { get; set; }

        public bool Active { get; set; } = true;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

        public ICollection<Ride> Rides { get; set; } = new List<Ride>();

        public IEnumerable<DayOfWeek> Days() =>
            DaysOfWeek.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Select(s => int.TryParse(s, out var n) ? n : -1)
                .Where(n => n is >= 0 and <= 6)
                .Select(n => (DayOfWeek)n)
                .Distinct();
    }
}
