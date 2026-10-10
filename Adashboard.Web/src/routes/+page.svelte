<script lang="ts">
	// Главная страница dashboard.
	// Собирает шапку, сетку категорий и все диалоги, управляет темой и проверкой статусов.
	import { onMount } from 'svelte';
	import CardDialog from '../lib/components/CardDialog.svelte';
	import CategoryDialog from '../lib/components/CategoryDialog.svelte';
	import ConfirmDialog from '../lib/components/ConfirmDialog.svelte';
	import DashboardGrid from '../lib/components/DashboardGrid.svelte';
	import DashboardHeader from '../lib/components/DashboardHeader.svelte';
	import SettingsDialog from '../lib/components/SettingsDialog.svelte';
	import { dashboard } from '../lib/dashboard/store.svelte';
	import { applyTheme, watchSystemTheme } from '../lib/dashboard/theme';
	import { MIN_HEALTH_INTERVAL_SECONDS, type DashboardCard, type DashboardCategory } from '../lib/dashboard/types';

	// Данные для диалогов: null означает, что диалог закрыт.
	let settingsOpen = $state(false);
	let categoryDialog = $state<{ category: DashboardCategory | null } | null>(null);
	let cardDialog = $state<{ card: DashboardCard | null; categoryId: number } | null>(null);
	let confirmDialog = $state<{
		message: string;
		confirmLabel: string;
		onconfirm: () => void;
	} | null>(null);

	onMount(() => {
		// Приводим раскладку к корректному виду на случай правок в сессии.
		dashboard.normalize();
	});

	// Тема: при режиме auto следим за системной настройкой и переключаем оформление.
	$effect(() => {
		const mode = dashboard.layout.themeMode;
		applyTheme(mode);

		if (mode !== 'auto') {
			return;
		}

		return watchSystemTheme(() => applyTheme('auto'));
	});

	// Периодическая проверка статусов с интервалом из настроек.
	$effect(() => {
		const intervalSeconds = Math.max(dashboard.layout.healthCheckIntervalSeconds, MIN_HEALTH_INTERVAL_SECONDS);
		const controller = new AbortController();

		const run = async () => {
			try {
				await dashboard.checkAllCards(controller.signal);
			} catch {
				// Проверка прервана: например, страница закрывается или изменился интервал.
			}
		};

		run();
		const timer = window.setInterval(run, intervalSeconds * 1000);

		return () => {
			controller.abort();
			window.clearInterval(timer);
		};
	});

	function openAddCategory() {
		categoryDialog = { category: null };
	}

	function openEditCategory(category: DashboardCategory) {
		categoryDialog = { category };
	}

	function saveCategory(title: string, width: number) {
		const dialog = categoryDialog;
		if (!dialog) {
			return;
		}

		if (dialog.category) {
			dashboard.updateCategory(dialog.category.id, title, width);
		} else {
			dashboard.addCategory(title, width);
		}

		categoryDialog = null;
	}

	function requestDeleteCategory(category: DashboardCategory) {
		// Пустую категорию удаляем сразу: подтверждать нечего.
		if (category.cards.length === 0) {
			dashboard.deleteCategory(category.id);
			return;
		}

		confirmDialog = {
			message: `Удалить категорию «${category.title}» вместе со всеми ссылками?`,
			confirmLabel: 'Удалить',
			onconfirm: () => {
				dashboard.deleteCategory(category.id);
				confirmDialog = null;
			}
		};
	}

	function openAddCard(category: DashboardCategory) {
		cardDialog = { card: null, categoryId: category.id };
	}

	function openEditCard(card: DashboardCard) {
		cardDialog = { card, categoryId: card.categoryId };
	}

	function saveCard(data: Omit<DashboardCard, 'id' | 'categoryId' | 'order'>) {
		const dialog = cardDialog;
		if (!dialog) {
			return;
		}

		if (dialog.card) {
			dashboard.updateCard(dialog.card.id, data);
			dashboard.checkCardNow(dialog.card);
		} else {
			const created = dashboard.addCard(dialog.categoryId, data);
			if (created) {
				dashboard.checkCardNow(created);
			}
		}

		cardDialog = null;
	}

	function requestDeleteCard(card: DashboardCard) {
		const name = card.title.trim().length > 0 ? card.title : card.url;
		cardDialog = null;

		confirmDialog = {
			message: `Удалить ссылку «${name}»?`,
			confirmLabel: 'Удалить',
			onconfirm: () => {
				dashboard.deleteCard(card.id);
				confirmDialog = null;
			}
		};
	}

	/** Удаление из открытого диалога карточки: закрываем его и показываем подтверждение. */
	function deleteCardFromDialog() {
		const card = cardDialog?.card;
		if (card) {
			requestDeleteCard(card);
		}
	}

	function closeSettings() {
		settingsOpen = false;
	}
</script>

<svelte:head>
	<title>{dashboard.layout.siteTitle.trim().length > 0 ? dashboard.layout.siteTitle : 'Adashboard'}</title>
</svelte:head>

<section class="mx-auto flex w-full max-w-[1600px] flex-col gap-5 p-4 sm:p-6">
	<DashboardHeader
		layout={dashboard.layout}
		editMode={dashboard.editMode}
		onToggleEdit={() => dashboard.toggleEditMode()}
		onAddCategory={openAddCategory}
		onOpenSettings={() => (settingsOpen = true)}
	/>

	<DashboardGrid
		onAddCard={openAddCard}
		onEditCategory={openEditCategory}
		onDeleteCategory={requestDeleteCategory}
		onEditCard={openEditCard}
	/>
</section>

{#if settingsOpen}
	<SettingsDialog
		settings={{
			themeMode: dashboard.layout.themeMode,
			siteTitle: dashboard.layout.siteTitle,
			siteDescription: dashboard.layout.siteDescription,
			showLogo: dashboard.layout.showLogo,
			healthCheckIntervalSeconds: dashboard.layout.healthCheckIntervalSeconds
		}}
		onsave={(settings) => {
			dashboard.updateSettings(settings);
			closeSettings();
		}}
		onclose={closeSettings}
	/>
{/if}

{#if categoryDialog}
	<CategoryDialog
		category={categoryDialog.category
			? { title: categoryDialog.category.title, width: categoryDialog.category.width }
			: null}
		onsave={saveCategory}
		onclose={() => (categoryDialog = null)}
	/>
{/if}

{#if cardDialog}
	<CardDialog
		card={cardDialog.card}
		onsave={saveCard}
		ondelete={cardDialog.card ? deleteCardFromDialog : null}
		onclose={() => (cardDialog = null)}
	/>
{/if}

{#if confirmDialog}
	<ConfirmDialog
		message={confirmDialog.message}
		confirmLabel={confirmDialog.confirmLabel}
		onconfirm={confirmDialog.onconfirm}
		onclose={() => (confirmDialog = null)}
	/>
{/if}
