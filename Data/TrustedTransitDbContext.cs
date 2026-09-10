using Microsoft.EntityFrameworkCore;
using TrustedTransit.Api.Models;

namespace TrustedTransit.Api.Data
{
    public class TrustedTransitDbContext : DbContext
    {
        public TrustedTransitDbContext(DbContextOptions<TrustedTransitDbContext> options) : base(options) { }

        public DbSet<User> Users { get; set; }
        public DbSet<Facility> Facilities { get; set; }
        public DbSet<Resident> Residents { get; set; }
        public DbSet<Driver> Drivers { get; set; }
        public DbSet<Ride> Rides { get; set; }
        public DbSet<RideSeries> RideSeries { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);
            
            modelBuilder.Entity<Ride>()
                .HasIndex(r => new { r.FacilityId, r.ScheduledPickupTime });

            modelBuilder.Entity<Ride>()
                .HasIndex(r => new { r.DriverId, r.Status });

            // One facility per email domain (Postgres treats multiple NULLs as distinct,
            // so facilities without a domain are unaffected).
            modelBuilder.Entity<Facility>()
                .HasIndex(f => f.EmailDomain)
                .IsUnique();

            // Facility and User reference each other (User.FacilityId = the user's facility;
            // Facility.ContactUserId = that facility's contact). Configure both as many-to-one
            // with no inverse navigation so EF doesn't collapse them into one 1:1 relationship
            // (which would force a unique index on ContactUserId).
            modelBuilder.Entity<User>()
                .HasOne(u => u.Facility)
                .WithMany()
                .HasForeignKey(u => u.FacilityId)
                .OnDelete(DeleteBehavior.ClientSetNull);

            modelBuilder.Entity<Facility>()
                .HasOne(f => f.User)
                .WithMany()
                .HasForeignKey(f => f.ContactUserId)
                .OnDelete(DeleteBehavior.ClientSetNull);

            modelBuilder.Entity<RideSeries>()
                .HasIndex(s => new { s.FacilityId, s.Active });

            // Deleting a series leaves its generated rides in place (RideSeriesId set null).
            modelBuilder.Entity<Ride>()
                .HasOne(r => r.RideSeries)
                .WithMany(s => s.Rides)
                .HasForeignKey(r => r.RideSeriesId)
                .OnDelete(DeleteBehavior.SetNull);
        }
    }
}