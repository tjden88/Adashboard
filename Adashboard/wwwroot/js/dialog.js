let activeDialog = null;

function getFocusable(dialog) {
    return Array.from(dialog.querySelectorAll(
        'a[href], button:not([disabled]), input:not([disabled]), select:not([disabled]), textarea:not([disabled]), [tabindex]:not([tabindex="-1"])'
    ));
}

export function focusDialog(dialog) {
    if (!dialog) {
        return;
    }

    activeDialog = dialog;
    dialog.focus();
}

document.addEventListener('keydown', (event) => {
    if (!activeDialog || !activeDialog.isConnected) {
        activeDialog = null;
        return;
    }

    if (event.key !== 'Tab') {
        return;
    }

    const focusable = getFocusable(activeDialog);
    if (focusable.length === 0) {
        return;
    }

    const first = focusable[0];
    const last = focusable[focusable.length - 1];

    if (event.shiftKey && document.activeElement === first) {
        event.preventDefault();
        last.focus();
    } else if (!event.shiftKey && document.activeElement === last) {
        event.preventDefault();
        first.focus();
    }
});
