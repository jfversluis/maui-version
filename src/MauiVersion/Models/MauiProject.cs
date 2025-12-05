namespace MauiVersion.Models;

public class MauiProject
{
    public required string ProjectFilePath { get; init; }
    public string? CurrentMauiVersion { get; init; }
    public List<string> MauiPackageReferences { get; init; } = new();
    public List<string> TargetFrameworks { get; init; } = new();
    public string? DotNetVersion { get; init; }
}
