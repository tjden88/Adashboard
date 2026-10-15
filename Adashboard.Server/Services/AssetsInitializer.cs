using Adashboard.Domain.Reference;

namespace Adashboard.Server.Services;

/// <summary>
/// Наполняет каталог статики приложения стандартными файлами (фон, логотип и т.п.),
/// если их там ещё нет.
/// </summary>
internal sealed class AssetsInitializer(DashboardPaths paths, ILogger<AssetsInitializer> logger)
{
    // Оригиналы стандартных файлов, поставляемые вместе с приложением.
    private readonly string _sourceDirectory = paths.AssetsPath;

    // Каталог статики, доступный по адресу /uploads/assets.
    private readonly string _targetDirectory = Path.Combine(paths.UploadsPath, DomainConstants.AssetsFolderName);

    /// <summary>
    /// Создаёт каталог статики и копирует в него отсутствующие стандартные файлы.
    /// </summary>
    public void Initialize()
    {
        logger.LogDebug("Запущено наполнение каталога статики стандартными файлами.");

        if (!Directory.Exists(_sourceDirectory))
        {
            logger.LogCritical("Каталог оригинальных ассетов {SourceDirectory} не найден. Наполнение пропущено.", _sourceDirectory);
            throw new InvalidOperationException("Каталог оригинальных ассетов не найден.");
        }

        Directory.CreateDirectory(_targetDirectory);

        var copiedCount = 0;
        foreach (var sourcePath in Directory.EnumerateFiles(_sourceDirectory))
        {
            var targetPath = Path.Combine(_targetDirectory, Path.GetFileName(sourcePath));
            if (File.Exists(targetPath))
            {
                continue;
            }

            File.Copy(sourcePath, targetPath);
            copiedCount++;
        }

        logger.LogInformation("Наполнение каталога статики завершено. Скопировано стандартных файлов: {FileCount}.", copiedCount);
    }
}
