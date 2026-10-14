using System.ComponentModel.DataAnnotations;
using Adashboard.Domain.ReadModels;

namespace Adashboard.Domain.Contracts;

/// <summary>
/// Контрактная модель категории ссылок на dashboard: то, что получает и отправляет фронтенд.
/// </summary>
/// <remarks>
/// UI-модель: навигации и атрибуты схемы БД живут в сущности <c>DashboardCategoryEntity</c>,
/// а здесь остаются только поля, нужные интерфейсу, и атрибуты валидации.
/// Плоская модель без дочерних карточек: и в запросах, и в ответах категория идёт сама по себе.
/// Карточки категории отдаёт read-модель <see cref="DashboardPageReadModel"/>.
/// </remarks>
public sealed class DashboardCategory
{
    /// <summary>
    /// Уникальный идентификатор категории.
    /// </summary>
    public int Id { get; set; }

    /// <summary>
    /// Идентификатор родительской страницы.
    /// </summary>
    public int DashboardPageId { get; set; }

    /// <summary>
    /// Отображаемое название категории.
    /// </summary>
    [MaxLength(120)]
    public string Title { get; set; } = string.Empty;

    /// <summary>
    /// Порядок категории среди остальных.
    /// </summary>
    public int Order { get; set; }

    /// <summary>
    /// Ширина категории в колонках сетки.
    /// </summary>
    public int Width { get; set; } = 6;
}
