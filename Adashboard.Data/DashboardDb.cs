using Adashboard.Data.Entities;
using Adashboard.Domain.Models;
using Microsoft.EntityFrameworkCore;

namespace Adashboard.Data;

/// <summary>
/// Контекст базы данных dashboard.
/// </summary>
public sealed class DashboardDb(DbContextOptions<DashboardDb> options) : DbContext(options)
{
    /// <summary>
    /// Раскладки dashboard.
    /// </summary>
    public DbSet<DashboardLayoutEntity> Layouts => Set<DashboardLayoutEntity>();

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
    }
}
