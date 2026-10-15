using Adashboard.Domain.Contracts;

namespace Adashboard.Domain.Extensions;


public static class CardPresentationExtensions
{
    /// <summary>Цвет иконки по умолчанию (тёмный) для светлого фона.</summary>
    private const string DarkForeground = "#0f172a";

    /// <summary>Цвет иконки и текста (белый) для тёмного фона.</summary>
    private const string LightForeground = "#ffffff";

    /// <summary>
    /// Цвет иконки, автоматически подобранный по фону карточки.
    /// </summary>
    public static string GetIconColor(this DashboardCard card) => ResolveForeground(card.BackgroundColor);

    /// <summary>
    /// Цвет текста заголовка, автоматически подобранный по фону карточки.
    /// </summary>
    public static string GetTextColor(this DashboardCard card) => ResolveForeground(card.BackgroundColor);

    /// <summary>
    /// Определяет контрастный цвет (белый или тёмный) для указанного цвета фона.
    /// </summary>
    private static string ResolveForeground(string backgroundColor)
        => ShouldUseLightText(backgroundColor) ? LightForeground : DarkForeground;

    /// <summary>
    /// Определяет, нужно ли для данного цвета фона использовать светлые (белые) текст и иконку.
    /// Возвращает <c>true</c> для достаточно тёмных фонов.
    /// </summary>
    private static bool ShouldUseLightText(string hexColor)
    {
        var rgb = ParseHexColor(hexColor);
        if (rgb is null)
        {
            return true;
        }

        // Относительная яркость по стандарту WCAG: глаз по-разному воспринимает красный,
        // зелёный и синий каналы, поэтому они входят в формулу с разными весами.
        var r = Linearize(rgb.Value.R / 255.0);
        var g = Linearize(rgb.Value.G / 255.0);
        var b = Linearize(rgb.Value.B / 255.0);

        var luminance = 0.2126 * r + 0.7152 * g + 0.0722 * b;

        return luminance < 0.45;
    }

    /// <summary>
    /// Разбирает цвет вида <c>#RRGGBB</c> в компоненты RGB; <c>null</c>, если формат не распознан.
    /// </summary>
    private static (byte R, byte G, byte B)? ParseHexColor(string hex)
    {
        if (string.IsNullOrWhiteSpace(hex))
        {
            return null;
        }

        var raw = hex.TrimStart('#');
        if (raw.Length != 6)
        {
            return null;
        }

        try
        {
            return (
                Convert.ToByte(raw[..2], 16),
                Convert.ToByte(raw[2..4], 16),
                Convert.ToByte(raw[4..6], 16));
        }
        catch (FormatException)
        {
            return null;
        }
    }

    /// <summary>
    /// Переводит значение канала (0..1) в линейное пространство для расчёта яркости.
    /// </summary>
    private static double Linearize(double c)
        => c <= 0.04045 ? c / 12.92 : Math.Pow((c + 0.055) / 1.055, 2.4);
}
