using System.Security.Cryptography;
using Adashboard.Domain.Reference;
using WPR.Operations;

namespace Adashboard.Server.Services;

/// <summary>
/// Сервис хранения пользовательских изображений-иконок карточек.
/// Файлы сохраняются локально в каталоге загрузок приложения (<c>uploads/card-icons</c>).
/// </summary>
public sealed class ImageIconService(DashboardPaths paths, ILogger<ImageIconService> logger)
{
    private const string FolderName = "card-icons";

    private readonly string _folder = Path.Combine(paths.UploadsPath, FolderName);

    /// <summary>
    /// Сохраняет переданные байты изображения для карточки (загрузка с устройства или из буфера).
    /// </summary>
    public async Task<OperationResult<string>> SaveFromBytesAsync(int cardId, byte[] content, CancellationToken cancellationToken = default)
    {
        logger.LogDebug("Начато сохранение изображения карточки {CardId} из загруженных данных.", cardId);

        if (cardId <= 0)
        {
            return Result.Failure("Некорректный идентификатор карточки.").As<string>();
        }

        if (content.Length == 0 || content.LongLength > DomainConstants.MaxImageBytes)
        {
            logger.LogWarning("Изображение карточки {CardId} отклонено: недопустимый размер.", cardId);
            return Result.Failure($"Изображение превышает максимальный размер {DomainConstants.MaxImageMegabytes} МБ.").As<string>();
        }

        var extension = ImageSignature.DetectExtension(content);

        if (extension is null)
        {
            logger.LogWarning("Изображение карточки {CardId} отклонено: неподдерживаемый формат.", cardId);
            return Result.Failure("Файл не является поддерживаемым растровым изображением.").As<string>();
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

        return $"{DomainConstants.UploadsUrl}/{FolderName}/{fileName}";
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
}
