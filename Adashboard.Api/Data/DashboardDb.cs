using Adashboard.Domain.Models;
using Microsoft.EntityFrameworkCore;

namespace Adashboard.Api.Data;

/// <summary>
/// Контекст базы данных dashboard.
/// </summary>
public sealed class DashboardDb(DbContextOptions<DashboardDb> options) : DbContext(options)
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

}
