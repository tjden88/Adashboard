using Adashboard.Domain.Contracts;

namespace Adashboard.Client.Helpers;

/// <summary>
/// Помощники отображения страниц dashboard в подписях интерфейса.
/// </summary>
public static class PageDisplayExtensions
{
    /// <summary>
    /// Подпись страницы для селекторов: путь показываем, чтобы различать страницы с похожими названиями.
    /// </summary>
    public static string DisplayLabel(this DashboardPage page) => page.Id == 1
        ? $"{page.Title} (основная)"
        : $"{page.Title} — /{page.Path}";
}
