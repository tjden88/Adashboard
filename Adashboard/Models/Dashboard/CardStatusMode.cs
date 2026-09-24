namespace Adashboard.Models.Dashboard;

/// <summary>
/// Режим проверки доступности сервиса, на который ведёт карточка.
/// </summary>
public enum CardStatusMode
{
    /// <summary>
    /// Статус не проверяется, индикатор на карточке не отображается.
    /// </summary>
    None = 0,

    /// <summary>
    /// Сервис считается доступным при любом HTTP-ответе, включая ошибки авторизации и 5xx.
    /// </summary>
    Reachable = 1,

    /// <summary>
    /// Сервис считается доступным, если сервер не вернул ошибку 5xx.
    /// </summary>
    NoServerError = 2,

    /// <summary>
    /// Сервис считается доступным только при успешном ответе (коды 2xx).
    /// </summary>
    Success = 3
}
