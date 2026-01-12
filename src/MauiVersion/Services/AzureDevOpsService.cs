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
        // Strategy 1: Try to find builds via GitHub check runs on PR commits
        var commits = await GetCommitsFromPrAsync(prNumber, cancellationToken);
        if (commits != null && commits.Count > 0)
        {
            // Try each commit starting from the most recent until we find one with a build that has PackageArtifacts
            foreach (var commitSha in commits)
            {
                _logger.LogInformation("Checking commit {Sha} for builds with PackageArtifacts", commitSha.Substring(0, 8));
                var build = await GetBuildFromCommitChecksAsync(commitSha, prNumber, cancellationToken);
                
                if (build != null)
                {
                    // Verify this build has PackageArtifacts before returning it
                    if (await HasPackageArtifactsAsync(build.Id, build.Organization, build.Project, cancellationToken))
                    {
                        _logger.LogInformation("Found build {BuildId} with PackageArtifacts for commit {Sha}", build.Id, commitSha.Substring(0, 8));
                        return build;
                    }
                    else
                    {
                        _logger.LogInformation("Build {BuildId} found but does not have PackageArtifacts, checking earlier commits", build.Id);
                    }
                }
            }
            
            _logger.LogInformation("No builds with PackageArtifacts found via GitHub checks, trying Azure DevOps API directly");
        }

        // Strategy 2: Fallback to Azure DevOps API to search for PR builds directly
        // This handles merge commits (refs/pull/PR/merge) which don't report checks to GitHub
        _logger.LogInformation("Searching Azure DevOps directly for PR #{PrNumber} builds", prNumber);
        var azDoBuild = await GetBuildFromAzureDevOpsAsync(prNumber, cancellationToken);
        if (azDoBuild != null)
        {
            if (await HasPackageArtifactsAsync(azDoBuild.Id, azDoBuild.Organization, azDoBuild.Project, cancellationToken))
            {
                _logger.LogInformation("Found build {BuildId} with PackageArtifacts via Azure DevOps API", azDoBuild.Id);
                return azDoBuild;
            }
        }

        _logger.LogWarning("No builds with PackageArtifacts found for PR #{PrNumber}", prNumber);
        return null;
    }

    private async Task<List<string>?> GetCommitsFromPrAsync(int prNumber, CancellationToken cancellationToken)
    {
        try
        {
            var url = $"https://api.github.com/repos/dotnet/maui/pulls/{prNumber}/commits?per_page=100";
            
            var response = await _httpClient.GetAsync(url, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("Failed to fetch commits from GitHub: {StatusCode}", response.StatusCode);
                return null;
            }

            var content = await response.Content.ReadAsStringAsync(cancellationToken);
            var commitsData = JsonSerializer.Deserialize<JsonElement>(content);
            
            var commits = new List<string>();
            foreach (var commit in commitsData.EnumerateArray())
            {
                var sha = commit.GetProperty("sha").GetString();
                if (sha != null)
                {
                    commits.Add(sha);
                }
            }

            // Return commits in reverse order (most recent first)
            commits.Reverse();
            _logger.LogInformation("Found {Count} commits in PR #{PrNumber}", commits.Count, prNumber);
            return commits;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to get commits from GitHub");
            return null;
        }
    }

    private async Task<AzureDevOpsBuild?> GetBuildFromAzureDevOpsAsync(int prNumber, CancellationToken cancellationToken)
    {
        try
        {
            // Query Azure DevOps for builds associated with this PR
            // The builds run on merge commits (refs/pull/PR/merge) which don't report to GitHub checks
            var organization = "dnceng-public";
            var project = "public";
            var buildsUrl = $"{BaseUrl}/{organization}/{project}/_apis/build/builds?api-version=7.1&branchName=refs/pull/{prNumber}/merge&$top=10";
            
            _logger.LogInformation("Querying Azure DevOps for PR #{PrNumber} builds", prNumber);
            
            var response = await _httpClient.GetAsync(buildsUrl, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("Failed to fetch builds from Azure DevOps: {StatusCode}", response.StatusCode);
                return null;
            }

            var content = await response.Content.ReadAsStringAsync(cancellationToken);
            var buildsResponse = JsonSerializer.Deserialize<JsonElement>(content);
            
            if (!buildsResponse.TryGetProperty("value", out var buildsArray))
            {
                _logger.LogWarning("No 'value' property in Azure DevOps builds response");
                return null;
            }
            
            var builds = buildsArray.EnumerateArray().ToList();
            _logger.LogInformation("Found {Count} builds in Azure DevOps for PR #{PrNumber}", builds.Count, prNumber);
            
            // Find the most recent completed build with "maui" in the definition name
            foreach (var build in builds)
            {
                var buildId = build.GetProperty("id").GetInt32();
                var buildNumber = build.GetProperty("buildNumber").GetString() ?? "";
                var status = build.GetProperty("status").GetString();
                var result = build.TryGetProperty("result", out var resultProp) ? resultProp.GetString() : null;
                var sourceVersion = build.GetProperty("sourceVersion").GetString() ?? "";
                var sourceBranch = build.GetProperty("sourceBranch").GetString() ?? "";
                
                // Get definition name to filter for MAUI builds
                var definitionName = "";
                if (build.TryGetProperty("definition", out var definition))
                {
                    definitionName = definition.GetProperty("name").GetString() ?? "";
                }
                
                _logger.LogInformation("Evaluating build: {BuildId} ({DefinitionName}), status={Status}, result={Result}", buildId, definitionName, status, result);
                
                // Look for completed builds with definition name exactly "maui-pr" (not uitests or devicetests)
                if (status == "completed" && definitionName == "maui-pr")
                {
                    _logger.LogInformation("Found MAUI PR build: {BuildId}, result={Result}", buildId, result);
                    
                    return new AzureDevOpsBuild
                    {
                        Id = buildId,
                        BuildNumber = buildNumber,
                        Status = status,
                        Result = result,
                        SourceBranch = sourceBranch,
                        SourceVersion = sourceVersion,
                        Organization = organization,
                        Project = project
                    };
                }
            }
            
            _logger.LogWarning("No completed MAUI builds found in Azure DevOps for PR #{PrNumber}", prNumber);
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error querying Azure DevOps for PR builds");
            return null;
        }
    }

    private async Task<bool> HasPackageArtifactsAsync(int buildId, string organization, string project, CancellationToken cancellationToken)
    {
        try
        {
            var artifactsUrl = $"{BaseUrl}/{organization}/{project}/_apis/build/builds/{buildId}/artifacts?api-version=7.1";
            
            var response = await _httpClient.GetAsync(artifactsUrl, cancellationToken);
            
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogDebug("Failed to fetch artifacts for build {BuildId}: {StatusCode}", buildId, response.StatusCode);
                return false;
            }

            var content = await response.Content.ReadAsStringAsync(cancellationToken);
            var artifactsResponse = JsonSerializer.Deserialize<AzureDevOpsArtifactsResponse>(content);

            var hasPackageArtifacts = artifactsResponse?.Value?.Any(a => a.Name.Equals("PackageArtifacts", StringComparison.OrdinalIgnoreCase)) ?? false;
            return hasPackageArtifacts;
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Error checking for PackageArtifacts on build {BuildId}", buildId);
            return false;
        }
    }

    private async Task<AzureDevOpsBuild?> GetBuildFromCommitChecksAsync(string commitSha, int prNumber, CancellationToken cancellationToken)
    {
        try
        {
            // Get check runs for this commit
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
            _logger.LogInformation("Found {Count} total check runs for commit {Sha}", totalChecks, commitSha.Substring(0, 8));

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
                var availableArtifacts = artifactsResponse?.Value?.Select(a => a.Name).ToList() ?? new List<string>();
                _logger.LogWarning("PackageArtifacts not found. Available artifacts: {Artifacts}", string.Join(", ", availableArtifacts.Any() ? availableArtifacts : new List<string> { "none" }));
                throw new Exception($"PackageArtifacts artifact not found for build {buildId}. Available artifacts: {(availableArtifacts.Any() ? string.Join(", ", availableArtifacts) : "none")}. This PR may be from a fork or may not have been configured to generate package artifacts.");
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
