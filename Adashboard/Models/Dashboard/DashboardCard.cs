namespace Adashboard.Models.Dashboard;

public sealed class DashboardCard
{
    public string Id { get; set; } = string.Empty;

    public string CategoryId { get; set; } = string.Empty;

    public string Title { get; set; } = string.Empty;

    public string Url { get; set; } = string.Empty;

    public string IconClass { get; set; } = string.Empty;

    public bool IsOnline { get; set; }

    public CardPosition Position { get; set; } = new();
}
