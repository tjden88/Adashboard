/**
 * Поиск элемента разметки страницы: отсутствие элемента означает ошибку в разметке,
 * поэтому элемент возвращается сразу нужного типа, и проверка на пустое значение в каждом месте не нужна.
 */
export function find<T extends HTMLElement>(id: string): T {
    const element = document.getElementById(id);
    if (!element) throw new Error(`Не найден элемент страницы: ${id}.`);

    return element as T;
}
