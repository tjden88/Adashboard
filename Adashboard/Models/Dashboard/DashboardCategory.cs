namespace Adashboard.Models.Dashboard;

public sealed class DashboardCategory
{
    public string Id { get; set; } = string.Empty;

    public string DashboardLayoutId { get; set; } = string.Empty;

    public string Title { get; set; } = string.Empty;

    public CategoryPosition Position { get; set; } = new();

    public List<DashboardCard> Cards { get; set; } = [];
}
