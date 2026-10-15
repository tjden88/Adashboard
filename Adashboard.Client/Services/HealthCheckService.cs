using System.Net;
using Adashboard.Domain.Enums;
using Adashboard.Domain.ReadModels;

namespace Adashboard.Client.Services;

/// <summary>
/// Сервис проверки доступности сервисов, на которые ведут карточки dashboard.
/// </summary>
public sealed class HealthCheckService(IDashboardApi dashboardApi, ILogger<HealthCheckService> logger)
{
    /// <summary>
    /// Максимальное число одновременных проверок доступности.
    /// </summary>
    private const int MaxConcurrentChecks = 6;

    // Пояснение для индикатора при сбое проверки: детали health-check в UI не показываем.
    private const string ErrorMessage = "Не удалось проверить статус сервиса.";

    // Каждая карточка проверяет себя сама и независимо от остальных, поэтому без ограничения
    // десятки карточек начали бы опрашивать сервер одновременно. Семафор сдерживает поток запросов.
    private readonly SemaphoreSlim _concurrencyLimiter = new(MaxConcurrentChecks, MaxConcurrentChecks);

    /// <summary>
    /// Выполняет одну проверку доступности карточки по её идентификатору. Возвращает
    /// <c>null</c>, если карточки на сервере больше нет: проверять её не нужно.
    /// </summary>
    public async Task<HealthCheckResult?> CheckAsync(int cardId, CancellationToken cancellationToken = default)
    {
        // Занимаем «слот» до начала запроса: проверок одновременно не больше MaxConcurrentChecks.
        await _concurrencyLimiter.WaitAsync(cancellationToken);
        try
        {
            var response = await dashboardApi.CheckHealthAsync(cardId, cancellationToken);

            // Карточки на сервере больше нет (её удалили, например, в другой вкладке): проверять нечего.
            if (response.StatusCode == HttpStatusCode.NotFound)
            {
                logger.LogWarning("Карточка {CardId} не найдена на сервере. Проверки для неё прекращены.", cardId);
                return null;
            }

            var result = response.Content;
            if (!response.IsSuccessStatusCode || result is null)
            {
                logger.LogWarning("Проверка карточки {CardId} завершилась ошибкой API. Код: {StatusCode}. Ответ: {Error}", cardId, (int?)response.StatusCode, response.ErrorText());
                return new HealthCheckResult(HealthStatus.Unknown, ErrorMessage);
            }

            return result;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // Отмена всего цикла проверок: пробрасываем, чтобы корректно завершить работу.
            throw;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Проверка карточки {CardId} завершилась ошибкой.", cardId);
            return new HealthCheckResult(HealthStatus.Unknown, ErrorMessage);
        }
        finally
        {
            _concurrencyLimiter.Release();
        }
    }
}
