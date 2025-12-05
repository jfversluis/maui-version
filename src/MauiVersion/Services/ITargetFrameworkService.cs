namespace MauiVersion.Services;

public interface ITargetFrameworkService
{
    string? ExtractDotNetVersionFromMauiVersion(string mauiVersion);
    bool IsVersionCompatible(string? projectDotNetVersion, string? packageDotNetVersion);
    Task UpdateTargetFrameworksAsync(string projectPath, string newDotNetVersion, CancellationToken cancellationToken = default);
}
