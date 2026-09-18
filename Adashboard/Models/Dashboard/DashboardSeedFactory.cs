namespace Adashboard.Models.Dashboard;

/// <summary>
/// Фабрика стартовой раскладки dashboard.
/// </summary>
public static class DashboardSeedFactory
{
    /// <summary>
    /// Создаёт базовую раскладку для первого запуска приложения.
    /// </summary>
    public static DashboardLayout CreateDefault()
    {
        var layout = new DashboardLayout
        {
            Id = "main",
            Categories =
            [
                new DashboardCategory
                {
                    Id = "cat-network",
                    DashboardLayoutId = "main",
                    Title = "Networking & Management",
                    Position = new CategoryPosition { Order = 0, Width = 6 },
                    Cards =
                    [
                        CreateCard("card-nginx", "cat-network", "Nginx Proxy Manager", "https://example.local/nginx", "fa-solid fa-network-wired", true, 0),
                        CreateCard("card-uptime", "cat-network", "Uptime Kuma", "https://example.local/uptime", "fa-solid fa-heart-pulse", true, 1),
                        CreateCard("card-portainer", "cat-network", "Portainer", "https://example.local/portainer", "fa-brands fa-docker", true, 2),
                        CreateCard("card-ansible", "cat-network", "Ansible", "https://example.local/ansible", "fa-solid fa-terminal", true, 3)
                    ]
                },
                new DashboardCategory
                {
                    Id = "cat-mediaserver",
                    DashboardLayoutId = "main",
                    Title = "Mediaserver",
                    Position = new CategoryPosition { Order = 1, Width = 8 },
                    Cards =
                    [
                        CreateCard("card-jellyfin", "cat-mediaserver", "Jellyfin", "https://example.local/jellyfin", "fa-solid fa-film", true, 0),
                        CreateCard("card-sonarr", "cat-mediaserver", "Sonarr", "https://example.local/sonarr", "fa-solid fa-satellite-dish", true, 1),
                        CreateCard("card-radarr", "cat-mediaserver", "Radarr", "https://example.local/radarr", "fa-solid fa-clapperboard", true, 2),
                        CreateCard("card-bazarr", "cat-mediaserver", "Bazarr", "https://example.local/bazarr", "fa-solid fa-closed-captioning", true, 3),
                        CreateCard("card-lidarr", "cat-mediaserver", "Lidarr", "https://example.local/lidarr", "fa-solid fa-compact-disc", true, 4),
                        CreateCard("card-sab", "cat-mediaserver", "SABnzbd", "https://example.local/sab", "fa-solid fa-download", true, 5)
                    ]
                },
                new DashboardCategory
                {
                    Id = "cat-services",
                    DashboardLayoutId = "main",
                    Title = "Services",
                    Position = new CategoryPosition { Order = 2, Width = 4 },
                    Cards =
                    [
                        CreateCard("card-grafana", "cat-services", "Grafana", "https://example.local/grafana", "fa-solid fa-chart-line", true, 0),
                        CreateCard("card-prom", "cat-services", "Prometheus", "https://example.local/prometheus", "fa-solid fa-chart-area", true, 1),
                        CreateCard("card-admin", "cat-services", "Adminer", "https://example.local/adminer", "fa-solid fa-database", false, 2)
                    ]
                }
            ]
        };

        return layout;
    }

    /// <summary>
    /// Создаёт карточку с заданной позицией и метаданными.
    /// </summary>
    private static DashboardCard CreateCard(string id, string categoryId, string title, string url, string iconClass, bool isOnline, int order) =>
        new()
        {
            Id = id,
            CategoryId = categoryId,
            Title = title,
            Url = url,
            IconClass = iconClass,
            IsOnline = isOnline,
            Position = new CardPosition { Order = order }
        };
}
