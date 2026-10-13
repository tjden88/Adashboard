using Adashboard.Data.Entities;
using Adashboard.Domain.Models;
using Riok.Mapperly.Abstractions;

namespace Adashboard.Server.Mapping;

/// <summary>
/// Маппинг между сущностями БД (<c>*Entity</c>) и контрактными моделями.
/// </summary>
/// <remarks>
/// <para>
/// Код маппинга не пишется руками: его генерирует source generator библиотеки Riok.Mapperly
/// на этапе сборки. Частичные методы объявляются только публичными и статическими —
/// реализации появятся в сгенерированной части класса.
/// </para>
/// <para>
/// Расхождения сущностей и моделей описаны явно атрибутами <c>MapperIgnoreSource</c> и
/// <c>MapperIgnoreTarget</c>: навигации БД, которых нет в UI-моделях, и идентификаторы
/// хранения, которых нет в контрактах. Вычисляемые read-only свойства моделей Mapperly
/// как цель не трогает.
/// </para>
/// </remarks>
[Mapper]
public static partial class Mapper
{
    /// <summary>
    /// Преобразует сущность раскладки в плоскую контрактную модель.
    /// </summary>
    /// <remarks>
    /// Переносятся только собственные поля раскладки: навигация на категории игнорируется,
    /// поскольку вложенную read-модель сервис собирает отдельно.
    /// </remarks>
    [MapperIgnoreSource(nameof(DashboardLayoutEntity.Categories))]
    public static partial DashboardLayout ToModel(DashboardLayoutEntity entity);

    /// <summary>
    /// Преобразует сущность категории в плоскую контрактную модель.
    /// </summary>
    /// <remarks>
    /// Навигации сущности (<c>Layout</c> и <c>Cards</c>) в плоской модели отсутствуют,
    /// поэтому явно игнорируются: иначе Mapperly сообщает о неотображённых членах.
    /// </remarks>
    [MapperIgnoreSource(nameof(DashboardCategoryEntity.Layout))]
    [MapperIgnoreSource(nameof(DashboardCategoryEntity.Cards))]
    public static partial DashboardCategory ToModel(DashboardCategoryEntity entity);

    /// <summary>
    /// Преобразует плоскую контрактную модель категории в сущность.
    /// </summary>
    /// <remarks>
    /// Навигации сущности в модели отсутствуют и потому игнорируются;
    /// связанные карточки и родительскую раскладку задают отдельные операции.
    /// </remarks>
    [MapperIgnoreTarget(nameof(DashboardCategoryEntity.Layout))]
    [MapperIgnoreTarget(nameof(DashboardCategoryEntity.Cards))]
    public static partial DashboardCategoryEntity ToEntity(DashboardCategory category);

    /// <summary>
    /// Копирует изменяемые поля контрактной модели категории в отслеживаемую сущность.
    /// </summary>
    /// <remarks>
    /// Идентификаторы хранения и навигации не затрагиваются: их меняют отдельные операции.
    /// </remarks>
    [MapperIgnoreTarget(nameof(DashboardCategoryEntity.Id))]
    [MapperIgnoreTarget(nameof(DashboardCategoryEntity.DashboardLayoutId))]
    [MapperIgnoreTarget(nameof(DashboardCategoryEntity.Layout))]
    [MapperIgnoreTarget(nameof(DashboardCategoryEntity.Cards))]
    [MapperIgnoreSource(nameof(DashboardCategory.Id))]
    [MapperIgnoreSource(nameof(DashboardCategory.DashboardLayoutId))]
    public static partial void UpdateCategory(DashboardCategory source, DashboardCategoryEntity target);

    /// <summary>
    /// Преобразует сущность карточки в контрактную модель.
    /// </summary>
    [MapperIgnoreSource(nameof(DashboardCardEntity.Category))]
    public static partial DashboardCard ToModel(DashboardCardEntity entity);

    /// <summary>
    /// Преобразует контрактную модель карточки в сущность.
    /// </summary>
    [MapperIgnoreTarget(nameof(DashboardCardEntity.Category))]
    public static partial DashboardCardEntity ToEntity(DashboardCard card);

    /// <summary>
    /// Копирует изменяемые поля контрактной модели карточки в отслеживаемую сущность.
    /// </summary>
    /// <remarks>
    /// Идентификатор хранения и категория не затрагиваются: категорию контроллер задаёт отдельно.
    /// </remarks>
    [MapperIgnoreTarget(nameof(DashboardCardEntity.Id))]
    [MapperIgnoreTarget(nameof(DashboardCardEntity.DashboardCategoryId))]
    [MapperIgnoreTarget(nameof(DashboardCardEntity.Category))]
    [MapperIgnoreSource(nameof(DashboardCard.Id))]
    [MapperIgnoreSource(nameof(DashboardCard.DashboardCategoryId))]
    public static partial void UpdateCard(DashboardCard source, DashboardCardEntity target);

    /// <summary>
    /// Преобразует сущность настроек в контрактную модель.
    /// </summary>
    [MapperIgnoreSource(nameof(SiteSettingsEntity.Id))]
    public static partial SiteSettings ToModel(SiteSettingsEntity entity);

    /// <summary>
    /// Преобразует контрактную модель настроек в сущность.
    /// </summary>
    [MapperIgnoreTarget(nameof(SiteSettingsEntity.Id))]
    public static partial SiteSettingsEntity ToEntity(SiteSettings settings);

    /// <summary>
    /// Копирует поля контрактной модели настроек в отслеживаемую сущность.
    /// </summary>
    [MapperIgnoreTarget(nameof(SiteSettingsEntity.Id))]
    public static partial void UpdateSettings(SiteSettings source, SiteSettingsEntity target);
}
