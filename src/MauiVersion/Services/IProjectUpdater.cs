using MauiVersion.Models;

namespace MauiVersion.Services;

public interface IProjectUpdater
{
    Task UpdateToStableAsync(MauiProject project, CancellationToken cancellationToken = default);
    Task UpdateToNightlyAsync(MauiProject project, string feedUrl, CancellationToken cancellationToken = default);
    Task UpdateToPrBuildAsync(MauiProject project, string artifactsPath, CancellationToken cancellationToken = default);
}
