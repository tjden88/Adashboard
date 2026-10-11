namespace Adashboard.Domain.Models;

/// <summary>
/// Модель изображения-иконки карточки. Может быть либо стандартной иконкой Font Awesome, либо загруженным пользователем изображением.
/// </summary>
public class CardImage
{
    public int Id { get; set; }

    /// <summary>
    /// Признак того, что изображение-иконка загружено пользователем и хранится в uploads.
    /// Иначе является стандартной иконкой Font Awesome.
    /// </summary>
    public bool IsCustom { get; set; }


    /// <summary>
    /// CSS-класс иконки Font Awesome.
    /// Либо относительный путь к загруженному изображению-иконке
    /// </summary>
    public string IconSource { get; set; } = "fa-solid fa-link";

}