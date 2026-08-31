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

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);
            
            modelBuilder.Entity<Ride>()
                .HasIndex(r => new { r.FacilityId, r.ScheduledPickupTime });
            
            modelBuilder.Entity<Ride>()
                .HasIndex(r => new { r.DriverId, r.Status });
        }
    }
}