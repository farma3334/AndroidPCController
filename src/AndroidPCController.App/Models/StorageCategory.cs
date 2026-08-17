namespace AndroidPCController.App.Models;

public sealed class StorageCategory
{
    public string Name { get; init; } = "";
    public long SizeBytes { get; init; }
    public string Icon { get; init; } = "";
    public string Color { get; init; } = "#8B5CF6";
    public double Percentage { get; init; }
}
