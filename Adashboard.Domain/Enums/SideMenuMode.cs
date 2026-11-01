namespace Adashboard.Domain.Enums;

/// <summary>
/// Режим бокового меню страниц: скрыто, свёрнуто, автосворачивание или всегда развёрнуто.
/// </summary>
public enum SideMenuMode
{
    Hidden = 0,
    AutoCollapse = 1,
    Expanded = 2,
    // Значения 0–2 сохранены: они хранятся в БД как int, новые — только дописывать в конец.
    Collapsed = 3
}
