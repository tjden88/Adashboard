using Adashboard.Models.Dashboard;
using Microsoft.EntityFrameworkCore;

namespace Adashboard.Data;

/// <summary>
/// Контекст базы данных dashboard.
/// </summary>
public sealed class DashboardDbContext(DbContextOptions<DashboardDbContext> options) : DbContext(options)
{
    /// <summary>
    /// Раскладки dashboard.
    /// </summary>
    public DbSet<DashboardLayout> Layouts => Set<DashboardLayout>();

    /// <summary>
    /// Категории ссылок.
    /// </summary>
    public DbSet<DashboardCategory> Categories => Set<DashboardCategory>();

    /// <summary>
    /// Карточки ссылок.
    /// </summary>
    public DbSet<DashboardCard> Cards => Set<DashboardCard>();

    /// <summary>
    /// Конфигурирует схему таблиц и связи между сущностями.
    /// </summary>
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<DashboardLayout>(builder =>
        {
            builder.ToTable("DashboardLayouts");
            builder.HasKey(x => x.Id);
            builder.Property(x => x.Id).ValueGeneratedNever();
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
            builder.Property(x => x.Id).ValueGeneratedNever();
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
            builder.Property(x => x.Id).ValueGeneratedNever();
            builder.Property(x => x.Title).HasMaxLength(120);
            builder.Property(x => x.Url).HasMaxLength(500);
            builder.Property(x => x.IconClass).HasMaxLength(120);
            builder.Property(x => x.BackgroundColor).HasMaxLength(32);

            builder.OwnsOne(x => x.Position, positionBuilder =>
            {
                positionBuilder.Property(x => x.Order).HasColumnName("DisplayOrder");
            });
        });
    }
}
