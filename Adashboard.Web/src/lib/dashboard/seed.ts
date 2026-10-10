import { DEFAULT_HEALTH_INTERVAL_SECONDS, type CardStatusMode, type DashboardCard, type DashboardLayout } from './types';

// Стартовая раскладка dashboard.
// Данные повторяют сид из DebugDbInitializer старого Blazor-проекта.

/** Создаёт карточку с заданными полями (упрощает описание сида ниже). */
function createCard(
	id: number,
	categoryId: number,
	title: string,
	url: string,
	iconClass: string,
	backgroundColor: string,
	isWide: boolean,
	order: number,
	statusMode: CardStatusMode = 'none'
): DashboardCard {
	return {
		id,
		categoryId,
		title,
		url,
		urlScheme: '',
		iconClass,
		imagePath: '',
		backgroundColor,
		isWide,
		statusMode,
		order
	};
}

/** Возвращает стартовую раскладку. Каждый вызов создаёт новые объекты. */
export function createSeedLayout(): DashboardLayout {
	return {
		id: 1,
		themeMode: 'auto',
		siteTitle: 'Adashboard',
		siteDescription: 'Личная стартовая страница со ссылками и сервисами',
		showLogo: true,
		healthCheckIntervalSeconds: DEFAULT_HEALTH_INTERVAL_SECONDS,
		categories: [
			{
				id: 101,
				title: 'Networking & Management',
				width: 6,
				order: 0,
				cards: [
					createCard(1001, 101, 'Nginx Proxy Manager', 'https://example.local/nginx', 'fa-solid fa-network-wired', '#f43f5e', true, 0),
					createCard(1002, 101, 'Uptime Kuma', 'https://example.local/uptime', 'fa-solid fa-heart-pulse', '#22c55e', false, 1),
					createCard(1003, 101, 'Portainer', 'https://example.local/portainer', 'fa-brands fa-docker', '#0ea5e9', true, 2),
					createCard(1004, 101, 'Ansible', 'https://example.local/ansible', 'fa-solid fa-terminal', '#8b5cf6', false, 3)
				]
			},
			{
				id: 102,
				title: 'Mediaserver',
				width: 8,
				order: 1,
				cards: [
					createCard(2001, 102, 'Jellyfin', 'https://example.local/jellyfin', 'fa-solid fa-film', '#a78bfa', false, 0),
					createCard(2002, 102, 'Sonarr', 'https://example.local/sonarr', 'fa-solid fa-satellite-dish', '#fb7185', true, 1),
					createCard(2003, 102, 'Radarr', 'https://example.local/radarr', 'fa-solid fa-clapperboard', '#facc15', true, 2),
					createCard(2004, 102, 'Bazarr', 'https://example.local/bazarr', 'fa-solid fa-closed-captioning', '#60a5fa', false, 3),
					createCard(2005, 102, 'Lidarr', 'https://example.local/lidarr', 'fa-solid fa-compact-disc', '#34d399', true, 4),
					createCard(2006, 102, 'SABnzbd', 'https://example.local/sab', 'fa-solid fa-download', '#22d3ee', true, 5)
				]
			},
			{
				id: 103,
				title: 'Services',
				width: 4,
				order: 2,
				cards: [
					createCard(3001, 103, 'Grafana', 'https://example.local/grafana', 'fa-solid fa-chart-line', '#f97316', true, 0),
					createCard(3002, 103, 'Prometheus', 'https://example.local/prometheus', 'fa-solid fa-chart-area', '#22d3ee', false, 1),
					createCard(3003, 103, 'Adminer', 'https://example.local/adminer', 'fa-solid fa-database', '#f43f5e', true, 2)
				]
			}
		]
	};
}
