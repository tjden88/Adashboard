using System.ComponentModel.DataAnnotations;
using Adashboard.Domain.Enums;
using Adashboard.Domain.Reference;

namespace Adashboard.Data.Entities;

/// <summary>
/// Сущность БД общих настроек приложения.
/// </summary>
/// <remarks>
/// Настройки хранятся одной строкой с фиксированным идентификатором <c>Id = 1</c>.
/// </remarks>
public sealed class AppSettingsEntity
{
    /// <summary>
    /// Идентификатор записи настроек. Всегда равен 1 (одиночная строка).
    /// </summary>
    [Key]
    public int Id { get; set; }

    /// <summary>Режим темы: auto, dark или light.</summary>
    public ThemeMode ThemeMode { get; set; } = ThemeMode.Auto;

    /// <summary>Режим бокового меню страниц: скрыто, свёрнуто, автосворачивание или всегда развёрнуто.</summary>
    public SideMenuMode SideMenuMode { get; set; } = SideMenuMode.AutoCollapse;

    /// <summary>Признак отображения иконки в шапке: иконка страницы или, если у страницы своей иконки нет, иконка приложения.</summary>
    public bool ShowPageIcon { get; set; } = true;

    /// <summary>Выравнивание заголовка и описания страницы в шапке: по левому краю или по центру.</summary>
    public PageHeaderAlignment HeaderAlignment { get; set; } = PageHeaderAlignment.Left;

    /// <summary>Интервал проверки статуса карточек в секундах.</summary>
    public int HealthCheckIntervalSeconds { get; set; } = DomainConstants.DefaultHealthCheckIntervalSeconds;
}
