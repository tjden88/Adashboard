namespace Adashboard.Server.Services;

/// <summary>
/// Пустое значение результата операции, не несущей полезных данных.
/// </summary>
/// <remarks>
/// Нужно, чтобы изменяющие операции тоже возвращали типизированный результат
/// <c>OperationResult&lt;Unit, ServiceError&gt;</c> с категорией ошибки.
/// </remarks>
public readonly struct Unit
{
    /// <summary>Единственное значение типа: им обозначается успешный результат без данных.</summary>
    public static Unit Value => default;
}
