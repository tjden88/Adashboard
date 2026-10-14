using System.ComponentModel.DataAnnotations;
using Adashboard.Domain.Enums;
using Adashboard.Domain.Reference;

namespace Adashboard.Data.Entities;

/// <summary>
/// Сущность БД общих настроек dashboard.
/// </summary>
/// <remarks>
/// Настройки хранятся одной строкой с фиксированным идентификатором <c>Id = 1</c>.
/// </remarks>
public sealed class SiteSettingsEntity
{
    /// <summary>
    /// Идентификатор записи настроек. Всегда равен 1 (одиночная строка).
    /// </summary>
    [Key]
    public int Id { get; set; }

    /// <summary>Режим темы: auto, dark или light.</summary>
    public ThemeMode ThemeMode { get; set; } = ThemeMode.Auto;

    /// <summary>Признак отображения логотипа в шапке dashboard.</summary>
    public bool ShowLogo { get; set; } = true;

    /// <summary>Интервал проверки статуса карточек в секундах.</summary>
    public int HealthCheckIntervalSeconds { get; set; } = DomainConstants.DefaultHealthCheckIntervalSeconds;
}
