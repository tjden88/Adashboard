using Adashboard.Domain.Contracts;
using Adashboard.Domain.Reference;

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

    /// <summary>
    /// Заголовок dashboard для шапки и вкладки браузера: заголовок страницы, а при пустом значении — имя приложения.
    /// </summary>
    public static string DisplayTitle(this DashboardPage page) => string.IsNullOrWhiteSpace(page.Title) ? DomainConstants.AppTitle : page.Title;

    /// <summary>
    /// Признак основной страницы: она всегда первая в меню и открывается по корневому адресу.
    /// </summary>
    public static bool IsMainPage(this DashboardPage page) => page.Id == 1;
}
