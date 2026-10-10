using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

namespace Adashboard.Services;

/// <summary>
/// Клиентская реализация <see cref="IImageIconService"/> через эндпоинты API.
/// </summary>
/// <remarks>
/// Серверная логика (проверка формата, ограничение размера, защита от SSRF, хеш в имени файла
/// и удаление старых файлов) живёт в Adashboard.Api. Клиент только передаёт данные и получает путь.
/// </remarks>
public sealed class ImageIconHttpService(
    HttpClient httpClient,
    ILogger<ImageIconHttpService> logger) : IImageIconService
{
    /// <summary>
    /// Скачивает изображение по ссылке через API.
    /// </summary>
    public async Task<string> SaveFromUrlAsync(int cardId, string url, CancellationToken cancellationToken = default)
    {
        logger.LogInformation("Начато сохранение изображения карточки {CardId} по ссылке через API.", cardId);

        using var response = await httpClient.PostAsJsonAsync(
            $"api/cards/{cardId}/image/from-url",
            new { url },
            cancellationToken);

        var imagePath = await ReadImagePathAsync(response, cancellationToken);
        logger.LogInformation("Изображение карточки {CardId} сохранено через API: {Path}.", cardId, imagePath);
        return imagePath;
    }

    /// <summary>
    /// Сохраняет загруженные байты изображения через API.
    /// </summary>
    public async Task<string> SaveFromBytesAsync(int cardId, byte[] content, CancellationToken cancellationToken = default)
    {
        logger.LogInformation("Начато сохранение изображения карточки {CardId} из загруженных данных через API.", cardId);

        using var form = new MultipartFormDataContent();
        var fileContent = new ByteArrayContent(content);
        fileContent.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
        form.Add(fileContent, "file", $"card-{cardId}");

        using var response = await httpClient.PostAsync($"api/cards/{cardId}/image", form, cancellationToken);

        var imagePath = await ReadImagePathAsync(response, cancellationToken);
        logger.LogInformation("Изображение карточки {CardId} сохранено через API: {Path}.", cardId, imagePath);
        return imagePath;
    }

    /// <summary>
    /// Запускает удаление изображений карточки через API.
    /// </summary>
    /// <remarks>
    /// Контракт метода синхронный (совместим с вызовами из существующих компонентов),
    /// поэтому HTTP-запрос выполняется в фоне: интерфейс не должен ждать сетевой операции.
    /// </remarks>
    public void Delete(int cardId)
    {
        if (cardId <= 0)
        {
            return;
        }

        _ = DeleteAsync(cardId);
    }

    /// <summary>
    /// Выполняет фоновое удаление изображений карточки и логирует результат.
    /// </summary>
    private async Task DeleteAsync(int cardId)
    {
        try
        {
            using var response = await httpClient.DeleteAsync($"api/cards/{cardId}/image");
            if (response.IsSuccessStatusCode)
            {
                logger.LogInformation("Изображения карточки {CardId} удалены через API.", cardId);
            }
            else
            {
                logger.LogWarning(
                    "Удаление изображений карточки {CardId} через API завершилось неудачно. Код: {StatusCode}.",
                    cardId,
                    (int)response.StatusCode);
            }
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Не удалось удалить изображения карточки {CardId} через API.", cardId);
        }
    }

    /// <summary>
    /// Читает путь к изображению из успешного ответа или выбрасывает понятную ошибку.
    /// </summary>
    private static async Task<string> ReadImagePathAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        var body = await response.Content.ReadAsStringAsync(cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            throw new InvalidOperationException(ExtractString(body, "error") ?? "Не удалось сохранить изображение.");
        }

        var imagePath = ExtractString(body, "imagePath");
        if (string.IsNullOrWhiteSpace(imagePath))
        {
            throw new InvalidOperationException("API не вернуло путь к изображению.");
        }

        return imagePath;
    }

    /// <summary>
    /// Извлекает строковое свойство из JSON-ответа API, не полагаясь на строгую модель.
    /// </summary>
    private static string? ExtractString(string body, string property)
    {
        if (string.IsNullOrWhiteSpace(body))
        {
            return null;
        }

        try
        {
            using var document = JsonDocument.Parse(body);
            if (document.RootElement.ValueKind == JsonValueKind.Object
                && document.RootElement.TryGetProperty(property, out var value)
                && value.ValueKind == JsonValueKind.String)
            {
                return value.GetString();
            }
        }
        catch (JsonException)
        {
            // Формат ответа неожиданный: считаем свойство отсутствующим.
        }

        return null;
    }
}
