namespace Adashboard.Data;

/// <summary>
/// Готовит каталог пользовательских загрузок к работе.
/// </summary>
/// <remarks>
/// При выносе загрузок в volume каталог может быть пустым, поэтому поставляемые с приложением
/// фон и логотип копируются один раз — отсутствующие файлы, чтобы не перетирать правки пользователя.
/// </remarks>
internal static class DashboardUploadsInitializer
{
    /// <summary>
    /// Создаёт каталог загрузок и наполняет его отсутствующими стандартными файлами.
    /// </summary>
    /// <param name="uploadsPath">Корневой каталог пользовательских загрузок.</param>
    /// <param name="webRootPath">Каталог wwwroot с поставляемыми файлами.</param>
    /// <param name="logger">Логгер для сообщений об инициализации.</param>
    public static void Initialize(string uploadsPath, string webRootPath, ILogger logger)
    {
        Directory.CreateDirectory(uploadsPath);

        var defaultBackgrounds = Path.Combine(webRootPath, "uploads", "backgrounds");
        if (!Directory.Exists(defaultBackgrounds))
        {
            logger.LogDebug("Каталог поставляемых фонов не найден. Наполнение загрузок пропущено.");
            return;
        }

        var targetDirectory = Path.Combine(uploadsPath, "backgrounds");
        Directory.CreateDirectory(targetDirectory);

        var copiedCount = 0;
        foreach (var sourcePath in Directory.EnumerateFiles(defaultBackgrounds))
        {
            var targetPath = Path.Combine(targetDirectory, Path.GetFileName(sourcePath));
            if (File.Exists(targetPath))
            {
                continue;
            }

            File.Copy(sourcePath, targetPath);
            copiedCount++;
        }

        if (copiedCount > 0)
        {
            logger.LogInformation(
                "В каталог загрузок скопировано стандартных файлов: {FileCount}.",
                copiedCount);
        }
    }
}
