using Microsoft.EntityFrameworkCore;
using TextileMonitoring.PopulationSim.Models;

namespace TextileMonitoring.PopulationSim.Data
{
    public class ApplicationDbContext : DbContext
    {
        public DbSet<Textile> Textiles => Set<Textile>();
        public DbSet<HistoricalDustData> HistoricalDustData => Set<HistoricalDustData>();

        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
            : base(options)
        {
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<Textile>(entity =>
            {
                entity.HasIndex(e => e.Dynasty);
                entity.HasIndex(e => e.Status);
                entity.Property(e => e.AreaCm2)
                    .HasComputedColumnSql("[WidthCm] * [HeightCm]");
            });

            modelBuilder.Entity<HistoricalDustData>(entity =>
            {
                entity.HasIndex(e => new { e.SensorId, e.ReadingTime });
                entity.HasIndex(e => new { e.TextileId, e.ReadingTime });
            });
        }
    }
}
