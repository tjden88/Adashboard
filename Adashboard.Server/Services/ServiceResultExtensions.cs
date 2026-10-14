using Microsoft.AspNetCore.Mvc;
using WPR.Operations;

namespace Adashboard.Server.Services;

/// <summary>
/// Перевод результатов операций сервисов в HTTP-ответы контроллеров.
/// </summary>
/// <remarks>
/// Единая точка соглашения «категория ошибки → статус»: контроллеры не разбирают ошибки вручную
/// и не выбрасывают исключения для ожидаемых ситуаций.
/// </remarks>
public static class ServiceResultExtensions
{
    /// <summary>
    /// Переводит результат без значения в ответ: успех — 204 No Content, ошибка — по её категории.
    /// </summary>
    public static ActionResult ToActionResult(this in OperationResult<Unit, ServiceError> result)
        => result.IsSuccess ? new NoContentResult() : ToErrorResult(result.Error);

    /// <summary>
    /// Переводит результат со значением в ответ: успех строит вызывающий код, ошибка — по её категории.
    /// </summary>
    public static ActionResult ToActionResult<T>(
        this in OperationResult<T, ServiceError> result,
        Func<T, ActionResult> onSuccess)
        => result.TryGetValue(out var value) ? onSuccess(value) : ToErrorResult(result.Error);

    /// <summary>
    /// Строит ответ об ошибке по её категории; сообщение попадает в тело ответа как <c>{ error }</c>.
    /// </summary>
    public static ActionResult ToErrorResult(ServiceError error) => error.Kind switch
    {
        ServiceErrorKind.NotFound => new NotFoundObjectResult(new { error = error.Message }),
        _ => new BadRequestObjectResult(new { error = error.Message })
    };
}
