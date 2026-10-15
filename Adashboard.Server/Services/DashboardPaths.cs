namespace Adashboard.Server.Services;

/// <summary>
/// Единый источник путей изменяемых данных dashboard.
/// </summary>
/// <remarks>
/// Каталог данных лежит вне <c>wwwroot</c>, поэтому файл БД не отдаётся как статика.
/// Раздаётся только подкаталог загрузок (см. <see cref="UploadsPath"/>) по адресу <c>/uploads</c>.
/// В контейнере каталог данных монтируется как volume.
/// </remarks>
public sealed class DashboardPaths(string rootPath)
{
    /// <summary>Корневой каталог изменяемых данных.</summary>
    public string DataPath { get; init; } = Path.Combine(rootPath, "appdata");

    /// <summary>Путь к файлу базы данных SQLite.</summary>
    public string DatabasePath => Path.Combine(DataPath, "data.db");

    /// <summary>Каталог пользовательских загрузок, раздаваемый по <c>/uploads</c>.</summary>
    public string UploadsPath => Path.Combine(DataPath, "uploads");

    /// <summary>Оригиналы стандартных файлов, поставляемые вместе с приложением.</summary>
    public string AssetsPath => Path.Combine(rootPath, "assets");

}
