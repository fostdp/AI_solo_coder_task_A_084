
using Microsoft.EntityFrameworkCore;
using TextileMonitoring.ZigBeeIngest.Models;

namespace TextileMonitoring.ZigBeeIngest.Data
{
    public class ApplicationDbContext : DbContext
    {
        public DbSet<Sensor> Sensors => Set<Sensor>();
        public DbSet<Textile> Textiles => Set<Textile>();

        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
            : base(options)
        {
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<Sensor>(entity =>
            {
                entity.HasIndex(e => e.SensorCode).IsUnique();
                entity.HasIndex(e => new { e.SensorType, e.IsActive });
            });

            modelBuilder.Entity<Textile>(entity =>
            {
                entity.Property(e => e.AreaCm2)
                    .HasComputedColumnSql("[WidthCm] * [HeightCm]");
            });
        }
    }
}
