/**
 * Контрактные типы API Adashboard: отражают модели Adashboard.Domain.
 * JSON в camelCase, enum'ы в JSON — числа.
 */

/** Схема, добавляемая к адресу карточки при открытии ссылки. */
export enum UrlScheme {
    None = 0,
    Http = 1,
    Https = 2,
}

/** Способ открытия ссылки карточки. */
export enum CardLinkTarget {
    NewTab = 0,
    SameTab = 1,
}

/** Режим проверки доступности сервиса по карточке. */
export enum CardStatusMode {
    None = 0,
    Reachable = 1,
    NoServerError = 2,
    Success = 3,
}

/** Страница dashboard — плоская контрактная модель, как её отдаёт API. */
export interface DashboardPage {
    id: number;
    title: string;
    description: string;
    path: string;
    icon: string;
    iconColor: string;
    order: number;
}

/** Категория ссылок внутри страницы. */
export interface DashboardCategory {
    id: number;
    dashboardPageId: number;
    title: string;
    order: number;
    width: number;
    /** null — мобильная ширина берётся из width, но не больше 4 колонок. */
    mobileWidth: number | null;
}

/** Карточка ссылки внутри категории. */
export interface DashboardCard {
    id: number;
    dashboardCategoryId: number;
    title: string;
    /** Адрес без схемы: схема хранится отдельно в urlScheme. */
    url: string;
    urlScheme: UrlScheme;
    linkTarget: CardLinkTarget;
    isCustomIcon: boolean;
    /** CSS-класс иконки Font Awesome либо путь к загруженному изображению. */
    iconSource: string;
    backgroundColor: string;
    isWide: boolean;
    statusMode: CardStatusMode;
    position: number;
}

/** Запрос создания карточки: карточка и необязательное изображение-иконка как base64 без префикса data URL. */
export interface SaveCardRequest {
    card: DashboardCard;
    imageContent: string | null;
}

/** Ответ GET /api/pages/{id}: страница с категориями и карточками. */
export interface DashboardPageReadModel {
    page: DashboardPage;
    categories: DashboardCategory[];
    cards: DashboardCard[];
}

/** Настройки расширения. Живут только в storage.local браузера: ключ не хранится в репозитории. */
export interface ExtensionSettings {
    /** Базовый адрес сервера без завершающего слэша, например http://localhost:8080. */
    serverUrl: string;
    apiKey: string;
}
