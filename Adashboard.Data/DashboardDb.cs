using Adashboard.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace Adashboard.Data;

/// <summary>
/// Контекст базы данных dashboard.
/// </summary>
public sealed class DashboardDb(DbContextOptions<DashboardDb> options) : DbContext(options)
{
    /// <summary>
    /// Страницы dashboard.
    /// </summary>
    public DbSet<DashboardPageEntity> Pages => Set<DashboardPageEntity>();

    /// <summary>
    /// Категории ссылок.
    /// </summary>
    public DbSet<DashboardCategoryEntity> Categories => Set<DashboardCategoryEntity>();

    /// <summary>
    /// Карточки ссылок.
    /// </summary>
    public DbSet<DashboardCardEntity> Cards => Set<DashboardCardEntity>();

    /// <summary>
    /// Общие настройки dashboard (одиночная строка).
    /// </summary>
    public DbSet<SiteSettingsEntity> SiteSettings => Set<SiteSettingsEntity>();

    /// <summary>
    /// Настраивает схему БД, которую не выразить атрибутами сущностей.
    /// </summary>
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.Entity<SiteSettingsEntity>(entity =>
        {
            entity.HasData(new SiteSettingsEntity
            {
                Id = 1,
            });
        });

        // Путь страницы уникален: по нему страница открывается в клиенте.
        modelBuilder.Entity<DashboardPageEntity>(entity =>
        {
            entity.HasIndex(x => x.Path).IsUnique();
        });
    }
}
