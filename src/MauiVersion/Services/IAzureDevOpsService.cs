using MauiVersion.Models;

namespace MauiVersion.Services;

public interface IAzureDevOpsService
{
    Task<AzureDevOpsBuild?> GetBuildForPrAsync(int prNumber, CancellationToken cancellationToken = default);
    Task<string> DownloadArtifactAsync(int buildId, string organization, string project, CancellationToken cancellationToken = default);
}
