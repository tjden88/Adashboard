namespace Adashboard.Api.Data;

/// <summary>
/// Перемещает стандартные файлы в wwwroot, если они отсутствуют.
/// </summary>
internal sealed class AssetsInitializer(IWebHostEnvironment environment, ILogger<AssetsInitializer> logger)
{
    // Публичная папка ассетов
    private readonly string assetsPath = Path.Combine(environment.ContentRootPath, "assets");

    // Публичная папка ассетов, доступная через веб-сервер
    private readonly string publicStaticPath = Path.Combine(environment.ContentRootPath, "data", "uploads", "assets");

    /// <summary>
    /// Создаёт каталог ассетов и наполняет его отсутствующими стандартными файлами.
    /// </summary>
    public void Initialize()
    {
        if(!Directory.Exists(assetsPath))
            throw new InvalidOperationException("Каталог ассетов не найден.");

        Directory.CreateDirectory(publicStaticPath);

        var copiedCount = 0;
        foreach (var sourcePath in Directory.EnumerateFiles(assetsPath))
        {
            var targetPath = Path.Combine(publicStaticPath, Path.GetFileName(sourcePath));
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
                "В каталог ассетов добавлено стандартных файлов: {FileCount}.", copiedCount);
        }
    }
}
