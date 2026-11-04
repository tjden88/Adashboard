import "./styles.css";
import { ApiClient, errorText } from "./api";
import { browserApi } from "./browser";
import { CARD_TITLE_MAX_LENGTH, CARD_URL_MAX_LENGTH, DEFAULT_CARD_BACKGROUND_COLOR, DEFAULT_CARD_ICON } from "./constants";
import { find } from "./dom";
import { base64FromDataUrl } from "./icon";
import { readCategoryId, readLastPageId, readSettings, saveCategoryId, saveLastPageId } from "./storage";
import { cardBackgroundColor } from "./theme";
import { cardTitle, isUrlLengthValid, parseCardUrl } from "./url";
import type { CardUrlParts } from "./url";
import { CardLinkTarget, CardStatusMode, UrlScheme } from "./types";
import type { DashboardCard, DashboardCategory, DashboardPage, DashboardPageReadModel, SaveCardRequest } from "./types";

/** Название категории, которую создаёт расширение. */
const NEW_CATEGORY_TITLE = "Закладки";

/** Подпись варианта «будет создана», когда на странице нет ни одной категории. */
const NEW_CATEGORY_LABEL = "Закладки (будет создана)";

/** Значение селектора для создаваемой категории: реального идентификатора ещё нет. */
const CREATE_CATEGORY_VALUE = 0;

/** Ширина новой категории по умолчанию, как значение по умолчанию DashboardCategory в домене. */
const NEW_CATEGORY_WIDTH = 6;

/** Состояния иконки: они живут отдельно, потому что иконка необязательна и не влияет на остальной интерфейс. */
type IconState = "loading" | "none" | "shown" | "hidden";

/** Состояние попапа после проверки настроек и адреса вкладки. */
interface PopupState {
    api: ApiClient;

    /** Отменяет незавершённые запросы, когда попап закрывается. */
    signal: AbortSignal;

    pageId: number;
    categoryId: number;

    /** Загруженные страницы и категории выбранной страницы: повтор после отказа повторяет только неудачный шаг. */
    pages: DashboardPage[];
    categories: DashboardCategory[];
    models: Map<number, DashboardPageReadModel>;

    /** Полный адрес вкладки — источник данных карточки. */
    sourceUrl: string;

    /** Идентификатор вкладки для чтения theme-color; null — вкладка недоступна. */
    tabId: number | null;

    /** Цвет фона карточки: читается один раз при открытии попапа и больше не запрашивается. */
    backgroundColor: string;

    /** Иконка как data URL; null — иконки нет, карточка сохранится с fa-solid fa-link. */
    iconDataUrl: string | null;

    iconState: IconState;
}

/** Вариант селектора: значение — идентификатор, подпись — название элемента. */
interface SelectOption {
    value: number;
    label: string;
}

/** Вариант селектора для списка. */
function selectOption(value: number, label: string): SelectOption {
    return { value, label };
}

/** Элементы разметки попапа: собираются один раз, дальше меняются только значения и видимость. */
const ui = {
    status: find("status"),
    error: find("error"),
    settingsButton: find<HTMLButtonElement>("settings-button"),
    retryButton: find<HTMLButtonElement>("retry-button"),
    form: find<HTMLFormElement>("card-form"),
    titleInput: find<HTMLInputElement>("title-input"),
    urlInput: find<HTMLInputElement>("url-input"),
    schemeSelect: find<HTMLSelectElement>("scheme-select"),
    pageSelect: find<HTMLSelectElement>("page-select"),
    categorySelect: find<HTMLSelectElement>("category-select"),
    iconBlock: find<HTMLDivElement>("icon-block"),
    iconImage: find<HTMLImageElement>("icon-image"),
    iconFallback: find<HTMLSpanElement>("icon-fallback"),
    iconStatus: find<HTMLSpanElement>("icon-status"),
    iconClearButton: find<HTMLButtonElement>("icon-clear"),
    iconRestoreButton: find<HTMLButtonElement>("icon-restore"),
    colorRow: find<HTMLParagraphElement>("color-row"),
    colorValue: find<HTMLSpanElement>("color-value"),
    saveButton: find<HTMLButtonElement>("save-button"),
};

/** Что повторять по кнопке «Повторить»: только неудачный шаг, успешно загруженное не запрашивается снова. */
let retryAction: (() => Promise<void>) | null = null;

/** Обработчики навешиваются один раз, чтобы повтор начального шага не добавлял вторые обработчики. */
let commonEventsWired = false;
let formEventsWired = false;

/**
 * Точка входа попапа: настройки, активная вкладка, страницы и категория, иконка, цвет фона
 * и сохранение карточки. Отказ хранилища или запроса вкладки тоже показываем с повтором.
 */
async function start(): Promise<void> {
    // Ошибка предыдущей попытки неактуальна, когда шаг повторяют.
    hideError();

    // Кнопки нужны и без состояния: настроек может не быть, а начальный шаг может отказать,
    // и тогда выход из start() происходит до формы.
    wireCommonEvents();

    try {
        const settings = await readSettings();
        if (!settings) {
            setStatus("Настройки не заданы: укажите адрес сервера и ключ доступа.");
            ui.settingsButton.hidden = false;
            return;
        }

        const tab = await activeTab();
        const cardUrl = parseCardUrl(tab?.url);
        if (!cardUrl) {
            setStatus("Добавить можно только страницу со схемой http или https.");
            return;
        }

        const state: PopupState = {
            api: new ApiClient(settings),
            signal: abortOnClose(),
            pageId: 0,
            categoryId: 0,
            pages: [],
            categories: [],
            models: new Map(),
            sourceUrl: tab?.url ?? "",
            tabId: tab?.id ?? null,
            backgroundColor: DEFAULT_CARD_BACKGROUND_COLOR,
            iconDataUrl: null,
            iconState: "loading",
        };

        fillFromTab(cardUrl, tab?.title);
        wireFormEvents(state);
        ui.form.hidden = false;

        // Иконка и цвет необязательны: отказ молча означает значение по умолчанию, поэтому их читаем
        // параллельно со страницами и без повтора.
        void loadIcon(state);
        void loadBackgroundColor(state);

        await runStep(() => loadPages(state));
    } catch (error) {
        // Хранилище или активная вкладка: повтор повторяет весь начальный шаг, запросов при этом не было.
        showError(errorText(error), () => start());
    }
}

/** Активная вкладка текущего окна; undefined — вкладка недоступна. */
async function activeTab(): Promise<chrome.tabs.Tab | undefined> {
    const tabs = await browserApi.tabs.query({ active: true, currentWindow: true });
    return tabs[0];
}

/**
 * Загружает страницы и категории выбранной страницы: запомненная страница, иначе первая
 * по порядку (API отдаёт страницы по Order).
 */
async function loadPages(state: PopupState): Promise<void> {
    setStatus("Загружаю страницы…");

    // Повтор после отказа повторяет только неудачный шаг: загруженные страницы не запрашиваем снова.
    if (state.pages.length === 0) state.pages = await state.api.getPages(state.signal);

    if (state.pages.length === 0) {
        setStatus("На сервере нет ни одной страницы: сначала создайте страницу в Adashboard.");
        return;
    }

    const remembered = await readLastPageId();
    state.pageId = state.pages.find((page) => page.id === remembered)?.id ?? state.pages[0].id;

    renderOptions(ui.pageSelect, state.pages.map((page) => selectOption(page.id, page.title)), state.pageId);
    await loadCategories(state);

    setStatus("");
    setSaveEnabled(isUrlLengthValid(ui.urlInput.value));
}

/** Категории страницы: модель берётся из кеша, поэтому повторный выбор той же страницы не повторяет запрос. */
async function loadCategories(state: PopupState): Promise<void> {
    const model = state.models.get(state.pageId) ?? (await state.api.getPage(state.pageId, state.signal));
    state.models.set(state.pageId, model);

    // Категории API отдаёт по Order: категория уже выбрана, если она была запомнена.
    state.categories = model.categories;
    state.categoryId = await chooseCategoryId(state);

    const options = state.categories.length === 0
        ? [selectOption(CREATE_CATEGORY_VALUE, NEW_CATEGORY_LABEL)]
        : state.categories.map((category) => selectOption(category.id, category.title));

    renderOptions(ui.categorySelect, options, state.categoryId);
}

/** Категория страницы: запомненная, иначе первая по порядку; если запомненной больше нет — тоже первая. */
async function chooseCategoryId(state: PopupState): Promise<number> {
    const remembered = await readCategoryId(state.pageId);
    if (remembered !== null && state.categories.some((category) => category.id === remembered)) return remembered;

    return state.categories.length === 0 ? CREATE_CATEGORY_VALUE : state.categories[0].id;
}

/** Выбор страницы запоминается и подтягивает категории этой страницы. */
async function selectPage(state: PopupState, pageId: number): Promise<void> {
    state.pageId = pageId;
    await saveLastPageId(pageId);
    await loadCategories(state);
}

/** Выбор категории запоминается отдельно для каждой страницы. */
async function rememberCategory(state: PopupState, categoryId: number): Promise<void> {
    // Вариант «будет создана» запоминаем только после реального создания категории при сохранении.
    if (categoryId > 0) await saveCategoryId(state.pageId, categoryId);

    state.categoryId = categoryId;
}

/**
 * Запрашивает иконку ровно один раз на открытие попапа. Отказ не показываем: без иконки
 * карточка сохраняется с fa-solid fa-link.
 */
async function loadIcon(state: PopupState): Promise<void> {
    try {
        const dataUrl = await state.api.getIconDataUrl(state.sourceUrl, state.signal);
        state.iconDataUrl = dataUrl;
        ui.iconImage.src = dataUrl;
        state.iconState = "shown";
    } catch {
        state.iconState = "none";
    }

    renderIcon(state);
}

/** Предпросмотр иконки: без полученной иконки блок не показываем вообще. */
function renderIcon(state: PopupState): void {
    ui.iconBlock.hidden = state.iconState === "none";
    ui.iconStatus.hidden = state.iconState !== "loading";
    ui.iconImage.hidden = state.iconState !== "shown";
    ui.iconFallback.hidden = state.iconState !== "hidden";
    ui.iconClearButton.hidden = state.iconState !== "shown";
    ui.iconRestoreButton.hidden = state.iconState !== "hidden";
}

/** «Очистить» и «Вернуть» меняют только видимость: полученные данные остаются, повторного запроса нет. */
function showIcon(state: PopupState, visible: boolean): void {
    state.iconState = visible ? "shown" : "hidden";
    renderIcon(state);
}

/**
 * Цвет фона читаем один раз на открытие попапа и кешируем в состоянии, чтобы сохранение
 * не зависело от executeScript. Отказ на вкладке не показываем как ошибку интерфейса:
 * образец просто показывает цвет по умолчанию.
 */
async function loadBackgroundColor(state: PopupState): Promise<void> {
    state.backgroundColor = await cardBackgroundColor(state.tabId);

    // Строку показываем ровно один раз, когда значение известно: перерисовки при сохранении нет.
    ui.colorRow.style.setProperty("--card-color", state.backgroundColor);
    ui.colorValue.textContent = state.backgroundColor;
    ui.colorRow.hidden = false;
}

/** Подставляет значения из вкладки: заголовок и адрес без схемы, схема — отдельным выбором. */
function fillFromTab(cardUrl: CardUrlParts, tabTitle: string | undefined): void {
    ui.titleInput.maxLength = CARD_TITLE_MAX_LENGTH;
    ui.urlInput.maxLength = CARD_URL_MAX_LENGTH;

    ui.titleInput.value = cardTitle(tabTitle, cardUrl.host);
    ui.urlInput.value = cardUrl.rest;
    ui.schemeSelect.value = String(cardUrl.scheme);

    // Длинный адрес не сохраняем: поле показывает полное значение, чтобы было видно, что сократить.
    if (!isUrlLengthValid(cardUrl.rest)) {
        showError(`Адрес длиннее ${CARD_URL_MAX_LENGTH} символов: сократите его, карточку не сохраняем.`);
    }
}

/** Сохраняет карточку: категорию создаём только при сохранении, чтобы не плодить пустые категории. */
async function saveCard(state: PopupState): Promise<void> {
    const title = ui.titleInput.value.trim();
    const url = ui.urlInput.value.trim();

    if (!title) {
        showError("Укажите заголовок карточки.");
        return;
    }

    if (!url) {
        showError("Укажите адрес карточки.");
        return;
    }

    if (!isUrlLengthValid(url)) {
        showError(`Адрес длиннее ${CARD_URL_MAX_LENGTH} символов: сократите его.`);
        return;
    }

    setStatus("Сохраняю…");
    setSaveEnabled(false);

    try {
        const categoryId = state.categoryId === CREATE_CATEGORY_VALUE ? await createCategory(state) : state.categoryId;

        // Пользовательская иконка сохраняется, только когда предпросмотр не скрыт кнопкой «Очистить».
        const imageContent = state.iconState === "shown" && state.iconDataUrl ? base64FromDataUrl(state.iconDataUrl) : null;

        const card: DashboardCard = {
            id: 0,
            dashboardCategoryId: categoryId,
            title,
            url,
            urlScheme: selectedScheme(),
            linkTarget: CardLinkTarget.NewTab,
            isCustomIcon: imageContent !== null,
            // С картинкой сервер сам заполнит путь файла, поэтому здесь пустая строка.
            iconSource: imageContent ? "" : DEFAULT_CARD_ICON,
            // Цвет уже прочитан при открытии попапа: сохранение не запрашивает его снова.
            backgroundColor: state.backgroundColor,
            isWide: false,
            // Положение в конце категории задаёт сервер, поэтому здесь оставляем ноль.
            position: 0,
            statusMode: CardStatusMode.None,
        };

        const request: SaveCardRequest = { card, imageContent };
        const created = await state.api.createCard(request, state.signal);

        // Запоминаем выбор, чтобы следующее открытие попапа открыло ту же страницу и категорию.
        await saveLastPageId(state.pageId);
        await saveCategoryId(state.pageId, created.dashboardCategoryId);

        window.close();
    } catch (error) {
        // Данные никуда не делись: сохранение повторяют той же кнопкой.
        setSaveEnabled(true);
        showError(errorText(error), () => saveCard(state));
    }
}

/**
 * Создаёт категорию при сохранении: пустую категорию из-за простого открытия попапа не создаём.
 * Порядок — следующий после максимального на странице, как в клиенте (PageView.NextCategoryOrder);
 * для «Закладок» категорий нет, поэтому начало отсчёта — 0.
 */
async function createCategory(state: PopupState): Promise<number> {
    const created = await state.api.createCategory(
        {
            id: 0,
            dashboardPageId: state.pageId,
            title: NEW_CATEGORY_TITLE,
            order: state.categories.length > 0 ? Math.max(...state.categories.map((category) => category.order)) + 1 : 0,
            width: NEW_CATEGORY_WIDTH,
            mobileWidth: null,
        },
        state.signal,
    );

    await saveCategoryId(state.pageId, created.id);
    return created.id;
}

/** Схема из селектора: значения select — строки, а в контракте схема — число. */
function selectedScheme(): UrlScheme {
    return Number(ui.schemeSelect.value) === UrlScheme.Https ? UrlScheme.Https : UrlScheme.Http;
}

/** Заполняет селектор и выделяет выбранное значение. */
function renderOptions(select: HTMLSelectElement, options: SelectOption[], selected: number): void {
    select.replaceChildren();

    for (const option of options) {
        const node = document.createElement("option");
        node.value = String(option.value);
        node.textContent = option.label;
        select.append(node);
    }

    select.value = String(selected);
}

/** Общие кнопки попапа: они нужны и когда состояния нет, поэтому навешиваются до проверки настроек. */
function wireCommonEvents(): void {
    if (commonEventsWired) return;
    commonEventsWired = true;

    ui.settingsButton.addEventListener("click", () => void openOptionsPage());
    ui.retryButton.addEventListener("click", () => void retryAction?.());
}

/**
 * Открывает страницу настроек: отказ возможен, например в Firefox, поэтому его показываем в попапе,
 * а не оставляем незамеченным. Попап закрываем, иначе он висит за новой вкладкой.
 */
async function openOptionsPage(): Promise<void> {
    try {
        await browserApi.runtime.openOptionsPage();
        window.close();
    } catch (error) {
        showError(errorText(error));
    }
}

/** Обработчики элементов формы: они работают только когда форма видима. */
function wireFormEvents(state: PopupState): void {
    // Повтор начального шага не должен добавлять вторые обработчики.
    if (formEventsWired) return;
    formEventsWired = true;

    ui.pageSelect.addEventListener("change", () => void runStep(() => selectPage(state, Number(ui.pageSelect.value))));
    ui.categorySelect.addEventListener("change", () => void runStep(() => rememberCategory(state, Number(ui.categorySelect.value))));

    // Когда пользователь исправляет длинный адрес, блокировка сохранения снимается сразу.
    ui.urlInput.addEventListener("input", () => {
        hideError();
        setSaveEnabled(isUrlLengthValid(ui.urlInput.value));
    });

    ui.iconClearButton.addEventListener("click", () => showIcon(state, false));
    ui.iconRestoreButton.addEventListener("click", () => showIcon(state, true));

    ui.form.addEventListener("submit", (event) => {
        event.preventDefault();
        void saveCard(state);
    });
}

/**
 * Выполняет шаг загрузки и при отказе показывает ошибку с повтором именно этого шага.
 * После успешного шага ошибка предыдущей попытки больше не нужна.
 */
async function runStep(action: () => Promise<void>): Promise<void> {
    try {
        await action();
        hideError();
    } catch (error) {
        showError(errorText(error), () => runStep(action));
    }
}

/** Строка состояния: загрузка, пустой результат или причина, по которой сохранить нельзя. */
function setStatus(message: string): void {
    ui.status.textContent = message;
    ui.status.hidden = message.length === 0;
}

/** Ошибка: текст из тела ответа сервера или понятное сообщение о сетевом отказе, повтор — кнопкой рядом. */
function showError(message: string, retry?: () => Promise<void>): void {
    setStatus("");
    ui.error.textContent = message;
    ui.error.hidden = false;

    retryAction = retry ?? null;
    ui.retryButton.hidden = retryAction === null;
}

/** Убирает ошибку: она неактуальна, когда шаг успешно выполнен или значение исправлено. */
function hideError(): void {
    ui.error.textContent = "";
    ui.error.hidden = true;

    retryAction = null;
    ui.retryButton.hidden = true;
}

/** Сохранение доступно, когда данные загружены и адрес корректен; во время запроса кнопка заблокирована. */
function setSaveEnabled(enabled: boolean): void {
    ui.saveButton.disabled = !enabled;
}

/** Отменяет незавершённые запросы, когда попап закрывается. */
function abortOnClose(): AbortSignal {
    const controller = new AbortController();
    window.addEventListener("pagehide", () => controller.abort());
    return controller.signal;
}

void start();
