namespace AndroidPCController.App.Models;

public sealed class StorageCategory
{
    public string Name { get; init; } = "";
    public long SizeBytes { get; init; }
    public string Icon { get; init; } = "";
    public string Color { get; init; } = "#00d2ff";
    public double Percentage { get; init; }
}
