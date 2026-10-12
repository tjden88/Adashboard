using Adashboard.Api.Data.Entities;
using Adashboard.Domain.Models;
using Riok.Mapperly.Abstractions;

namespace Adashboard.Api.Mapping;

/// <summary>
/// Маппинг между сущностями БД (<c>*Entity</c>) и контрактными моделями.
/// </summary>
/// <remarks>
/// <para>
/// Код маппинга не пишется руками: его генерирует source generator библиотеки Riok.Mapperly
/// на этапе сборки. Частичные методы объявляются только публичными — реализации появятся
/// в сгенерированной части класса.
/// </para>
/// <para>
/// Расхождения сущностей и моделей описаны явно атрибутами <c>MapperIgnoreSource</c> и
/// <c>MapperIgnoreTarget</c>: навигации БД, которых нет в UI-моделях, и идентификаторы
/// хранения, которых нет в контрактах. Вычисляемые read-only свойства моделей Mapperly
/// как цель не трогает.
/// </para>
/// </remarks>
[Mapper]
public partial class DashboardMapper
{
    /// <summary>
    /// Преобразует сущность раскладки в контрактную модель вместе с категориями и карточками.
    /// </summary>
    public partial DashboardLayout ToModel(DashboardLayoutEntity entity);

    /// <summary>
    /// Преобразует контрактную модель раскладки в сущность вместе с категориями и карточками.
    /// </summary>
    public partial DashboardLayoutEntity ToEntity(DashboardLayout layout);

    /// <summary>
    /// Преобразует сущность категории в контрактную модель.
    /// </summary>
    [MapperIgnoreSource(nameof(DashboardCategoryEntity.Layout))]
    public partial DashboardCategory ToModel(DashboardCategoryEntity entity);

    /// <summary>
    /// Преобразует контрактную модель категории в сущность.
    /// </summary>
    [MapperIgnoreTarget(nameof(DashboardCategoryEntity.Layout))]
    public partial DashboardCategoryEntity ToEntity(DashboardCategory category);

    /// <summary>
    /// Копирует изменяемые поля контрактной модели категории в отслеживаемую сущность.
    /// </summary>
    /// <remarks>
    /// Идентификаторы хранения и вложенные карточки не затрагиваются: их меняют отдельные операции.
    /// </remarks>
    [MapperIgnoreTarget(nameof(DashboardCategoryEntity.Id))]
    [MapperIgnoreTarget(nameof(DashboardCategoryEntity.DashboardLayoutId))]
    [MapperIgnoreTarget(nameof(DashboardCategoryEntity.Layout))]
    [MapperIgnoreTarget(nameof(DashboardCategoryEntity.Cards))]
    [MapperIgnoreSource(nameof(DashboardCategory.Id))]
    [MapperIgnoreSource(nameof(DashboardCategory.DashboardLayoutId))]
    [MapperIgnoreSource(nameof(DashboardCategory.Cards))]
    public partial void UpdateCategory(DashboardCategory source, DashboardCategoryEntity target);

    /// <summary>
    /// Преобразует сущность карточки в контрактную модель.
    /// </summary>
    [MapperIgnoreSource(nameof(DashboardCardEntity.Category))]
    public partial DashboardCard ToModel(DashboardCardEntity entity);

    /// <summary>
    /// Преобразует контрактную модель карточки в сущность.
    /// </summary>
    [MapperIgnoreTarget(nameof(DashboardCardEntity.Category))]
    [MapperIgnoreSource(nameof(DashboardCard.UrlSchemeText))]
    public partial DashboardCardEntity ToEntity(DashboardCard card);

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
    [MapperIgnoreSource(nameof(DashboardCard.UrlSchemeText))]
    public partial void UpdateCard(DashboardCard source, DashboardCardEntity target);

    /// <summary>
    /// Преобразует сущность настроек в контрактную модель.
    /// </summary>
    [MapperIgnoreSource(nameof(SiteSettingsEntity.Id))]
    public partial SiteSettings ToModel(SiteSettingsEntity entity);

    /// <summary>
    /// Преобразует контрактную модель настроек в сущность.
    /// </summary>
    [MapperIgnoreTarget(nameof(SiteSettingsEntity.Id))]
    public partial SiteSettingsEntity ToEntity(SiteSettings settings);

    /// <summary>
    /// Копирует поля контрактной модели настроек в отслеживаемую сущность.
    /// </summary>
    [MapperIgnoreTarget(nameof(SiteSettingsEntity.Id))]
    public partial void UpdateSettings(SiteSettings source, SiteSettingsEntity target);
}
