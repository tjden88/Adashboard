using Adashboard.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace Adashboard.Data;

/// <summary>
/// Контекст базы данных приложения.
/// </summary>
public sealed class DashboardDb(DbContextOptions<DashboardDb> options) : DbContext(options)
{
    /// <summary>
    /// Страницы приложения.
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
    /// Общие настройки приложения (одиночная строка).
    /// </summary>
    public DbSet<AppSettingsEntity> AppSettings => Set<AppSettingsEntity>();

    /// <summary>
    /// Настраивает схему БД, которую не выразить атрибутами сущностей.
    /// </summary>
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.Entity<AppSettingsEntity>(entity =>
        {
            entity.HasData(new AppSettingsEntity
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
