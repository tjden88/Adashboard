namespace Adashboard.Api.Services;

/// <summary>
/// Наполняет каталог статики приложения стандартными файлами (фон, логотип и т.п.),
/// если их там ещё нет.
/// </summary>
/// <remarks>
/// Оригиналы хранятся вне публичных папок (в каталоге <c>Assets</c>) и служат резервом:
/// пользователь может заменять файлы в каталоге статики, но оригиналы этим не затрагиваются.
/// </remarks>
internal sealed class AssetsInitializer(
    DashboardPaths paths,
    IWebHostEnvironment environment,
    ILogger<AssetsInitializer> logger)
{
    // Оригиналы стандартных файлов, поставляемые вместе с приложением.
    private readonly string _sourceDirectory = Path.Combine(environment.ContentRootPath, "Assets");

    // Каталог статики, доступный по адресу /uploads/assets.
    private readonly string _targetDirectory = Path.Combine(paths.UploadsPath, "assets");

    /// <summary>
    /// Создаёт каталог статики и копирует в него отсутствующие стандартные файлы.
    /// </summary>
    public void Initialize()
    {
        logger.LogInformation("Запущено наполнение каталога статики стандартными файлами.");

        if (!Directory.Exists(_sourceDirectory))
        {
            logger.LogWarning(
                "Каталог оригинальных ассетов {SourceDirectory} не найден. Наполнение пропущено.",
                _sourceDirectory);
            return;
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

        logger.LogInformation(
            "Наполнение каталога статики завершено. Скопировано стандартных файлов: {FileCount}.",
            copiedCount);
    }
}
