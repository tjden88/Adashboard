using Adashboard.Models.Dashboard;

namespace Adashboard.Data;

/// <summary>
/// Сервис работы с раскладкой dashboard.
/// </summary>
public interface IDashboardLayoutService
{
    /// <summary>
    /// Загружает основную раскладку dashboard.
    /// </summary>
    Task<DashboardLayout> GetMainLayoutAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Сохраняет основную раскладку dashboard.
    /// </summary>
    Task SaveMainLayoutAsync(DashboardLayout layout, CancellationToken cancellationToken = default);
}
