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
        public DbSet<RideNotification> RideNotifications { get; set; }
        public DbSet<FacilityDomain> FacilityDomains { get; set; }
        public DbSet<TransportCompany> TransportCompanies { get; set; }
        public DbSet<TransportCompanyDomain> TransportCompanyDomains { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);
            
            modelBuilder.Entity<Ride>()
                .HasIndex(r => new { r.FacilityId, r.ScheduledPickupTime });

            modelBuilder.Entity<Ride>()
                .HasIndex(r => new { r.DriverId, r.Status });

            // Nulls are distinct under a unique index (Postgres default), so old rides without
            // a token yet don't collide.
            modelBuilder.Entity<Ride>()
                .HasIndex(r => r.TrackingToken)
                .IsUnique();

            // One facility per email domain — a facility can hold several domains, but each
            // domain belongs to at most one facility.
            modelBuilder.Entity<FacilityDomain>()
                .HasIndex(d => d.Domain)
                .IsUnique();
            modelBuilder.Entity<FacilityDomain>()
                .HasOne(d => d.Facility)
                .WithMany(f => f.Domains)
                .HasForeignKey(d => d.FacilityId)
                .OnDelete(DeleteBehavior.Cascade);

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

            modelBuilder.Entity<RideNotification>()
                .HasIndex(n => n.RideId);
            modelBuilder.Entity<RideNotification>()
                .HasOne(n => n.Ride).WithMany()
                .HasForeignKey(n => n.RideId)
                .OnDelete(DeleteBehavior.Cascade);

            // Deleting a series leaves its generated rides in place (RideSeriesId set null).
            modelBuilder.Entity<Ride>()
                .HasOne(r => r.RideSeries)
                .WithMany(s => s.Rides)
                .HasForeignKey(r => r.RideSeriesId)
                .OnDelete(DeleteBehavior.SetNull);

            // One transport company per email domain, same posture as FacilityDomain.
            modelBuilder.Entity<TransportCompanyDomain>()
                .HasIndex(d => d.Domain)
                .IsUnique();
            modelBuilder.Entity<TransportCompanyDomain>()
                .HasOne(d => d.TransportCompany)
                .WithMany(c => c.Domains)
                .HasForeignKey(d => d.TransportCompanyId)
                .OnDelete(DeleteBehavior.Cascade);

            // TransportCompany and User reference each other the same way Facility/User do
            // (User.TransportCompanyId = the admin's company; TransportCompany.ContactUserId =
            // that company's contact) — configured explicitly so EF keeps them as two
            // relationships instead of collapsing them into one 1:1.
            modelBuilder.Entity<User>()
                .HasOne(u => u.TransportCompany)
                .WithMany()
                .HasForeignKey(u => u.TransportCompanyId)
                .OnDelete(DeleteBehavior.ClientSetNull);

            modelBuilder.Entity<TransportCompany>()
                .HasOne(c => c.User)
                .WithMany()
                .HasForeignKey(c => c.ContactUserId)
                .OnDelete(DeleteBehavior.ClientSetNull);

            // A driver's company roster membership — deleting a company unlinks its drivers
            // rather than deleting their profiles/ride history.
            modelBuilder.Entity<Driver>()
                .HasOne(d => d.TransportCompany)
                .WithMany(c => c.Drivers)
                .HasForeignKey(d => d.TransportCompanyId)
                .OnDelete(DeleteBehavior.ClientSetNull);
        }
    }
}