<!--
Символьный индекс проекта. Сгенерирован scripts/project-map.ps1 — вручную не редактируется.
Как использовать: ищи здесь имя символа, открывай только файл из строки, читай его по месту, а не целиком.
Дата генерации: 2026-11-04 14:37
-->

## Adashboard.Client
Adashboard.Client/Components/Dashboard/Card.razor: @inject HealthCheckService HealthCheckService | @inject DashboardApiOptions ApiOptions | Task OnClickAsync() => IsEditMode ? EditRequested.InvokeAsync(Model) : | Task OnDuplicateClickAsync() => DuplicateRequested.InvokeAsync(Model); | void OnImageError() => _imageFailed = true; | async Task RunHealthLoopAsync(CancellationToken cancellationToken)
Adashboard.Client/Components/Dashboard/Category.razor: @inject IDashboardApi DashboardApi | void OpenAddCardDialog() | void OpenCardDialog(DashboardCard card) | Task OnCardSavedAsync(DashboardCard card) => CardSaved.InvokeAsync(car | async Task DuplicateCardAsync(DashboardCard card) | void RequestCardDelete(DashboardCard card)
Adashboard.Client/Components/Dashboard/PageView.razor: @inject IDashboardApi DashboardApi | @inject NavigationManager Navigation | @inject IJSRuntime JS | Task OnCategoryCreatedAsync(DashboardCategory category) | Task OnCategorySavedAsync(DashboardCategory category) | Task OnCategoryDeletedAsync(DashboardCategory category)
Adashboard.Client/Components/Dialogs/AppSettingsDialog.razor: @inject AppStateService AppStateService | static readonly (ThemeMode Mode, string Title)[] _ThemeOptions = | static readonly (SideMenuMode Mode, string Title, string Hint)[] _Side | static readonly (PageHeaderAlignment Mode, string Title)[] _HeaderAlig | async Task SaveAsync() | AppSettings ToSettings() => new()
Adashboard.Client/Components/Dialogs/CardDialog.razor: @inject IDashboardApi DashboardApi | @inject DashboardApiOptions ApiOptions | @inject IJSRuntime JS | static readonly (CardStatusMode Mode, string Title, string Hint)[] Sta | async Task LoadCategoriesAsync() | void ToggleLinkTarget() =>
Adashboard.Client/Components/Dialogs/CategoryDialog.razor: @inject IDashboardApi DashboardApi | void OnWidthInput(ChangeEventArgs args) | void OnMobileWidthInput(ChangeEventArgs args) | void OnMobileAutoChanged(ChangeEventArgs args) | async Task SaveAsync()
Adashboard.Client/Components/Dialogs/ConfirmDialog.razor: Task CancelAsync() => Dialog.CloseAsync(); | async Task ConfirmAsync()
Adashboard.Client/Components/Dialogs/DialogContainer.razor: @inject IJSRuntime JS | Task CloseAsync() => OpenChanged.InvokeAsync(false); | void SetBusy(bool isBusy) | Task RequestCloseAsync() => _isBusy ? Task.CompletedTask : CloseAsync( | Task HandleOverlayClickAsync() => CloseOnOverlayClick ? RequestCloseAs | Task HandleKeyDownAsync(KeyboardEventArgs args) => args.Key == "Escape
Adashboard.Client/Components/Dialogs/PageDialog.razor: @inject IDashboardApi DashboardApi | void OnTitleInput(ChangeEventArgs args) | void OnPathInput(ChangeEventArgs args) | void ClearIcon() => _model.Icon = string.Empty; | async Task SaveAsync() | bool IsPathTaken(string path)
Adashboard.Client/Components/Layout/ApiKeyLogin.razor: @inject AuthStateService AuthState | async Task SubmitAsync()
Adashboard.Client/Components/Layout/Header.razor: @inject NavigationManager Navigation | @inject DashboardApiOptions ApiOptions | void OnPageCreated(DashboardPage page) => Navigation.NavigateTo("/" + | void OpenSiteSettings() => _isSettingsOpen = true; | void ToggleEditMode() => AppStateService.ToggleEditMode(); | void ToggleMobileMenu() => AppStateService.ToggleMobileMenu();
Adashboard.Client/Components/Layout/MainLayout.razor: @inject IJSRuntime JS | @inject DashboardApiOptions ApiOptions | async Task InitializeThemeAsync() | async Task ApplyThemeAsync() | async ValueTask DisposeAsync()
Adashboard.Client/Components/Layout/SideMenu.razor: @inject NavigationManager Navigation | @inject IJSRuntime JS | @inject IDashboardApi DashboardApi | bool IsPageActive(string? pagePath) | string GetCurrentPath() | async Task OnNavKeyDown(KeyboardEventArgs e)
Adashboard.Client/Components/Pages/Home.razor: @page "/" | @page "/{**PagePath}" | @inject IDashboardApi DashboardApi | @inject NavigationManager Navigation | void Dispose() | Task RetryAsync() => LoadAsync(_loadedPath);
Adashboard.Client/Components/Routes.razor: @inject AppStateService AppStateService | Task OnAuthenticatedAsync() => AppStateService.LoadSettingsAsync();
Adashboard.Client/Components/Shared/ColorPicker.razor: @inject IJSRuntime JS | async Task OnInputAsync(ChangeEventArgs args) | async Task OnColorPicked(string color) | async ValueTask DisposeAsync()
Adashboard.Client/Components/Shared/IconPicker.razor: string OptionId(int index) => $"{_listId}-{index}"; | void OnSearchInput(ChangeEventArgs args) | async Task OnKeyDownAsync(KeyboardEventArgs args) | async Task SelectIconAsync(string icon)
Adashboard.Client/Helpers/ApiKeyHandler.cs: sealed class ApiKeyHandler(ApiKeyStore store) : DelegatingHandler
Adashboard.Client/Helpers/ApiResponseExtensions.cs: static class ApiResponseExtensions
Adashboard.Client/Helpers/NetworkErrorHandler.cs: sealed class NetworkErrorHandler : DelegatingHandler
Adashboard.Client/Helpers/PageDisplayExtensions.cs: static class PageDisplayExtensions
Adashboard.Client/Models/DashboardPageModel.cs: sealed class DashboardPageModel | void NotifyPageChanged() => PageInfoChanged?.Invoke();
Adashboard.Client/Services/ApiKeyStore.cs: sealed class ApiKeyStore(IJSRuntime js, ILogger<ApiKeyStore> logger) : | Task NotifyUnauthorizedAsync() => Unauthorized?.Invoke() ?? Task.Compl
Adashboard.Client/Services/AppStateService.cs: sealed class AppStateService | void ToggleEditMode() | void OpenMobileMenu() | void CloseMobileMenu() | void ToggleMobileMenu() | void SetSettings(AppSettings settings)
Adashboard.Client/Services/AuthStateService.cs: sealed class AuthStateService : IDisposable | void Dispose() => _store.Unauthorized -= OnUnauthorizedAsync;
Adashboard.Client/Services/DashboardApiOptions.cs: sealed class DashboardApiOptions | string? ResolveUploadPath(string? path)
Adashboard.Client/Services/HealthCheckService.cs: sealed class HealthCheckService(IDashboardApi dashboardApi, ILogger<He

## Adashboard.Server
Adashboard.Server/Auth/ApiKeyAuthenticationHandler.cs: sealed class ApiKeyAuthenticationHandler( | const string SchemeName = "ApiKey";
Adashboard.Server/Auth/ApiKeySettings.cs: sealed class ApiKeySettings | bool Matches(string? provided)
Adashboard.Server/Controllers/CardsController.cs: class CardsController(PageService pageService, ILogger<CardsController
Adashboard.Server/Controllers/CategoriesController.cs: class CategoriesController(
Adashboard.Server/Controllers/HealthController.cs: class HealthController : ControllerBase | IActionResult Get() | IActionResult CheckAccess()
Adashboard.Server/Controllers/PageController.cs: class PageController(
Adashboard.Server/Controllers/ProbeController.cs: class ProbeController(ProbeService probeService, ILogger<ProbeControll
Adashboard.Server/Controllers/SettingsController.cs: class SettingsController(AppSettingsService settingsService, ILogger<S
Adashboard.Server/Helpers/ImageSignature.cs: static class ImageSignature
Adashboard.Server/Mapping/MapperConfig.cs: static class MapperConfig
Adashboard.Server/Services/AppSettingsService.cs: sealed class AppSettingsService(DashboardDb db, ILogger<AppSettingsSer
Adashboard.Server/Services/AssetsInitializer.cs: sealed class AssetsInitializer(DashboardPaths paths, ILogger<AssetsIni | void Initialize()
Adashboard.Server/Services/DashboardPaths.cs: sealed class DashboardPaths(string rootPath)
Adashboard.Server/Services/ImageIconService.cs: sealed class ImageIconService(DashboardPaths paths, ILogger<ImageIconS | void Delete(int cardId)
Adashboard.Server/Services/PageService.cs: sealed class PageService(DashboardDb db, ILogger<PageService> logger,
Adashboard.Server/Services/ProbeService.cs: sealed class ProbeService(DashboardDb db, HttpClient client, ILogger<P

## Adashboard.Domain
Adashboard.Domain/Contracts/AppSettings.cs: sealed class AppSettings
Adashboard.Domain/Contracts/DashboardCard.cs: sealed class DashboardCard
Adashboard.Domain/Contracts/DashboardCategory.cs: sealed class DashboardCategory
Adashboard.Domain/Contracts/DashboardPage.cs: sealed class DashboardPage
Adashboard.Domain/Contracts/SaveCardRequest.cs: sealed class SaveCardRequest
Adashboard.Domain/Enums/CardLinkTarget.cs: enum CardLinkTarget
Adashboard.Domain/Enums/CardStatusMode.cs: enum CardStatusMode
Adashboard.Domain/Enums/HealthStatus.cs: enum HealthStatus
Adashboard.Domain/Enums/PageHeaderAlignment.cs: enum PageHeaderAlignment
Adashboard.Domain/Enums/SideMenuMode.cs: enum SideMenuMode
Adashboard.Domain/Enums/ThemeMode.cs: enum ThemeMode
Adashboard.Domain/Enums/UrlScheme.cs: enum UrlScheme
Adashboard.Domain/Extensions/CardPresentationExtensions.cs: static class CardPresentationExtensions
Adashboard.Domain/Extensions/UrlSchemeExtensions.cs: static class UrlSchemeExtensions
Adashboard.Domain/ReadModels/DashboardCategoryReadModel.cs: sealed record DashboardCategoryReadModel
Adashboard.Domain/ReadModels/DashboardPageReadModel.cs: sealed record DashboardPageReadModel
Adashboard.Domain/ReadModels/HealthCheckResult.cs: record HealthCheckResult(HealthStatus Status, string Message);
Adashboard.Domain/Reference/DomainConstants.cs: static class DomainConstants | const int CardTitleMaxLength = 120; | const int CardUrlMaxLength = 500; | const int CategoryTitleMaxLength = 120; | const int CategoryWidthMin = 1; | const int CategoryWidthMax = 12;
Adashboard.Domain/Reference/FontAwesomeIcons.cs: static class FontAwesomeIcons
Adashboard.Domain/Reference/PagePathRules.cs: static class PagePathRules | const int MaxLength = DomainConstants.PagePathMaxLength;

## Adashboard.Data
Adashboard.Data/DashboardDb.cs: sealed class DashboardDb(DbContextOptions<DashboardDb> options) : DbCo
Adashboard.Data/DbInitializer.cs: sealed class DbInitializer(DashboardDb db, ILogger<DbInitializer> logg
Adashboard.Data/Entities/AppSettingsEntity.cs: sealed class AppSettingsEntity
Adashboard.Data/Entities/DashboardCardEntity.cs: sealed class DashboardCardEntity
Adashboard.Data/Entities/DashboardCategoryEntity.cs: sealed class DashboardCategoryEntity
Adashboard.Data/Entities/DashboardPageEntity.cs: sealed class DashboardPageEntity
Adashboard.Data/Migrations/20261101174632_Initial.cs: partial class Initial : Migration
Adashboard.Data/Migrations/20261103171634_PageOrderAndMobileCategory.cs: partial class PageOrderAndMobileCategory : Migration
Adashboard.Data/Migrations/20261104093434_AddPageHeaderAlignment.cs: partial class AddPageHeaderAlignment : Migration

## Adashboard.Extension
Adashboard.Extension/src/api.ts: export const REQUEST_TIMEOUT_MS = 10_000; | export class ApiError extends Error { | export function errorText(error: unknown): string { | export class ApiClient {
Adashboard.Extension/src/browser.ts: export const browserApi = globalThis.browser ?? globalThis.chrome;
Adashboard.Extension/src/constants.ts: export const CARD_TITLE_MAX_LENGTH = 120; | export const CARD_URL_MAX_LENGTH = 500; | export const DEFAULT_CARD_ICON = "fa-solid fa-link"; | export const DEFAULT_CARD_BACKGROUND_COLOR = "#1e40af";
Adashboard.Extension/src/dom.ts: export function find<T extends HTMLElement>(id: string): T {
Adashboard.Extension/src/icon.ts: export function base64FromDataUrl(dataUrl: string): string | null {
Adashboard.Extension/src/storage.ts: export async function readSettings(): Promise<ExtensionSettings | null | export async function saveSettings(settings: ExtensionSettings): Promi | export async function readLastPageId(): Promise<number | null> { | export async function saveLastPageId(pageId: number): Promise<void> { | export async function readCategoryId(pageId: number): Promise<number | | export async function saveCategoryId(pageId: number, categoryId: numbe
Adashboard.Extension/src/theme.ts: export function normalizeThemeColor(value: string | null): string | nu | export async function cardBackgroundColor(tabId: number | null): Promi
Adashboard.Extension/src/types.ts: export enum UrlScheme { | export enum CardLinkTarget { | export enum CardStatusMode { | export interface DashboardPage { | export interface DashboardCategory { | export interface DashboardCard {
Adashboard.Extension/src/url.ts: export interface CardUrlParts { | export function parseCardUrl(rawUrl: string | undefined): CardUrlParts | export function cardTitle(tabTitle: string | undefined, host: string): | export function isUrlLengthValid(value: string): boolean {

