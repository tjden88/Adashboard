using System.Security.Cryptography;
using Adashboard.Domain.Reference;
using WPR.Operations;

namespace Adashboard.Server.Services;

/// <summary>
/// Сервис хранения пользовательских изображений-иконок карточек.
/// Файлы сохраняются локально в каталоге загрузок приложения (<c>uploads/card-icons</c>).
/// </summary>
/// <remarks>
/// Изображение принимается только готовыми байтами от клиента: по URL сервер больше не ходит.
/// Поэтому ограничения здесь не зависят от доверия к пользователю — это запрет на размер
/// больше <see cref="DomainConstants.MaxImageBytes"/> и проверка сигнатуры растрового изображения
/// (защита от сохранения SVG/HTML, которые браузер мог бы исполнить).
/// </remarks>
public sealed class ImageIconService(DashboardPaths paths, ILogger<ImageIconService> logger)
{
    private const string FolderRelativePath = "uploads/card-icons";

    private readonly string _folder = Path.Combine(paths.UploadsPath, "card-icons");

    /// <summary>
    /// Сохраняет переданные байты изображения для карточки (загрузка с устройства или из буфера).
    /// </summary>
    /// <param name="cardId">Идентификатор карточки.</param>
    /// <param name="content">Содержимое изображения.</param>
    /// <param name="cancellationToken">Токен отмены операции.</param>
    /// <returns>Относительный путь к сохранённому изображению либо ошибка валидации.</returns>
    public async Task<OperationResult<string, ServiceError>> SaveFromBytesAsync(
        int cardId,
        byte[] content,
        CancellationToken cancellationToken = default)
    {
        logger.LogDebug("Начато сохранение изображения карточки {CardId} из загруженных данных.", cardId);

        if (cardId <= 0)
        {
            return ServiceError.Validation("Некорректный идентификатор карточки.").AsFailure<string>();
        }

        if (content.Length == 0 || content.LongLength > DomainConstants.MaxImageBytes)
        {
            logger.LogWarning("Изображение карточки {CardId} отклонено: недопустимый размер.", cardId);
            return ServiceError
                .Validation($"Изображение превышает максимальный размер {DomainConstants.MaxImageMegabytes} МБ.")
                .AsFailure<string>();
        }

        var extension = DetectExtension(content);

        if (extension is null)
        {
            logger.LogWarning("Изображение карточки {CardId} отклонено: неподдерживаемый формат.", cardId);
            return ServiceError.Validation("Файл не является поддерживаемым растровым изображением.").AsFailure<string>();
        }

        var path = await WriteImageAsync(cardId, content, extension, cancellationToken);

        logger.LogInformation("Изображение карточки {CardId} сохранено: {Path}.", cardId, path);
        return path;
    }

    /// <summary>
    /// Удаляет все сохранённые изображения карточки.
    /// </summary>
    /// <param name="cardId">Идентификатор карточки.</param>
    public void Delete(int cardId)
    {
        if (cardId <= 0)
            return;

        try
        {
            RemoveCardFiles(cardId);
            logger.LogInformation("Изображения карточки {CardId} удалены.", cardId);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Не удалось удалить изображения карточки {CardId}.", cardId);
        }
    }

    /// <summary>
    /// Записывает изображение в хранилище и возвращает относительный путь.
    /// </summary>
    private async Task<string> WriteImageAsync(int cardId, byte[] content, string extension, CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(_folder);

        // Зашитый в имя хеш содержимого меняется при замене картинки и сбрасывает кэш браузера.
        var hash = Convert.ToHexString(SHA256.HashData(content))[..16].ToLowerInvariant();
        var fileName = $"{cardId}-{hash}{extension}";
        var targetPath = Path.Combine(_folder, fileName);
        var tempPath = Path.Combine(_folder, $".tmp-{Guid.NewGuid():N}");

        await File.WriteAllBytesAsync(tempPath, content, cancellationToken);

        try
        {
            // Сначала фиксируем новый файл, и только потом убираем старые: сбой на этом
            // этапе не должен оставить карточку без изображения.
            File.Move(tempPath, targetPath, overwrite: true);
            RemoveCardFiles(cardId, exceptPath: targetPath);
        }
        catch
        {
            if (File.Exists(tempPath))
            {
                File.Delete(tempPath);
            }

            throw;
        }

        return $"/{FolderRelativePath}/{fileName}";
    }

    /// <summary>
    /// Удаляет файлы всех изображений указанной карточки, кроме файла-исключения.
    /// </summary>
    private void RemoveCardFiles(int cardId, string? exceptPath = null)
    {
        if (!Directory.Exists(_folder))
        {
            return;
        }

        foreach (var path in Directory.EnumerateFiles(_folder, $"{cardId}-*"))
        {
            if (exceptPath is not null && string.Equals(path, exceptPath, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            File.Delete(path);
        }
    }


    /// <summary>
    /// Определяет расширение изображения по сигнатуре файла.
    /// </summary>
    private static string? DetectExtension(ReadOnlySpan<byte> data)
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
            var brand = System.Text.Encoding.ASCII.GetString(data.Slice(8, 4));
            if (brand is "avif" or "avis")
            {
                return ".avif";
            }
        }

        return null;
    }
}
