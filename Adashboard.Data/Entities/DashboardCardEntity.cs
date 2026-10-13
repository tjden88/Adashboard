using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Adashboard.Domain.Enums;

namespace Adashboard.Data.Entities;

/// <summary>
/// Сущность БД карточки ссылки внутри категории.
/// </summary>
/// <remarks>
/// Ориентирована на EF Core: содержит навигацию на родительскую категорию и атрибуты схемы.
/// </remarks>
public sealed class DashboardCardEntity
{
    /// <summary>
    /// Уникальный идентификатор карточки.
    /// </summary>
    public int Id { get; set; }

    /// <summary>
    /// Идентификатор категории, к которой принадлежит карточка.
    /// </summary>
    public int DashboardCategoryId { get; set; }

    /// <summary>
    /// Отображаемое название карточки.
    /// </summary>
    [MaxLength(30)]
    public string Title { get; set; } = string.Empty;

    /// <summary>
    /// Адрес перехода по карточке (без схемы).
    /// </summary>
    [MaxLength(500)]
    public string Url { get; set; } = string.Empty;

    /// <summary>
    /// Схема, добавляемая к адресу при открытии: "http://", "https://" или пустая строка.
    /// </summary>
    public UrlScheme UrlScheme { get; set; } = UrlScheme.Http;

    /// <summary>
    /// Признак того, что изображение-иконка загружено пользователем и хранится в uploads.
    /// Иначе является стандартной иконкой Font Awesome.
    /// </summary>
    public bool IsCustomIcon { get; set; }

    /// <summary>
    /// CSS-класс иконки Font Awesome либо относительный путь к загруженному изображению-иконке.
    /// </summary>
    public string IconSource { get; set; } = "fa-solid fa-link";

    /// <summary>
    /// Цвет фона карточки в формате CSS (например, #1e293b).
    /// </summary>
    public string BackgroundColor { get; set; } = "#1e40af";

    /// <summary>
    /// Признак широкой карточки: занимает две колонки вместо одной.
    /// </summary>
    public bool IsWide { get; set; }

    /// <summary>
    /// Режим проверки доступности сервиса. Определяет, проверять ли статус и что считать успехом.
    /// </summary>
    public CardStatusMode StatusMode { get; set; } = CardStatusMode.None;

    /// <summary>
    /// Положение карточки внутри категории.
    /// </summary>
    public int Position { get; set; }

    /// <summary>
    /// Родительская категория (навигация).
    /// </summary>
    [ForeignKey(nameof(DashboardCategoryId))]
    public DashboardCategoryEntity? Category { get; set; }
}
