using System.Diagnostics;
using System.Text;
using System.Xml.Linq;
using MauiVersion.Models;
using Microsoft.Extensions.Logging;
using Spectre.Console;

namespace MauiVersion.Services;

public class ProjectUpdater : IProjectUpdater
{
    private readonly ILogger<ProjectUpdater> _logger;
    private readonly ITargetFrameworkService _targetFrameworkService;
    private const string MauiControlsPackage = "Microsoft.Maui.Controls";

    public ProjectUpdater(ILogger<ProjectUpdater> logger, ITargetFrameworkService targetFrameworkService)
    {
        _logger = logger;
        _targetFrameworkService = targetFrameworkService;
    }

    public async Task UpdateToStableAsync(MauiProject project, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Updating to stable release from NuGet.org");

        var projectDir = Path.GetDirectoryName(project.ProjectFilePath);
        if (projectDir == null)
        {
            throw new InvalidOperationException("Could not determine project directory");
        }

        await RemoveNuGetConfigAsync(projectDir, cancellationToken);

        string? latestVersion = null;
        int? targetTfmVersion = null;
        
        await AnsiConsole.Status()
            .StartAsync("Fetching latest stable MAUI version...", async ctx =>
            {
                ctx.Spinner(Spinner.Known.Dots);
                
                latestVersion = await GetLatestStableVersionForTargetFrameworkAsync(project.DotNetVersion, cancellationToken);
                
                if (string.IsNullOrEmpty(latestVersion))
                {
                    // No stable version for current TFM, get the latest overall stable
                    latestVersion = await GetLatestStableVersionAsync(cancellationToken);
                    
                    if (string.IsNullOrEmpty(latestVersion))
                    {
                        throw new Exception("Could not find any stable version of Microsoft.Maui.Controls on NuGet.org");
                    }
                    
                    // Try to determine what TFM this version needs
                    var versionParts = latestVersion.Split('.');
                    if (versionParts.Length >= 1 && int.TryParse(versionParts[0], out var majorVersion))
                    {
                        targetTfmVersion = majorVersion + 1; // MAUI 9.x needs .NET 9, etc.
                    }
                }
                else
                {
                    _logger.LogInformation("Latest stable version for .NET {TFM}: {Version}", project.DotNetVersion, latestVersion);
                }
            });

        // If we need to downgrade TFM, ask the user
        if (targetTfmVersion.HasValue && int.TryParse(project.DotNetVersion, out var currentDotNetVersion) && targetTfmVersion.Value < currentDotNetVersion)
        {
            AnsiConsole.MarkupLine($"[yellow]⚠[/] Your project targets .NET {project.DotNetVersion}, but the latest stable MAUI is {latestVersion}");
            AnsiConsole.MarkupLine($"[yellow]⚠[/] This version requires .NET {targetTfmVersion.Value}");
            AnsiConsole.WriteLine();
            
            var shouldDowngrade = AnsiConsole.Confirm($"Do you want to update your project to .NET {targetTfmVersion.Value}?", false);
            
            if (shouldDowngrade)
            {
                await _targetFrameworkService.UpdateTargetFrameworksAsync(project.ProjectFilePath, targetTfmVersion.Value.ToString(), cancellationToken);
                AnsiConsole.MarkupLine($"[green]✓[/] Updated TargetFrameworks to .NET {targetTfmVersion.Value}");
                AnsiConsole.MarkupLine("[yellow]![/] Note: You may need to update other package dependencies to match .NET {0}", targetTfmVersion.Value);
            }
            else
            {
                AnsiConsole.MarkupLine("[yellow]⚠[/] Continuing without TFM update. Package restore may fail.");
            }
        }

        await AnsiConsole.Status()
            .StartAsync($"Updating to version {latestVersion}...", async ctx =>
            {
                ctx.Spinner(Spinner.Known.Dots);
                
                ctx.Status($"Updating package version...");
                await UpdatePackageVersionInProjectAsync(project.ProjectFilePath, MauiControlsPackage, latestVersion!, cancellationToken);
                
                ctx.Status("Restoring packages...");
                var projectFileName = Path.GetFileName(project.ProjectFilePath);
                var result = await RunDotNetCommandAsync(
                    $"restore \"{projectFileName}\" --disable-parallel",
                    projectDir,
                    cancellationToken);

                if (result.exitCode != 0)
                {
                    AnsiConsole.MarkupLine("[red]✗[/] Package restore failed.");
                    AnsiConsole.MarkupLine($"[yellow]⚠[/] Installed version: {latestVersion}");
                    
                    if (!string.IsNullOrEmpty(result.error))
                    {
                        AnsiConsole.MarkupLine($"[red]Error output:[/]");
                        AnsiConsole.WriteLine(result.error);
                    }
                    if (!string.IsNullOrEmpty(result.output))
                    {
                        AnsiConsole.MarkupLine($"[yellow]Standard output:[/]");
                        AnsiConsole.WriteLine(result.output);
                    }
                    
                    AnsiConsole.MarkupLine("[yellow]⚠[/] Try running 'dotnet restore' manually to see the full error.");
                    _logger.LogError("Restore failed. Error: {Error}, Output: {Output}", result.error, result.output);
                    throw new Exception($"Failed to restore packages: {result.error}");
                }
            });

        AnsiConsole.MarkupLine("[green]✓[/] Updated to stable release");
    }

    public async Task<bool> UpdateToNightlyAsync(MauiProject project, string feedUrl, bool autoUpdateTfm = false, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Updating to nightly build from {FeedUrl}", feedUrl);

        var projectDir = Path.GetDirectoryName(project.ProjectFilePath);
        if (projectDir == null)
        {
            throw new InvalidOperationException("Could not determine project directory");
        }

        await CreateNuGetConfigAsync(projectDir, feedUrl, "nightly", cancellationToken);

        var success = await AnsiConsole.Status()
            .StartAsync("Fetching latest nightly MAUI version...", async ctx =>
            {
                ctx.Spinner(Spinner.Known.Dots);
                
                var latestVersion = await GetLatestNightlyVersionAsync(feedUrl, cancellationToken);
                _logger.LogInformation("Latest nightly version: {Version}", latestVersion);
                
                var packageDotNetVersion = _targetFrameworkService.ExtractDotNetVersionFromMauiVersion(latestVersion);
                
                // Check if there's a TFM mismatch
                if (!_targetFrameworkService.IsVersionCompatible(project.DotNetVersion, packageDotNetVersion))
                {
                    if (autoUpdateTfm)
                    {
                        // -f flag was specified, auto-update without prompting
                        ctx.Status($"Updating TargetFrameworks to .NET {packageDotNetVersion}...");
                        await _targetFrameworkService.UpdateTargetFrameworksAsync(project.ProjectFilePath, packageDotNetVersion!, cancellationToken);
                        AnsiConsole.MarkupLine($"[blue]ℹ[/] Updated TargetFrameworks to .NET {packageDotNetVersion}");
                        AnsiConsole.MarkupLine($"[yellow]⚠[/] [dim]You may need to update other package dependencies to match .NET {packageDotNetVersion}[/]");
                    }
                    else
                    {
                        // -f flag was not specified, prompt the user
                        ctx.Status("Target framework mismatch detected...");
                        
                        if (!await PromptForTargetFrameworkUpdateAsync(project.DotNetVersion, packageDotNetVersion, cancellationToken))
                        {
                            AnsiConsole.MarkupLine("[yellow]✖[/] Operation cancelled by user");
                            return false;
                        }
                        
                        ctx.Status($"Updating TargetFrameworks to .NET {packageDotNetVersion}...");
                        await _targetFrameworkService.UpdateTargetFrameworksAsync(project.ProjectFilePath, packageDotNetVersion!, cancellationToken);
                        AnsiConsole.MarkupLine($"[blue]ℹ[/] Updated TargetFrameworks to .NET {packageDotNetVersion}");
                        AnsiConsole.MarkupLine($"[yellow]⚠[/] [dim]You may need to update other package dependencies to match .NET {packageDotNetVersion}[/]");
                    }
                }
                
                ctx.Status($"Updating to version {latestVersion}...");
                await UpdatePackageVersionInProjectAsync(project.ProjectFilePath, MauiControlsPackage, latestVersion, cancellationToken);
                
                ctx.Status("Restoring packages...");
                var projectFileName = Path.GetFileName(project.ProjectFilePath);
                var result = await RunDotNetCommandAsync(
                    $"restore \"{projectFileName}\"",
                    projectDir,
                    cancellationToken);

                if (result.exitCode != 0)
                {
                    throw new Exception($"Failed to restore packages: {result.error}");
                }
                
                return true;
            });

        if (success)
        {
            AnsiConsole.MarkupLine("[green]✓[/] Updated to nightly build");
        }
        
        return success;
    }

    public async Task<bool> UpdateToPrBuildAsync(MauiProject project, string artifactsPath, bool autoUpdateTfm = false, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Updating to PR build from {ArtifactsPath}", artifactsPath);

        var projectDir = Path.GetDirectoryName(project.ProjectFilePath);
        if (projectDir == null)
        {
            throw new InvalidOperationException("Could not determine project directory");
        }

        var nupkgFiles = Directory.GetFiles(artifactsPath, "*.nupkg", SearchOption.AllDirectories);
        
        if (nupkgFiles.Length == 0)
        {
            throw new Exception($"No NuGet packages found in {artifactsPath}");
        }

        var mauiControlsPackage = nupkgFiles
            .FirstOrDefault(f => Path.GetFileName(f).StartsWith("Microsoft.Maui.Controls", StringComparison.OrdinalIgnoreCase));

        if (mauiControlsPackage == null)
        {
            throw new Exception("Microsoft.Maui.Controls package not found in artifacts");
        }

        var version = ExtractVersionFromNupkg(mauiControlsPackage);
        _logger.LogInformation("Found MAUI version {Version}", version);

        var packageDotNetVersion = _targetFrameworkService.ExtractDotNetVersionFromMauiVersion(version);

        // Check if there's a TFM mismatch
        if (!_targetFrameworkService.IsVersionCompatible(project.DotNetVersion, packageDotNetVersion))
        {
            // Ensure we have a valid target version
            var targetVersion = !string.IsNullOrEmpty(packageDotNetVersion) ? packageDotNetVersion : "10.0";
            
            if (autoUpdateTfm)
            {
                // -f flag was specified, auto-update without prompting
                packageDotNetVersion = targetVersion;
                
                await AnsiConsole.Status()
                    .StartAsync($"Updating TargetFrameworks to .NET {packageDotNetVersion}...", async ctx =>
                    {
                        ctx.Spinner(Spinner.Known.Dots);
                        await _targetFrameworkService.UpdateTargetFrameworksAsync(project.ProjectFilePath, packageDotNetVersion!, cancellationToken);
                    });
                
                AnsiConsole.MarkupLine($"[blue]ℹ[/] Updated TargetFrameworks to .NET {packageDotNetVersion}");
                AnsiConsole.MarkupLine($"[yellow]⚠[/] [dim]You may need to update other package dependencies to match .NET {packageDotNetVersion}[/]");
            }
            else
            {
                // -f flag was not specified, prompt the user
                if (!await PromptForTargetFrameworkUpdateAsync(project.DotNetVersion, targetVersion, cancellationToken))
                {
                    AnsiConsole.MarkupLine("[yellow]✖[/] Operation cancelled by user");
                    return false;
                }
                
                packageDotNetVersion = targetVersion;
                
                await AnsiConsole.Status()
                    .StartAsync($"Updating TargetFrameworks to .NET {packageDotNetVersion}...", async ctx =>
                    {
                        ctx.Spinner(Spinner.Known.Dots);
                        await _targetFrameworkService.UpdateTargetFrameworksAsync(project.ProjectFilePath, packageDotNetVersion!, cancellationToken);
                    });
                
                AnsiConsole.MarkupLine($"[blue]ℹ[/] Updated TargetFrameworks to .NET {packageDotNetVersion}");
                AnsiConsole.MarkupLine($"[yellow]⚠[/] [dim]You may need to update other package dependencies to match .NET {packageDotNetVersion}[/]");
            }
        }

        var nugetSourcePath = Path.GetDirectoryName(mauiControlsPackage);
        if (nugetSourcePath == null)
        {
            throw new Exception("Could not determine NuGet source path");
        }

        await CreateNuGetConfigAsync(projectDir, nugetSourcePath, "pr-build", cancellationToken);

        await AnsiConsole.Status()
            .StartAsync($"Updating to PR build version {version}...", async ctx =>
            {
                ctx.Spinner(Spinner.Known.Dots);
                
                await UpdatePackageVersionInProjectAsync(project.ProjectFilePath, MauiControlsPackage, version, cancellationToken);
                
                ctx.Status("Restoring packages...");
                var projectFileName = Path.GetFileName(project.ProjectFilePath);
                var result = await RunDotNetCommandAsync(
                    $"restore \"{projectFileName}\"",
                    projectDir,
                    cancellationToken);

                if (result.exitCode != 0)
                {
                    throw new Exception($"Failed to restore packages: {result.error}");
                }
            });

        AnsiConsole.MarkupLine($"[green]✓[/] Updated to PR build version {version}");
        return true;
    }

    private async Task CreateNuGetConfigAsync(string projectDir, string sourceUrl, string sourceName, CancellationToken cancellationToken)
    {
        var nugetConfigPath = Path.Combine(projectDir, "NuGet.config");
        
        var config = new XDocument(
            new XDeclaration("1.0", "utf-8", null),
            new XElement("configuration",
                new XElement("packageSources",
                    new XElement("clear"),
                    new XElement("add",
                        new XAttribute("key", sourceName),
                        new XAttribute("value", sourceUrl)),
                    new XElement("add",
                        new XAttribute("key", "nuget.org"),
                        new XAttribute("value", "https://api.nuget.org/v3/index.json"))
                )
            )
        );

        await Task.Run(() => config.Save(nugetConfigPath), cancellationToken);
        _logger.LogInformation("Created NuGet.config at {Path}", nugetConfigPath);
        AnsiConsole.MarkupLine($"[blue]ℹ[/] Created NuGet.config with source: {sourceName}");
    }

    private async Task RemoveNuGetConfigAsync(string projectDir, CancellationToken cancellationToken)
    {
        var nugetConfigPath = Path.Combine(projectDir, "NuGet.config");
        
        if (File.Exists(nugetConfigPath))
        {
            await Task.Run(() => File.Delete(nugetConfigPath), cancellationToken);
            _logger.LogInformation("Removed NuGet.config from {Path}", projectDir);
            AnsiConsole.MarkupLine("[blue]ℹ[/] Removed NuGet.config");
        }
    }

    private string ExtractVersionFromNupkg(string nupkgPath)
    {
        var fileName = Path.GetFileNameWithoutExtension(nupkgPath);
        var parts = fileName.Split('.');
        
        for (int i = 0; i < parts.Length; i++)
        {
            if (int.TryParse(parts[i], out _))
            {
                return string.Join(".", parts[i..]);
            }
        }

        throw new Exception($"Could not extract version from {nupkgPath}");
    }

    private async Task UpdatePackageVersionInProjectAsync(string projectPath, string packageName, string version, CancellationToken cancellationToken)
    {
        var doc = await Task.Run(() => XDocument.Load(projectPath), cancellationToken);
        
        var packageReference = doc.Descendants("PackageReference")
            .FirstOrDefault(e => e.Attribute("Include")?.Value == packageName);

        if (packageReference != null)
        {
            var versionAttr = packageReference.Attribute("Version");
            if (versionAttr != null)
            {
                versionAttr.Value = version;
            }
            else
            {
                packageReference.Add(new XAttribute("Version", version));
            }
        }
        else
        {
            var itemGroup = doc.Descendants("ItemGroup").FirstOrDefault()
                ?? doc.Root?.Elements("ItemGroup").FirstOrDefault();
            
            if (itemGroup == null)
            {
                itemGroup = new XElement("ItemGroup");
                doc.Root?.Add(itemGroup);
            }

            itemGroup.Add(new XElement("PackageReference",
                new XAttribute("Include", packageName),
                new XAttribute("Version", version)));
        }

        await Task.Run(() => doc.Save(projectPath), cancellationToken);
        _logger.LogInformation("Updated {Package} to version {Version} in {Project}", packageName, version, projectPath);
    }

    private async Task<bool> PromptForTargetFrameworkUpdateAsync(string? currentVersion, string? requiredVersion, CancellationToken cancellationToken)
    {
        AnsiConsole.WriteLine();
        AnsiConsole.MarkupLine($"[yellow]⚠[/] Target framework mismatch detected:");
        AnsiConsole.MarkupLine($"  Current project: [cyan].NET {currentVersion ?? "unknown"}[/]");
        AnsiConsole.MarkupLine($"  Required for package: [cyan].NET {requiredVersion ?? "unknown"}[/]");
        AnsiConsole.WriteLine();

        var choice = AnsiConsole.Prompt(
            new SelectionPrompt<string>()
                .Title("How would you like to proceed?")
                .AddChoices(new[]
                {
                    "Update TargetFrameworks to match package",
                    "Cancel operation"
                }));

        if (choice.StartsWith("Update"))
        {
            AnsiConsole.MarkupLine($"[yellow]⚠[/] [dim]Note: You may need to manually update other package dependencies to versions compatible with .NET {requiredVersion}[/]");
            AnsiConsole.WriteLine();
        }

        return choice.StartsWith("Update");
    }

    private async Task<string> GetLatestStableVersionForTargetFrameworkAsync(string? dotNetVersion, CancellationToken cancellationToken)
    {
        using var httpClient = new HttpClient();
        var url = $"https://api.nuget.org/v3-flatcontainer/{MauiControlsPackage.ToLowerInvariant()}/index.json";
        
        try
        {
            var response = await httpClient.GetStringAsync(url, cancellationToken);
            var doc = System.Text.Json.JsonDocument.Parse(response);
            
            var versions = doc.RootElement.GetProperty("versions").EnumerateArray()
                .Select(v => v.GetString())
                .Where(v => v != null && !v.Contains("-"))
                .Select(v => v!)
                .ToList();

            if (string.IsNullOrEmpty(dotNetVersion))
            {
                return versions.LastOrDefault() ?? throw new Exception("No stable version found");
            }

            var compatibleVersions = versions
                .Where(v =>
                {
                    var versionDotNet = _targetFrameworkService.ExtractDotNetVersionFromMauiVersion(v);
                    return versionDotNet == dotNetVersion;
                })
                .ToList();

            if (compatibleVersions.Any())
            {
                return compatibleVersions.Last();
            }

            return versions.LastOrDefault() ?? throw new Exception("No stable version found");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to fetch latest stable version");
            throw new Exception("Failed to fetch latest stable version from NuGet.org", ex);
        }
    }

    private async Task<string> GetLatestStableVersionAsync(CancellationToken cancellationToken)
    {
        // Just call the other method with null dotNetVersion to get the overall latest
        return await GetLatestStableVersionForTargetFrameworkAsync(null, cancellationToken);
    }

    private async Task<string> GetLatestNightlyVersionAsync(string feedUrl, CancellationToken cancellationToken)
    {
        using var httpClient = new HttpClient();
        
        try
        {
            var serviceIndexResponse = await httpClient.GetStringAsync(feedUrl, cancellationToken);
            var serviceIndex = System.Text.Json.JsonDocument.Parse(serviceIndexResponse);
            
            var packageBaseAddress = serviceIndex.RootElement.GetProperty("resources").EnumerateArray()
                .FirstOrDefault(r => r.GetProperty("@type").GetString() == "PackageBaseAddress/3.0.0")
                .GetProperty("@id").GetString();

            if (packageBaseAddress == null)
            {
                throw new Exception("Could not find PackageBaseAddress in feed");
            }

            var versionsUrl = $"{packageBaseAddress}{MauiControlsPackage.ToLowerInvariant()}/index.json";
            var versionsResponse = await httpClient.GetStringAsync(versionsUrl, cancellationToken);
            var versionsDoc = System.Text.Json.JsonDocument.Parse(versionsResponse);
            
            var versions = versionsDoc.RootElement.GetProperty("versions").EnumerateArray()
                .Select(v => v.GetString())
                .Where(v => v != null)
                .Select(v => v!)
                .ToList();

            return versions.LastOrDefault() ?? throw new Exception("No nightly version found");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to fetch latest nightly version");
            throw new Exception("Failed to fetch latest nightly version", ex);
        }
    }

    private async Task<(int exitCode, string output, string error)> RunDotNetCommandAsync(
        string arguments,
        string workingDirectory,
        CancellationToken cancellationToken)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = "dotnet",
            Arguments = arguments,
            WorkingDirectory = workingDirectory,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        using var process = new Process { StartInfo = startInfo };
        
        var outputBuilder = new StringBuilder();
        var errorBuilder = new StringBuilder();

        process.OutputDataReceived += (sender, e) =>
        {
            if (e.Data != null)
            {
                outputBuilder.AppendLine(e.Data);
            }
        };

        process.ErrorDataReceived += (sender, e) =>
        {
            if (e.Data != null)
            {
                errorBuilder.AppendLine(e.Data);
            }
        };

        process.Start();
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();

        await process.WaitForExitAsync(cancellationToken);

        return (process.ExitCode, outputBuilder.ToString(), errorBuilder.ToString());
    }
}
