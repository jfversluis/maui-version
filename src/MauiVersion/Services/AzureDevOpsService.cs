using System.IO.Compression;
using System.Text.Json;
using MauiVersion.Models;
using Microsoft.Extensions.Logging;

namespace MauiVersion.Services;

public class AzureDevOpsService : IAzureDevOpsService
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<AzureDevOpsService> _logger;
    
    private const string BaseUrl = "https://dev.azure.com";

    public AzureDevOpsService(ILogger<AzureDevOpsService> logger)
    {
        _logger = logger;
        _httpClient = new HttpClient();
        _httpClient.DefaultRequestHeaders.Add("Accept", "application/json");
        _httpClient.DefaultRequestHeaders.Add("User-Agent", "MauiChannelCLI/1.0");
    }

    public async Task<AzureDevOpsBuild?> GetBuildForPrAsync(int prNumber, CancellationToken cancellationToken = default)
    {
        // Get the commit SHA from GitHub PR
        var commitSha = await GetCommitShaFromGitHubPrAsync(prNumber, cancellationToken);
        if (commitSha == null)
        {
            _logger.LogError("Could not get commit SHA from GitHub for PR {PrNumber}", prNumber);
            return null;
        }

        // Get the build info from GitHub Checks API
        return await GetBuildFromGitHubChecksAsync(commitSha, prNumber, cancellationToken);
    }

    private async Task<string?> GetCommitShaFromGitHubPrAsync(int prNumber, CancellationToken cancellationToken)
    {
        try
        {
            var url = $"https://api.github.com/repos/dotnet/maui/pulls/{prNumber}";
            
            var response = await _httpClient.GetAsync(url, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("Failed to fetch PR from GitHub: {StatusCode}", response.StatusCode);
                return null;
            }

            var content = await response.Content.ReadAsStringAsync(cancellationToken);
            var prData = JsonSerializer.Deserialize<JsonElement>(content);
            
            var sha = prData.GetProperty("head").GetProperty("sha").GetString();
            _logger.LogInformation("Found commit SHA from GitHub PR: {Sha}", sha);

            return sha;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to get commit SHA from GitHub");
            return null;
        }
    }

    private async Task<AzureDevOpsBuild?> GetBuildFromGitHubChecksAsync(string commitSha, int prNumber, CancellationToken cancellationToken)
    {
        try
        {
            var allCheckRuns = new List<JsonElement>();
            var url = $"https://api.github.com/repos/dotnet/maui/commits/{commitSha}/check-runs?per_page=100";
            var page = 1;
            
            while (true)
            {
                var pagedUrl = $"{url}&page={page}";
                _logger.LogInformation("Fetching check runs for commit {Sha} (page {Page})", commitSha.Substring(0, 8), page);
                var response = await _httpClient.GetAsync(pagedUrl, cancellationToken);
                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogWarning("Failed to fetch check runs from GitHub: {StatusCode}", response.StatusCode);
                    return null;
                }

                var content = await response.Content.ReadAsStringAsync(cancellationToken);
                var checksData = JsonSerializer.Deserialize<JsonElement>(content);
                
                var checkRuns = checksData.GetProperty("check_runs").EnumerateArray().ToList();
                if (checkRuns.Count == 0)
                {
                    break; // No more pages
                }
                
                allCheckRuns.AddRange(checkRuns);
                
                // Check if there are more pages
                var totalCount = checksData.GetProperty("total_count").GetInt32();
                if (allCheckRuns.Count >= totalCount)
                {
                    break; // We have all checks
                }
                
                page++;
            }
            
            var totalChecks = allCheckRuns.Count;
            _logger.LogInformation("Found {Count} total check runs for commit across all pages", totalChecks);

            if (totalChecks == 0)
            {
                _logger.LogWarning("No check runs found for PR #{PrNumber}. Build may not have been triggered yet.", prNumber);
                return null;
            }

            _logger.LogInformation("Scanning {Count} checks for Azure DevOps builds", totalChecks);

            // Log all check names for debugging
            var checkNames = new List<string>();
            foreach (var check in allCheckRuns)
            {
                var checkName = check.GetProperty("name").GetString();
                if (checkName != null)
                {
                    checkNames.Add(checkName);
                }
            }
            _logger.LogInformation("Available check runs: {CheckNames}", string.Join(", ", checkNames));

            // Find the MAUI-public build check
            var relevantChecks = new List<(string name, string status, string? conclusion)>();
            foreach (var check in allCheckRuns)
            {
                var name = check.GetProperty("name").GetString();
                var status = check.GetProperty("status").GetString();
                string? conclusion = null;
                if (check.TryGetProperty("conclusion", out var conclusionProp) && conclusionProp.ValueKind != JsonValueKind.Null)
                {
                    conclusion = conclusionProp.GetString();
                }
                var detailsUrl = check.GetProperty("details_url").GetString();

                _logger.LogDebug("Check run: name={Name}, status={Status}, conclusion={Conclusion}, detailsUrl={Url}", name, status, conclusion, detailsUrl);

                // Look for MAUI build checks that completed successfully
                // Match patterns:
                // 1. "maui-pr" exactly (main build check without sub-jobs)
                // 2. Any check that starts with "maui-pr (" or "maui-pr-" (sub-jobs from the same build)
                // 3. Exclude uitests builds - they don't have PackageArtifacts
                bool isRelevantBuild = false;
                if (name == "maui-pr")
                {
                    // Main build check
                    isRelevantBuild = true;
                }
                else if (name != null && (name.StartsWith("maui-pr (") || name.StartsWith("maui-pr-")))
                {
                    // Exclude uitests builds
                    if (!name.Contains("uitests", StringComparison.OrdinalIgnoreCase))
                    {
                        isRelevantBuild = true;
                    }
                }

                if (isRelevantBuild)
                {
                    relevantChecks.Add((name ?? "unknown", status ?? "unknown", conclusion));
                    
                    if (status == "completed" && detailsUrl != null && detailsUrl.Contains("dev.azure.com"))
                    {
                        _logger.LogInformation("Found build check: name={Name}, status={Status}, conclusion={Conclusion}", name, status, conclusion);

                        // Extract build info from URL
                        // Patterns:
                        //  - https://dev.azure.com/dnceng-public/public/_build/results?buildId=155100
                        //  - https://dev.azure.com/dnceng-public/public/_build/results?buildId=155161&view=logs&jobId=...
                        var match = System.Text.RegularExpressions.Regex.Match(detailsUrl, @"dev\.azure\.com/([^/]+)/([^/]+)/_build/results\?buildId=(\d+)");
                        if (match.Success)
                        {
                            var org = match.Groups[1].Value;
                            var project = match.Groups[2].Value;
                            var buildIdStr = match.Groups[3].Value;
                            
                            if (int.TryParse(buildIdStr, out int buildId))
                            {
                                _logger.LogInformation("Extracted build info: org={Org}, project={Project}, buildId={BuildId}", org, project, buildId);

                                return new AzureDevOpsBuild
                                {
                                    Id = buildId,
                                    BuildNumber = $"PR-{prNumber}",
                                    Status = status,
                                    Result = conclusion,
                                    SourceBranch = $"pr/{prNumber}",
                                    SourceVersion = commitSha,
                                    Organization = org,
                                    Project = project
                                };
                            }
                        }
                    }
                }
            }

            if (relevantChecks.Any())
            {
                _logger.LogWarning("Found {Count} relevant MAUI checks, but none were completed with Azure DevOps artifacts. Status: {Checks}",
                    relevantChecks.Count,
                    string.Join(", ", relevantChecks.Select(c => $"{c.name} ({c.status}/{c.conclusion ?? "pending"})")));
            }
            else
            {
                _logger.LogWarning("Could not find any MAUI build checks (maui-pr) for this PR");
            }
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching build from GitHub checks");
            return null;
        }
    }



    public async Task<string> DownloadArtifactAsync(int buildId, string organization, string project, CancellationToken cancellationToken = default)
    {
        try
        {
            var artifactsUrl = $"{BaseUrl}/{organization}/{project}/_apis/build/builds/{buildId}/artifacts?api-version=7.1";
            
            _logger.LogInformation("Fetching artifacts for build {BuildId}", buildId);
            
            var response = await _httpClient.GetAsync(artifactsUrl, cancellationToken);
            
            if (!response.IsSuccessStatusCode)
            {
                throw new Exception($"Failed to fetch artifacts: {response.StatusCode}");
            }

            var content = await response.Content.ReadAsStringAsync(cancellationToken);
            var artifactsResponse = JsonSerializer.Deserialize<AzureDevOpsArtifactsResponse>(content);

            var nugetArtifact = artifactsResponse?.Value
                .FirstOrDefault(a => a.Name.Equals("PackageArtifacts", StringComparison.OrdinalIgnoreCase));

            if (nugetArtifact?.Resource?.DownloadUrl == null)
            {
                throw new Exception($"PackageArtifacts artifact not found for build {buildId}");
            }

            _logger.LogInformation("Downloading artifact from {Url}", nugetArtifact.Resource.DownloadUrl);

            var downloadResponse = await _httpClient.GetAsync(nugetArtifact.Resource.DownloadUrl, cancellationToken);
            downloadResponse.EnsureSuccessStatusCode();

            var tempPath = Path.Combine(Path.GetTempPath(), "maui-pr-artifacts", buildId.ToString());
            Directory.CreateDirectory(tempPath);

            var zipPath = Path.Combine(tempPath, "artifacts.zip");
            await using (var fileStream = File.Create(zipPath))
            {
                await downloadResponse.Content.CopyToAsync(fileStream, cancellationToken);
            }

            var extractPath = Path.Combine(tempPath, "extracted");
            ZipFile.ExtractToDirectory(zipPath, extractPath, true);

            _logger.LogInformation("Artifacts extracted to {Path}", extractPath);
            
            return extractPath;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error downloading artifacts for build {BuildId}", buildId);
            throw;
        }
    }
}
