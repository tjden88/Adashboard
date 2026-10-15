using Adashboard.Domain.Contracts;
using Adashboard.Domain.Reference;

namespace Adashboard.Client.Models;

/// <summary>
/// Изменяемая модель данных текущей страницы dashboard: то, что получают шапка, представление
/// страницы, категории и карточки.
/// </summary>
public sealed class DashboardPageModel
{
    /// <summary>
    /// Происходит при изменении текущей страницы
    /// </summary>
    public event Action? PageInfoChanged;

    /// <summary>
    /// Информационная часть текущей страницы.
    /// </summary>
    public DashboardPage Page
    {
        get;
        set
        {
            if(Equals(field, value))
                return;
            field = value;
            PageInfoChanged?.Invoke();
        }
    } = new();

    /// <summary>
    /// Список всех страниц dashboard: нужен для поиска страницы по пути и выбора страницы в диалогах.
    /// </summary>
    public List<DashboardPage> Pages { get; set; } = [];

    /// <summary>
    /// Категории текущей страницы.
    /// </summary>
    public List<DashboardCategory> Categories { get; set; } = [];

    /// <summary>
    /// Карточки категорий текущей страницы.
    /// </summary>
    public List<DashboardCard> Cards { get; set; } = [];

    /// <summary>
    /// Заголовок dashboard для шапки и вкладки браузера: заголовок текущей страницы,
    /// а при пустом значении — имя приложения.
    /// </summary>
    public string DisplayTitle => string.IsNullOrWhiteSpace(Page.Title) ? DomainConstants.AppTitle : Page.Title;

    /// <summary>
    /// Признак главной страницы
    /// </summary>
    public bool IsMainPage => Page.Id == 1;

    /// <summary>
    /// Уведомить об изменении текущей страницы
    /// </summary>
    public void NotifyPageChanged() => PageInfoChanged?.Invoke();
}
