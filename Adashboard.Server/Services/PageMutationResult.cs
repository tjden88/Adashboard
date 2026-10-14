namespace Adashboard.Server.Services;

/// <summary>
/// Результат изменяющей операции над страницей dashboard.
/// </summary>
[Obsolete]
public enum PageMutationResult
{
    /// <summary>Операция выполнена.</summary>
    Success,

    /// <summary>Страница с указанным идентификатором не найдена.</summary>
    NotFound,

    /// <summary>Путь уже занят другой страницей.</summary>
    PathTaken,

    /// <summary>Путь основной страницы зарезервирован: его нельзя занять или изменить.</summary>
    MainPageProtected
}
