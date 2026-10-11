namespace Adashboard.Api.Data;

/// <summary>
/// Единый источник путей изменяемых данных dashboard.
/// </summary>
/// <remarks>
/// Каталог данных лежит вне <c>wwwroot</c>, поэтому файл БД не отдаётся как статика.
/// Раздаётся только подкаталог загрузок (см. <see cref="UploadsPath"/>) по адресу <c>/uploads</c>.
/// В контейнере каталог данных монтируется как volume.
/// </remarks>
/// <param name="DataPath">Корневой каталог изменяемых данных.</param>
/// <param name="DatabasePath">Путь к файлу базы данных SQLite.</param>
/// <param name="UploadsPath">Каталог пользовательских загрузок, раздаваемый по <c>/uploads</c>.</param>
public sealed record DashboardPaths(string DataPath, string DatabasePath, string UploadsPath);
