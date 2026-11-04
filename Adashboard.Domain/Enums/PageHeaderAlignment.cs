namespace Adashboard.Domain.Enums;

/// <summary>
/// Выравнивание заголовка и описания страницы в шапке: по левому краю или по центру.
/// </summary>
public enum PageHeaderAlignment
{
    Left = 0,
    // Значение 0 сохранено: enum хранится в БД как int, новые значения только дописываем в конец.
    Center = 1
}
