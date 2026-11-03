namespace Adashboard.Domain.Reference;

/// <summary>
/// Общие константы dashboard: ограничения размеров и значения по умолчанию,
/// которые используют домен, API и клиент.
/// </summary>
public static class DomainConstants
{
    /// <summary>Максимальная длина заголовка карточки.</summary>
    public const int CardTitleMaxLength = 120;

    /// <summary>Максимальная длина адреса (ссылки) карточки.</summary>
    public const int CardUrlMaxLength = 500;

    /// <summary>Максимальная длина названия категории.</summary>
    public const int CategoryTitleMaxLength = 120;

    /// <summary>Минимальная ширина категории в колонках сетки.</summary>
    public const int CategoryWidthMin = 1;

    /// <summary>Максимальная ширина категории в колонках десктопной сетки.</summary>
    public const int CategoryWidthMax = 12;

    /// <summary>Максимальная ширина категории на среднем брейкпоинте, где сетка состоит из 6 колонок.</summary>
    public const int CategoryMediumWidthMax = 6;

    /// <summary>Максимальная ширина категории на мобильном брейкпоинте, где сетка состоит из 4 колонок.</summary>
    public const int CategoryMobileWidthMax = 4;

    /// <summary>Максимальная длина названия страницы.</summary>
    public const int PageTitleMaxLength = 120;

    /// <summary>Максимальная длина описания страницы.</summary>
    public const int PageDescriptionMaxLength = 300;

    /// <summary>Максимальная длина пути страницы.</summary>
    public const int PagePathMaxLength = 120;

    /// <summary>Максимальная длина CSS-класса иконки страницы (Font Awesome).</summary>
    public const int PageIconClassMaxLength = 100;

    /// <summary>Максимальная длина цвета иконки страницы.</summary>
    public const int PageIconColorMaxLength = 20;

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

    /// <summary>Заголовок dashboard по умолчанию: подставляется, когда у страницы пустой заголовок.</summary>
    public const string AppTitle = "Adashboard";

    /// <summary>Имя HTTP-заголовка, в котором клиент передаёт ключ доступа к API.</summary>
    public const string ApiKeyHeaderName = "X-Api-Key";

    /// <summary>Относительный адрес каталога пользовательских загрузок, который раздаёт сервер.</summary>
    public const string UploadsUrl = "/uploads";

    /// <summary>Имя подкаталога статики приложения внутри каталога загрузок.</summary>
    public const string AssetsFolderName = "assets";

    /// <summary>Относительный адрес каталога статики приложения: фон и иконка.</summary>
    public const string AssetsUrl = UploadsUrl + "/" + AssetsFolderName;

    /// <summary>Относительный адрес иконки приложения.</summary>
    public const string IconAsset = AssetsUrl + "/icon.png";

    /// <summary>Относительный адрес светлого фонового изображения.</summary>
    public const string LightBackgroundAsset = AssetsUrl + "/bg-light.jpg";

    /// <summary>Относительный адрес тёмного фонового изображения.</summary>
    public const string DarkBackgroundAsset = AssetsUrl + "/bg-dark.jpg";
}
