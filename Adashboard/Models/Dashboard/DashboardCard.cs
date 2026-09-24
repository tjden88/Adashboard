using System.Globalization;

namespace Adashboard.Models.Dashboard;

/// <summary>
/// Карточка ссылки внутри категории.
/// </summary>
public sealed class DashboardCard
{
    /// <summary>
    /// Уникальный идентификатор карточки.
    /// </summary>
    public int Id { get; set; }

    /// <summary>
    /// Идентификатор категории, к которой принадлежит карточка.
    /// </summary>
    public int CategoryId { get; set; }

    /// <summary>
    /// Отображаемое название карточки.
    /// </summary>
    public string Title { get; set; } = string.Empty;

    /// <summary>
    /// Адрес перехода по карточке (без схемы).
    /// </summary>
    public string Url { get; set; } = string.Empty;

    /// <summary>
    /// Схема, добавляемая к адресу при открытии: "http://", "https://" или пустая строка.
    /// </summary>
    public string UrlScheme { get; set; } = string.Empty;

    /// <summary>
    /// CSS-класс иконки Font Awesome.
    /// </summary>
    public string IconClass { get; set; } = string.Empty;

    /// <summary>
    /// Относительный путь к загруженному изображению-иконке или пустая строка.
    /// Если задан, изображение имеет приоритет над <see cref="IconClass"/>.
    /// </summary>
    public string ImagePath { get; set; } = string.Empty;

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
    public CardPosition Position { get; set; } = new();

    /// <summary>
    /// Определяет, нужно ли для данного цвета фона использовать светлые (белые) текст и иконку.
    /// Возвращает true для достаточно тёмных фонов.
    /// </summary>
    public static bool ShouldUseLightText(string hexColor)
    {
        var rgb = ParseHexColor(hexColor);
        if (rgb is null) return true;

        double r = Linearize(rgb.Value.R / 255.0);
        double g = Linearize(rgb.Value.G / 255.0);
        double b = Linearize(rgb.Value.B / 255.0);

        double luminance = 0.2126 * r + 0.7152 * g + 0.0722 * b;

        return luminance < 0.45;
    }

    /// <summary>
    /// Цвет иконки, автоматически подобранный на основе фона карточки.
    /// </summary>
    public string GetIconColor() => ShouldUseLightText(BackgroundColor) ? "#ffffff" : "#0f172a";

    /// <summary>
    /// Цвет текста заголовка карточки, автоматически подобранный на основе фона.
    /// </summary>
    public string GetTextColor() => ShouldUseLightText(BackgroundColor) ? "#ffffff" : "#0f172a";

    private static (byte R, byte G, byte B)? ParseHexColor(string hex)
    {
        if (string.IsNullOrWhiteSpace(hex)) return null;
        var raw = hex.TrimStart('#');
        if (raw.Length != 6) return null;
        return (
            byte.Parse(raw[..2], NumberStyles.HexNumber),
            byte.Parse(raw[2..4], NumberStyles.HexNumber),
            byte.Parse(raw[4..6], NumberStyles.HexNumber));
    }

    private static double Linearize(double c)
    {
        return c <= 0.04045 ? c / 12.92 : Math.Pow((c + 0.055) / 1.055, 2.4);
    }
}
