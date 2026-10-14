namespace Adashboard.Domain.Reference;

/// <summary>
/// Общие константы dashboard: ограничения размеров и значения по умолчанию,
/// которые используют домен, API и клиент.
/// </summary>
/// <remarks>
/// Значения заданы в одном месте, чтобы атрибуты валидации контрактов и сущностей БД,
/// проверки API и ограничения полей ввода клиента не расходились между собой.
/// </remarks>
public static class DomainConstants
{
    /// <summary>Максимальная длина заголовка карточки.</summary>
    public const int CardTitleMaxLength = 120;

    /// <summary>Максимальная длина адреса (ссылки) карточки.</summary>
    public const int CardUrlMaxLength = 500;

    /// <summary>Максимальная длина названия категории.</summary>
    public const int CategoryTitleMaxLength = 120;

    /// <summary>Максимальная длина названия страницы.</summary>
    public const int PageTitleMaxLength = 120;

    /// <summary>Максимальная длина описания страницы.</summary>
    public const int PageDescriptionMaxLength = 300;

    /// <summary>Максимальная длина пути страницы.</summary>
    public const int PagePathMaxLength = 120;

    /// <summary>Максимальный размер принимаемого изображения-иконки в мегабайтах.</summary>
    public const int MaxImageMegabytes = 2;

    /// <summary>Максимальный размер принимаемого изображения-иконки в байтах.</summary>
    public const long MaxImageBytes = MaxImageMegabytes * 1024L * 1024L;

    /// <summary>Интервал проверки статуса карточек по умолчанию в секундах.</summary>
    public const int DefaultHealthCheckIntervalSeconds = 30;

    /// <summary>Максимальный интервал проверки статуса карточек в секундах.</summary>
    public const int HealthCheckIntervalMaxSeconds = 3600;

    /// <summary>CSS-класс иконки карточки по умолчанию (Font Awesome).</summary>
    public const string DefaultCardIcon = "fa-solid fa-link";

    /// <summary>Цвет фона карточки по умолчанию.</summary>
    public const string DefaultCardBackgroundColor = "#1e40af";
}
