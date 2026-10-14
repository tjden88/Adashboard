using WPR.Operations;

namespace Adashboard.Server.Services;

/// <summary>
/// Категория ошибки операции сервиса, определяющая HTTP-статус ответа API.
/// </summary>
public enum ServiceErrorKind
{
    /// <summary>Некорректные или неполные данные запроса (HTTP 400).</summary>
    Validation,

    /// <summary>Запрошенный объект не найден (HTTP 404).</summary>
    NotFound
}

/// <summary>
/// Ожидаемая ошибка операции сервиса: категория и понятное пользователю сообщение.
/// </summary>
/// <remarks>
/// Сервисы не выбрасывают исключения для ожидаемых ситуаций, а возвращают результат
/// <c>OperationResult</c> из библиотеки WPR.Operations с такой ошибкой. Контроллер переводит
/// категорию в HTTP-статус, а сообщение — в тело ответа.
/// </remarks>
public readonly record struct ServiceError(ServiceErrorKind Kind, string Message)
{
    /// <summary>Создаёт ошибку некорректных данных (HTTP 400).</summary>
    public static ServiceError Validation(string message) => new(ServiceErrorKind.Validation, message);

    /// <summary>Создаёт ошибку «объект не найден» (HTTP 404).</summary>
    public static ServiceError NotFound(string message) => new(ServiceErrorKind.NotFound, message);
}

/// <summary>
/// Превращение ошибки сервиса в неуспешный результат операции.
/// </summary>
public static class ServiceErrorExtensions
{
    /// <summary>Оборачивает ошибку в неуспешный результат без значения.</summary>
    public static OperationResult<Unit, ServiceError> AsFailure(this ServiceError error)
        => OperationResult<Unit, ServiceError>.Failure(error);

    /// <summary>Оборачивает ошибку в неуспешный результат со значением типа <typeparamref name="T"/>.</summary>
    public static OperationResult<T, ServiceError> AsFailure<T>(this ServiceError error)
        => OperationResult<T, ServiceError>.Failure(error);
}
