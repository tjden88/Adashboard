// Вставка изображения из буфера обмена для поля иконки карточки.
let pasteHandler = null;
let dotNetRef = null;

function readAsDataUrl(file) {
    return new Promise((resolve, reject) => {
        const reader = new FileReader();
        reader.onload = () => resolve(reader.result);
        reader.onerror = () => reject(reader.error);
        reader.readAsDataURL(file);
    });
}

async function onPaste(event) {
    const items = event.clipboardData?.items;
    if (!items || !dotNetRef) {
        return;
    }

    for (const item of items) {
        if (item.kind !== "file" || !item.type.startsWith("image/")) {
            continue;
        }

        const file = item.getAsFile();
        if (!file) {
            continue;
        }

        event.preventDefault();

        try {
            const dataUrl = await readAsDataUrl(file);
            await dotNetRef.invokeMethodAsync("OnClipboardImage", dataUrl);
        } catch (error) {
            console.error("Не удалось прочитать изображение из буфера обмена", error);
        }

        return;
    }
}

export function initializeClipboard(ref) {
    disposeClipboard();

    dotNetRef = ref;
    pasteHandler = onPaste;
    document.addEventListener("paste", pasteHandler, true);
}

export function disposeClipboard() {
    if (pasteHandler) {
        document.removeEventListener("paste", pasteHandler, true);
    }

    pasteHandler = null;
    dotNetRef = null;
}
