// Диалоговые окна: начальный фокус, возврат фокуса и блокировка прокрутки фона.
//
// Контейнер диалогов зовёт эти функции с идентификатором окна. Стопку открытых окон модуль
// ведёт сам, поэтому закрытие окна возвращает фокус туда, откуда его открыли, а прокрутка
// фона блокируется, пока открыто хотя бы одно окно.

const focusableSelector =
    'a[href], button:not([disabled]), input:not([disabled]), select:not([disabled]), textarea:not([disabled]), [tabindex]:not([tabindex="-1"])';

const stack = [];

function findDialog(id) {
    return document.querySelector(`[data-dialog-id="${id}"]`);
}

function getFocusable(dialog) {
    return Array.from(dialog.querySelectorAll(focusableSelector));
}

export function focusDialog(id) {
    if (stack.some(entry => entry.id === id)) {
        return;
    }

    const dialog = findDialog(id);
    if (!dialog) {
        return;
    }

    stack.push({ id: id, previous: document.activeElement, dialog: dialog });
    document.body.classList.add('dialog-open');

    // Начальный фокус: помеченный элемент, иначе первое поле в теле окна, иначе само окно.
    const body = dialog.querySelector('[data-dialog-body]');
    const target = dialog.querySelector('[autofocus]')
        ?? getFocusable(body ?? dialog)[0]
        ?? dialog;

    target.focus({ preventScroll: true });
}

export function releaseDialog(id) {
    const index = stack.findIndex(entry => entry.id === id);
    if (index < 0) {
        return;
    }

    const isTop = index === stack.length - 1;
    const entry = stack[index];
    stack.splice(index, 1);

    if (stack.length === 0) {
        document.body.classList.remove('dialog-open');
    }

    // Фокус возвращаем только при закрытии верхнего окна: активным снова становится
    // элемент, с которого это окно открыли.
    if (isTop && entry.previous instanceof HTMLElement && entry.previous.isConnected) {
        entry.previous.focus({ preventScroll: true });
    }
}

// Ловушка фокуса: Tab не выходит за пределы верхнего окна. Если фокус оказался вне окна
// (например, после клика по затемнению), при нажатии Tab возвращаем его в окно.
document.addEventListener('keydown', event => {
    if (event.key !== 'Tab' || stack.length === 0) {
        return;
    }

    const dialog = stack[stack.length - 1].dialog;
    if (!dialog || !dialog.isConnected) {
        return;
    }

    const focusable = getFocusable(dialog);
    if (focusable.length === 0) {
        return;
    }

    const first = focusable[0];
    const last = focusable[focusable.length - 1];

    if (!dialog.contains(document.activeElement)) {
        event.preventDefault();
        (event.shiftKey ? last : first).focus();
    } else if (event.shiftKey && (document.activeElement === first || document.activeElement === dialog)) {
        event.preventDefault();
        last.focus();
    } else if (!event.shiftKey && document.activeElement === last) {
        event.preventDefault();
        first.focus();
    }
});
