using JobRadar.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace JobRadar.Infrastructure.Data;

/// <summary>
/// EF Core DbContext — configurado via Fluent API (sem Data Annotations nas entidades de domínio).
/// </summary>
public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<JobResult> JobResults { get; set; }
    public DbSet<SearchHistory> SearchHistories { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<JobResult>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.Title).IsRequired().HasMaxLength(500);
            e.Property(x => x.Url).IsRequired().HasMaxLength(1000);
            e.Property(x => x.Keywords).HasMaxLength(500);
            e.HasIndex(x => x.Url);
            e.HasIndex(x => x.PublishedAt);
        });

        modelBuilder.Entity<SearchHistory>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.Keywords).IsRequired().HasMaxLength(500);
            e.HasIndex(x => x.SearchedAt);
        });
    }
}
