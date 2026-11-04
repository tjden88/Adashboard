/**
 * Base64-содержимое изображения из data URL без префикса: API принимает imageContent
 * как base64-строку, а предпросмотр в интерфейсе работает по самому data URL.
 */
export function base64FromDataUrl(dataUrl: string): string | null {
    const match = /^data:[^;,]+;base64,([A-Za-z0-9+/=\r\n]+)$/.exec(dataUrl);
    return match ? match[1] : null;
}
