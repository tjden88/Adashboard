using System.Globalization;
using System.Text;

namespace Adashboard.Domain.Reference;

/// <summary>
/// Правила относительного пути страницы dashboard: проверка корректности и генерация из заголовка.
/// </summary>
/// <remarks>
/// Путь — сегмент адреса: латиница в нижнем регистре, цифры и дефис. Используется и на сервере
/// (проверка входных данных), и на клиенте (валидация в диалоге и авто-slug из названия).
/// </remarks>
public static class PagePathRules
{
    /// <summary>Максимальная длина пути (совпадает с ограничением модели).</summary>
    public const int MaxLength = DomainConstants.PagePathMaxLength;

    // Соответствие русских букв латинице для генерации пути из заголовка.
    private static readonly Dictionary<char, string> Transliteration = new()
    {
        ['а'] = "a", ['б'] = "b", ['в'] = "v", ['г'] = "g", ['д'] = "d",
        ['е'] = "e", ['ё'] = "e", ['ж'] = "zh", ['з'] = "z", ['и'] = "i",
        ['й'] = "y", ['к'] = "k", ['л'] = "l", ['м'] = "m", ['н'] = "n",
        ['о'] = "o", ['п'] = "p", ['р'] = "r", ['с'] = "s", ['т'] = "t",
        ['у'] = "u", ['ф'] = "f", ['х'] = "h", ['ц'] = "c", ['ч'] = "ch",
        ['ш'] = "sh", ['щ'] = "sch", ['ъ'] = string.Empty, ['ы'] = "y", ['ь'] = string.Empty,
        ['э'] = "e", ['ю'] = "yu", ['я'] = "ya",
        ['і'] = "i", ['ї'] = "i", ['є'] = "e", ['ґ'] = "g",
    };

    /// <summary>
    /// Проверяет, что значение является корректным путём страницы либо пустой строкой
    /// (пустой путь допустим только у основной страницы).
    /// </summary>
    public static bool IsValid(string? path)
    {
        if (path is null || path.Length > MaxLength)
        {
            return false;
        }

        if (path.Length == 0)
        {
            return true;
        }

        var previousDash = false;
        for (var i = 0; i < path.Length; i++)
        {
            var c = path[i];

            if (c == '-')
            {
                // Дефис не может стоять в начале, в конце или идти подряд.
                if (i == 0 || i == path.Length - 1 || previousDash)
                {
                    return false;
                }

                previousDash = true;
                continue;
            }

            previousDash = false;

            if (c is >= 'a' and <= 'z' || c is >= '0' and <= '9')
            {
                continue;
            }

            return false;
        }

        return true;
    }

    /// <summary>
    /// Формирует путь из заголовка: транслитерация кириллицы, нижний регистр,
    /// остальные символы заменяются дефисом.
    /// </summary>
    /// <param name="title">Отображаемый заголовок страницы.</param>
    /// <returns>Путь страницы; пустая строка, если из заголовка не удалось получить ни одного допустимого символа.</returns>
    public static string CreateSlug(string? title)
    {
        if (string.IsNullOrWhiteSpace(title))
        {
            return string.Empty;
        }

        var builder = new StringBuilder(title.Length);
        foreach (var rawChar in title.Trim().ToLowerInvariant())
        {
            var c = char.ToLower(rawChar, CultureInfo.InvariantCulture);

            if (Transliteration.TryGetValue(c, out var replacement))
            {
                builder.Append(replacement);
            }
            else if (c is >= 'a' and <= 'z' || c is >= '0' and <= '9')
            {
                builder.Append(c);
            }
            else
            {
                // Любой другой символ (пробел, знак, кириллица без соответствия) — разделитель.
                builder.Append('-');
            }
        }

        return CollapseDashes(builder.ToString());
    }

    // Схлопывает подряд идущие дефисы и убирает их по краям.
    private static string CollapseDashes(string value)
    {
        var builder = new StringBuilder(value.Length);
        var previousDash = false;

        foreach (var c in value)
        {
            if (c == '-')
            {
                if (previousDash || builder.Length == 0)
                {
                    continue;
                }

                previousDash = true;
            }
            else
            {
                previousDash = false;
            }

            builder.Append(c);
        }

        while (builder.Length > 0 && builder[^1] == '-')
        {
            builder.Length--;
        }

        var result = builder.ToString();
        return result.Length > MaxLength ? result[..MaxLength].TrimEnd('-') : result;
    }
}
