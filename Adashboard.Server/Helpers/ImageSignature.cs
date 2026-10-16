using System.Text;

namespace Adashboard.Server.Helpers;

/// <summary>
/// Определение формата изображения по содержимому: используется при приёме файлов
/// от пользователя и при подтягивании иконки по ссылке.
/// </summary>
public static class ImageSignature
{
    // Сколько байт проверяем при распознавании текстовых форматов вроде SVG.
    private const int TextProbeBytes = 1024;

    /// <summary>
    /// Определяет расширение файла изображения по его сигнатуре.
    /// </summary>
    public static string? DetectExtension(ReadOnlySpan<byte> data)
    {
        if (data.Length >= 8 && data[0] == 0x89 && data[1] == 0x50 && data[2] == 0x4E && data[3] == 0x47)
        {
            return ".png";
        }

        if (data.Length >= 3 && data[0] == 0xFF && data[1] == 0xD8 && data[2] == 0xFF)
        {
            return ".jpg";
        }

        if (data.Length >= 6 && data[0] == 0x47 && data[1] == 0x49 && data[2] == 0x46 && data[3] == 0x38)
        {
            return ".gif";
        }

        if (data.Length >= 12
            && data[0] == 0x52 && data[1] == 0x49 && data[2] == 0x46 && data[3] == 0x46
            && data[8] == 0x57 && data[9] == 0x45 && data[10] == 0x42 && data[11] == 0x50)
        {
            return ".webp";
        }

        if (data.Length >= 2 && data[0] == 0x42 && data[1] == 0x4D)
        {
            return ".bmp";
        }

        if (data.Length >= 4 && data[0] == 0x00 && data[1] == 0x00 && data[2] == 0x01 && data[3] == 0x00)
        {
            return ".ico";
        }

        if (data.Length >= 12 && data[4] == 0x66 && data[5] == 0x74 && data[6] == 0x79 && data[7] == 0x70)
        {
            var brand = Encoding.ASCII.GetString(data.Slice(8, 4));
            if (brand is "avif" or "avis")
            {
                return ".avif";
            }
        }

        // SVG — текстовый формат без бинарной сигнатуры: распознаём по началу документа.
        if (IsSvg(data))
        {
            return ".svg";
        }

        return null;
    }

    /// <summary>
    /// Возвращает MIME-тип изображения по расширению файла.
    /// </summary>
    public static string? GetContentType(string extension) => extension switch
    {
        ".png" => "image/png",
        ".jpg" => "image/jpeg",
        ".gif" => "image/gif",
        ".webp" => "image/webp",
        ".bmp" => "image/bmp",
        ".ico" => "image/x-icon",
        ".avif" => "image/avif",
        ".svg" => "image/svg+xml",
        _ => null
    };

    /// <summary>
    /// Проверяет, начинается ли содержимое с документа SVG.
    /// </summary>
    private static bool IsSvg(ReadOnlySpan<byte> data)
    {
        var length = Math.Min(data.Length, TextProbeBytes);
        if (length == 0)
        {
            return false;
        }

        var text = Encoding.UTF8.GetString(data[..length]).TrimStart('\uFEFF', ' ', '\t', '\r', '\n');

        if (text.StartsWith("<svg", StringComparison.OrdinalIgnoreCase)
            || text.StartsWith("<!doctype svg", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        // Пропускаем XML-пролог и комментарии перед корневым тегом.
        if (text.StartsWith("<?xml", StringComparison.OrdinalIgnoreCase)
            || text.StartsWith("<!--", StringComparison.Ordinal))
        {
            return text.Contains("<svg", StringComparison.OrdinalIgnoreCase);
        }

        return false;
    }
}
