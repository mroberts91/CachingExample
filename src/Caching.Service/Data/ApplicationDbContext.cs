
using Microsoft.EntityFrameworkCore;

namespace Caching.Service.Data;
public class ApplicationDbContext : DbContext
{
    public DbSet<CityData> CityData { get; set; }

    public ApplicationDbContext(DbContextOptions options) : base(options) { }

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        builder.Entity<CityData>(e =>
        {
            e.ToTable("city_data");
            e.HasKey(o => o.ZipCode);
            e.Property(o => o.ZipCode)
                .HasColumnName("Zipcode");
            e.Property(o => o.Latitude)
                .HasColumnName("Lattatude");
            e.Property(o => o.StateAbbreviation)
                .HasColumnName("Abbr");
        });
    }
}
