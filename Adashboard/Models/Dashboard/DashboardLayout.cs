namespace Adashboard.Models.Dashboard;

public sealed class DashboardLayout
{
    public string Id { get; set; } = "main";

    public List<DashboardCategory> Categories { get; set; } = [];
}
