<script lang="ts">
	// Диалог создания и редактирования карточки ссылки.
	// Позволяет задать заголовок, адрес со схемой, иконку (встроенную или своё изображение),
	// цвет фона, признак «широкая» и режим проверки статуса.
	import { untrack } from 'svelte';
	import BaseDialog from './BaseDialog.svelte';
	import ColorPickerField from './ColorPickerField.svelte';
	import IconPicker from './IconPicker.svelte';
	import { fetchPageTitle } from '../dashboard/pageTitle';
	import {
		DEFAULT_CARD_COLOR,
		DEFAULT_CARD_ICON,
		type CardStatusMode,
		type DashboardCard
	} from '../dashboard/types';
	import { isSafeUrl, isValidHexColor, normalizeUrl, splitScheme } from '../dashboard/url';

	// Поля, которые диалог возвращает наружу. Идентификатор и порядок назначает хранилище.
	type CardFormData = Omit<DashboardCard, 'id' | 'categoryId' | 'order'>;

	let { card = null, onsave, ondelete = null, onclose }: {
		card?: DashboardCard | null;
		onsave: (data: CardFormData) => void;
		ondelete?: (() => void) | null;
		onclose: () => void;
	} = $props();

	/** Максимальный размер принимаемого изображения (как в старом сервисе иконок). */
	const MAX_IMAGE_BYTES = 2 * 1024 * 1024;

	/** Варианты проверки статуса, как в Blazor-диалоге. */
	const statusOptions: { mode: CardStatusMode; title: string; hint: string }[] = [
		{ mode: 'none', title: 'Не проверять', hint: 'Индикатор скрыт.' },
		{ mode: 'reachable', title: 'Сервис отвечает', hint: 'Любой ответ сервера.' },
		{ mode: 'noServerError', title: 'Без серверных ошибок', hint: 'Кроме ошибок 5xx.' },
		{ mode: 'success', title: 'Успешный ответ (2xx)', hint: 'Только ответ 2xx.' }
	];

	// Диалог монтируется заново на каждое открытие, поэтому начальные значения
	// считываем один раз через untrack — реактивное отслеживание здесь не нужно.
	const initial = untrack(() => {
		const cardValue = card;
		const parsed = cardValue
			? cardValue.urlScheme.length > 0
				? { scheme: cardValue.urlScheme, url: cardValue.url }
				: splitScheme(cardValue.url)
			: { scheme: 'https://', url: '' };

		return {
			title: cardValue?.title ?? '',
			url: parsed.url,
			scheme: parsed.scheme,
			color: cardValue?.backgroundColor ?? DEFAULT_CARD_COLOR,
			icon: cardValue?.iconClass ?? '',
			statusMode: cardValue?.statusMode ?? 'none',
			isWide: cardValue?.isWide ?? false,
			useCustomImage: Boolean(cardValue?.imagePath),
			existingImagePath: cardValue?.imagePath ?? ''
		};
	});

	let title = $state(initial.title);
	let url = $state(initial.url);
	let scheme = $state(initial.scheme);
	let color = $state(initial.color);
	let icon = $state(initial.icon);
	let statusMode = $state<CardStatusMode>(initial.statusMode);
	let isWide = $state(initial.isWide);

	// Режим иконки: встроенная Font Awesome или своё изображение.
	let useCustomImage = $state(initial.useCustomImage);
	let imageUrl = $state('');
	let uploadedPreview = $state<string | null>(null);
	const existingImagePath = initial.existingImagePath;

	let error = $state<string | null>(null);
	let imageError = $state<string | null>(null);
	let isFetchingTitle = $state(false);
	let titleLookupError = $state<string | null>(null);

	// Предпросмотр: загруженный файл, ссылка или уже сохранённое изображение.
	const previewSource = $derived(uploadedPreview ?? (imageUrl.trim() || existingImagePath || null));

	const schemeLabel = $derived(scheme.length === 0 ? '○' : scheme);

	function cycleScheme() {
		scheme = scheme === 'http://' ? 'https://' : scheme === 'https://' ? '' : 'http://';
	}

	function handleImageUrlInput(event: Event) {
		imageUrl = (event.currentTarget as HTMLInputElement).value;

		// Ссылка имеет приоритет над вставленным ранее изображением.
		if (imageUrl.length > 0) {
			uploadedPreview = null;
		}

		imageError = null;
	}

	/** Читает изображение из буфера обмена и превращает его в data URL. */
	function handlePaste(event: ClipboardEvent) {
		if (!useCustomImage) {
			return;
		}

		const items = event.clipboardData?.items;
		if (!items) {
			return;
		}

		for (const item of items) {
			if (item.kind !== 'file' || !item.type.startsWith('image/')) {
				continue;
			}

			const file = item.getAsFile();
			if (!file) {
				continue;
			}

			event.preventDefault();

			if (file.size > MAX_IMAGE_BYTES) {
				imageError = 'Изображение превышает максимальный размер 2 МБ.';
				return;
			}

			const reader = new FileReader();
			reader.onload = () => {
				uploadedPreview = typeof reader.result === 'string' ? reader.result : null;
				imageUrl = '';
				imageError = null;
			};
			reader.onerror = () => {
				imageError = 'Не удалось прочитать изображение из буфера обмена.';
			};
			reader.readAsDataURL(file);
			return;
		}
	}

	function clearImage() {
		imageUrl = '';
		uploadedPreview = null;
		useCustomImage = false;
		imageError = null;
	}

	/** Пытается подставить заголовок страницы по адресу карточки. */
	async function fetchTitle() {
		titleLookupError = null;
		const { scheme: normalizedScheme, url: normalizedUrl } = normalizeUrl(scheme, url);

		if (normalizedUrl.length === 0) {
			titleLookupError = 'Введите ссылку.';
			return;
		}

		if (!isSafeUrl(normalizedScheme, normalizedUrl)) {
			titleLookupError = 'Недопустимая схема ссылки.';
			return;
		}

		isFetchingTitle = true;

		try {
			const found = await fetchPageTitle(normalizedScheme + normalizedUrl);
			if (!found) {
				titleLookupError = 'Не удалось найти заголовок на странице.';
			} else {
				title = found;
			}
		} catch (caught) {
			titleLookupError = caught instanceof Error ? caught.message : 'Не удалось получить заголовок страницы.';
		} finally {
			isFetchingTitle = false;
		}
	}

	function save() {
		error = null;
		imageError = null;
		titleLookupError = null;

		const trimmedIcon = icon.trim();
		const { scheme: normalizedScheme, url: normalizedUrl } = normalizeUrl(scheme, url);

		if (normalizedUrl.length === 0) {
			error = 'Введите ссылку.';
			return;
		}

		if (!isSafeUrl(normalizedScheme, normalizedUrl)) {
			error = 'Недопустимая схема ссылки.';
			return;
		}

		const normalizedColor = color.trim().length === 0 ? DEFAULT_CARD_COLOR : color.trim();
		if (!isValidHexColor(normalizedColor)) {
			error = 'Цвет фона должен быть в формате #RRGGBB.';
			return;
		}

		let imagePath = '';
		if (useCustomImage) {
			// Приоритет у вставленного изображения, затем у ссылки, затем у уже сохранённого.
			if (uploadedPreview) {
				imagePath = uploadedPreview;
			} else if (imageUrl.trim().length > 0) {
				imagePath = imageUrl.trim();
			} else if (existingImagePath.length > 0) {
				imagePath = existingImagePath;
			} else {
				imageError = 'Укажите ссылку или вставьте изображение из буфера.';
				return;
			}
		}

		onsave({
			title: title.trim(),
			url: normalizedUrl,
			urlScheme: normalizedScheme,
			iconClass: trimmedIcon.length === 0 ? DEFAULT_CARD_ICON : trimmedIcon,
			imagePath,
			backgroundColor: normalizedColor,
			isWide,
			statusMode
		});
	}
</script>

<svelte:window onpaste={handlePaste} />

<BaseDialog open={true} title={card ? `Редактировать карточку: ${card.title}` : 'Добавить карточку'} onclose={onclose}>
	<form
		class="flex flex-col gap-4"
		onsubmit={(event) => {
			event.preventDefault();
			save();
		}}
	>
		<!-- Заголовок с кнопкой получения из страницы -->
		<label class="flex flex-col gap-1.5 text-sm">
			<span class="font-semibold text-slate-600 dark:text-slate-300">Заголовок</span>
			<div class="flex gap-2">
				<input
					class="form-input"
					type="text"
					maxlength="120"
					autocomplete="off"
					placeholder="Например: Docker"
					bind:value={title}
				/>
				<button
					type="button"
					class="btn shrink-0"
					onclick={fetchTitle}
					disabled={isFetchingTitle}
					title="Получить заголовок со страницы"
					aria-label="Получить заголовок со страницы"
				>
					<i
						class="fa-solid {isFetchingTitle ? 'fa-spinner fa-spin' : 'fa-wand-magic-sparkles'}"
						aria-hidden="true"
					></i>
				</button>
			</div>
			{#if titleLookupError}
				<p class="text-xs text-red-600 dark:text-red-400" role="alert">{titleLookupError}</p>
			{/if}
		</label>

		<!-- Адрес со схемой -->
		<label class="flex flex-col gap-1.5 text-sm">
			<span class="font-semibold text-slate-600 dark:text-slate-300">URL (ссылка)</span>
			<div class="flex">
				<button
					type="button"
					class="shrink-0 min-w-[4.6rem] rounded-l-xl border border-r-0 border-slate-300/70 bg-white/70 px-3 py-2 font-semibold text-orange-500 dark:border-white/10 dark:bg-slate-900/60"
					onclick={cycleScheme}
					title="Схема ссылки"
					aria-label="Схема ссылки: {schemeLabel}"
				>
					{schemeLabel}
				</button>
				<input
					class="form-input rounded-l-none"
					type="text"
					maxlength="500"
					autocomplete="off"
					placeholder="example.com"
					bind:value={url}
				/>
			</div>
		</label>

		<!-- Иконка -->
		<div class="flex flex-col gap-2 text-sm">
			<span class="font-semibold text-slate-600 dark:text-slate-300">Иконка</span>

			<div class="flex flex-wrap gap-4" role="radiogroup" aria-label="Тип иконки">
				<label class="flex items-center gap-2">
					<input class="h-4 w-4 accent-orange-500" type="radio" name="cardIconMode" checked={!useCustomImage} onchange={() => (useCustomImage = false)} />
					<span>Встроенная иконка</span>
				</label>
				<label class="flex items-center gap-2">
					<input class="h-4 w-4 accent-orange-500" type="radio" name="cardIconMode" checked={useCustomImage} onchange={() => (useCustomImage = true)} />
					<span>Своё изображение</span>
				</label>
			</div>

			{#if useCustomImage}
				<div class="flex items-start gap-4">
					<div
						class="grid h-[4.6rem] w-[4.6rem] shrink-0 place-items-center overflow-hidden rounded-xl border border-slate-300/70 bg-white/70 text-slate-400 dark:border-white/10 dark:bg-slate-900/60"
					>
						{#if previewSource}
							<img src={previewSource} alt="Предпросмотр изображения" class="h-full w-full object-contain" />
						{:else}
							<i class="fa-regular fa-image" aria-hidden="true"></i>
						{/if}
					</div>

					<div class="flex min-w-0 flex-1 flex-col gap-2">
						<input
							class="form-input"
							type="text"
							maxlength="1000"
							autocomplete="off"
							placeholder="https://example.com/icon.png"
							value={imageUrl}
							oninput={handleImageUrlInput}
							aria-label="Ссылка на изображение"
						/>
						<div class="flex flex-wrap gap-2">
							<button type="button" class="btn" onclick={clearImage}>
								<i class="fa-solid fa-eraser" aria-hidden="true"></i>Очистить
							</button>
						</div>
						<p class="text-xs text-slate-500 dark:text-slate-400">
							Ссылка на изображение или вставка из буфера обмена (Ctrl+V).
						</p>
					</div>
				</div>

				{#if imageError}
					<p class="text-xs text-red-600 dark:text-red-400" role="alert">{imageError}</p>
				{/if}
			{:else}
				<IconPicker bind:value={icon} />
			{/if}
		</div>

		<!-- Цвет фона и признак «широкая» -->
		<div class="flex flex-wrap items-end gap-3">
			<div class="min-w-[12rem] flex-1">
				<span class="mb-1.5 block text-sm font-semibold text-slate-600 dark:text-slate-300">Цвет фона</span>
				<ColorPickerField bind:value={color} />
			</div>
			<label class="flex items-center gap-2 rounded-xl border border-slate-300/70 bg-white/70 px-3 py-2 text-sm dark:border-white/10 dark:bg-slate-900/60" title="Карточка займёт две колонки">
				<input class="h-4 w-4 accent-orange-500" type="checkbox" bind:checked={isWide} />
				<span>Широкая</span>
			</label>
		</div>

		<!-- Режим проверки статуса -->
		<div class="flex flex-col gap-2 text-sm">
			<span class="font-semibold text-slate-600 dark:text-slate-300">Проверка статуса</span>
			<div class="grid grid-cols-2 gap-2 max-[440px]:grid-cols-1" role="radiogroup" aria-label="Режим проверки статуса">
				{#each statusOptions as option (option.mode)}
					<label
						class="flex cursor-pointer items-start gap-2 rounded-lg border border-slate-300/70 p-2 transition hover:bg-white/60 dark:border-white/10 dark:hover:bg-slate-800/60"
						title={option.hint}
					>
						<input class="mt-0.5 h-4 w-4 accent-orange-500" type="radio" name="cardStatusMode" value={option.mode} checked={statusMode === option.mode} onchange={() => (statusMode = option.mode)} />
						<span class="flex flex-col">
							<span class="text-xs font-semibold">{option.title}</span>
							<span class="text-[0.7rem] text-slate-500 dark:text-slate-400">{option.hint}</span>
						</span>
					</label>
				{/each}
			</div>
		</div>

		{#if error}
			<p class="text-sm text-red-600 dark:text-red-400" role="alert">{error}</p>
		{/if}

		<div class="flex items-center gap-2">
			{#if card && ondelete}
				<button type="button" class="btn" onclick={ondelete}>
					<i class="fa-solid fa-trash" aria-hidden="true"></i>Удалить
				</button>
			{/if}
			<button type="submit" class="btn btn-primary ml-auto">
				<i class="fa-solid fa-floppy-disk" aria-hidden="true"></i>Сохранить
			</button>
		</div>
	</form>
</BaseDialog>
