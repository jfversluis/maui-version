using MauiVersion.Models;

namespace MauiVersion.Services;

public interface IProjectUpdater
{
    Task UpdateToStableAsync(MauiProject project, CancellationToken cancellationToken = default);
    Task<bool> UpdateToNightlyAsync(MauiProject project, string feedUrl, bool autoUpdateTfm = false, CancellationToken cancellationToken = default);
    Task<bool> UpdateToPrBuildAsync(MauiProject project, string artifactsPath, bool autoUpdateTfm = false, CancellationToken cancellationToken = default);
}
