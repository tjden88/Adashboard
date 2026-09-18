using Adashboard.Models.Dashboard;
using Microsoft.EntityFrameworkCore;

namespace Adashboard.Data;

public sealed class DashboardDbContext(DbContextOptions<DashboardDbContext> options) : DbContext(options)
{
    public DbSet<DashboardLayout> Layouts => Set<DashboardLayout>();

    public DbSet<DashboardCategory> Categories => Set<DashboardCategory>();

    public DbSet<DashboardCard> Cards => Set<DashboardCard>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<DashboardLayout>(builder =>
        {
            builder.ToTable("DashboardLayouts");
            builder.HasKey(x => x.Id);
            builder.Property(x => x.Id).HasMaxLength(80);
            builder.Property(x => x.ThemeMode).HasMaxLength(16);

            builder.HasMany(x => x.Categories)
                .WithOne()
                .HasForeignKey(x => x.DashboardLayoutId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<DashboardCategory>(builder =>
        {
            builder.ToTable("DashboardCategories");
            builder.HasKey(x => x.Id);
            builder.Property(x => x.Id).HasMaxLength(80);
            builder.Property(x => x.DashboardLayoutId).HasMaxLength(80);
            builder.Property(x => x.Title).HasMaxLength(120);

            builder.OwnsOne(x => x.Position, positionBuilder =>
            {
                positionBuilder.Property(x => x.Order).HasColumnName("DisplayOrder");
                positionBuilder.Property(x => x.Width).HasColumnName("DisplayWidth");
            });

            builder.HasMany(x => x.Cards)
                .WithOne()
                .HasForeignKey(x => x.CategoryId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<DashboardCard>(builder =>
        {
            builder.ToTable("DashboardCards");
            builder.HasKey(x => x.Id);
            builder.Property(x => x.Id).HasMaxLength(80);
            builder.Property(x => x.CategoryId).HasMaxLength(80);
            builder.Property(x => x.Title).HasMaxLength(120);
            builder.Property(x => x.Url).HasMaxLength(500);
            builder.Property(x => x.IconClass).HasMaxLength(120);
            builder.Property(x => x.IconColor).HasMaxLength(32);

            builder.OwnsOne(x => x.Position, positionBuilder =>
            {
                positionBuilder.Property(x => x.Order).HasColumnName("DisplayOrder");
            });
        });
    }
}
