using System.CommandLine;
using System.CommandLine.Parsing;
using MauiVersion.Models;
using MauiVersion.Services;
using Microsoft.Extensions.Logging;
using Spectre.Console;

namespace MauiVersion.Commands;

public class ApplyCommand : BaseCommand
{
    private readonly IProjectLocator _projectLocator;
    private readonly IProjectUpdater _projectUpdater;
    private readonly IAzureDevOpsService _azureDevOpsService;
    private readonly ILogger<ApplyCommand> _logger;
    
    private readonly Option<string?> _projectOption;
    private readonly Option<string?> _channelOption;
    private readonly Option<int?> _prOption;

    public ApplyCommand(
        IProjectLocator projectLocator,
        IProjectUpdater projectUpdater,
        IAzureDevOpsService azureDevOpsService,
        ILogger<ApplyCommand> logger)
        : base("apply", "Apply a MAUI release channel to your project")
    {
        _projectLocator = projectLocator;
        _projectUpdater = projectUpdater;
        _azureDevOpsService = azureDevOpsService;
        _logger = logger;

        _projectOption = new Option<string?>(
            aliases: ["--project", "-p"],
            description: "Path to the .csproj file or directory containing the project");
        AddOption(_projectOption);

        _channelOption = new Option<string?>(
            aliases: ["--channel", "-c"],
            description: "Channel to apply: stable, nightly, or pr");
        AddOption(_channelOption);

        _prOption = new Option<int?>(
            aliases: ["--apply-pr", "-pr"],
            description: "Apply artifacts from a specific PR number");
        AddOption(_prOption);
    }

    protected override async Task<int> ExecuteAsync(ParseResult parseResult, CancellationToken cancellationToken)
    {
        try
        {
            var projectPath = parseResult.GetValueForOption(_projectOption);
            var channelName = parseResult.GetValueForOption(_channelOption);
            var prNumber = parseResult.GetValueForOption(_prOption);

            AnsiConsole.Write(new FigletText("MAUI Channel").Color(Color.Purple));

            var project = await _projectLocator.FindMauiProjectAsync(projectPath, cancellationToken);
            if (project == null)
            {
                AnsiConsole.MarkupLine("[red]✗[/] No MAUI project found in the current directory");
                return 1;
            }

            AnsiConsole.MarkupLine($"[blue]ℹ[/] Found MAUI project: [cyan]{Path.GetFileName(project.ProjectFilePath)}[/]");
            if (!string.IsNullOrEmpty(project.CurrentMauiVersion))
            {
                AnsiConsole.MarkupLine($"[blue]ℹ[/] Current version: [cyan]{project.CurrentMauiVersion}[/]");
            }
            if (!string.IsNullOrEmpty(project.DotNetVersion))
            {
                AnsiConsole.MarkupLine($"[blue]ℹ[/] Target .NET version: [cyan].NET {project.DotNetVersion}[/]");
            }

            ReleaseChannel channel;

            if (prNumber.HasValue)
            {
                channel = new ReleaseChannel
                {
                    Name = $"PR #{prNumber}",
                    Type = ReleaseChannelType.PR,
                    Description = $"Pull Request #{prNumber}",
                    PrNumber = prNumber.Value
                };
            }
            else if (!string.IsNullOrEmpty(channelName))
            {
                channel = channelName.ToLowerInvariant() switch
                {
                    "stable" => new ReleaseChannel
                    {
                        Name = "Stable",
                        Type = ReleaseChannelType.Stable,
                        Description = "Stable release from NuGet.org"
                    },
                    "nightly" => await SelectNightlyChannelAsync(cancellationToken),
                    "pr" => await SelectPrChannelAsync(cancellationToken),
                    _ => throw new Exception($"Unknown channel: {channelName}")
                };
            }
            else
            {
                channel = await PromptForChannelAsync(cancellationToken);
            }

            await ApplyChannelAsync(project, channel, cancellationToken);

            return 0;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error applying release channel");
            AnsiConsole.MarkupLine($"[red]✗[/] Error: {Markup.Escape(ex.Message)}");
            return 1;
        }
    }

    private async Task<ReleaseChannel> PromptForChannelAsync(CancellationToken cancellationToken)
    {
        var channels = new[]
        {
            "Stable (NuGet.org)",
            "Nightly (latest builds)",
            "PR Build (specific pull request)"
        };

        var selection = AnsiConsole.Prompt(
            new SelectionPrompt<string>()
                .Title("Select a [green]release channel[/]:")
                .AddChoices(channels));

        if (selection.StartsWith("Stable"))
        {
            return new ReleaseChannel
            {
                Name = "Stable",
                Type = ReleaseChannelType.Stable,
                Description = "Stable release from NuGet.org"
            };
        }
        else if (selection.StartsWith("Nightly"))
        {
            return await SelectNightlyChannelAsync(cancellationToken);
        }
        else
        {
            return await SelectPrChannelAsync(cancellationToken);
        }
    }

    private async Task<ReleaseChannel> SelectNightlyChannelAsync(CancellationToken cancellationToken)
    {
        var nightlyChannels = new List<NightlyChannelOption>
        {
            new NightlyChannelOption("dotnet9", "https://pkgs.dev.azure.com/dnceng/public/_packaging/dotnet9/nuget/v3/index.json", ".NET 9 nightly builds"),
            new NightlyChannelOption("dotnet10", "https://pkgs.dev.azure.com/dnceng/public/_packaging/dotnet10/nuget/v3/index.json", ".NET 10 nightly builds")
        };

        var selection = AnsiConsole.Prompt(
            new SelectionPrompt<NightlyChannelOption>()
                .Title("Select a [green]nightly channel[/]:")
                .AddChoices(nightlyChannels)
                .UseConverter(c => $"{c.Name} - {c.Description}"));

        return new ReleaseChannel
        {
            Name = $"Nightly ({selection.Name})",
            Type = ReleaseChannelType.Nightly,
            Description = selection.Description,
            FeedUrl = selection.Url
        };
    }

    private class NightlyChannelOption
    {
        public string Name { get; }
        public string Url { get; }
        public string Description { get; }

        public NightlyChannelOption(string name, string url, string description)
        {
            Name = name;
            Url = url;
            Description = description;
        }
    }

    private async Task<ReleaseChannel> SelectPrChannelAsync(CancellationToken cancellationToken)
    {
        var prNumber = AnsiConsole.Ask<int>("Enter [green]PR number[/]:");

        return new ReleaseChannel
        {
            Name = $"PR #{prNumber}",
            Type = ReleaseChannelType.PR,
            Description = $"Pull Request #{prNumber}",
            PrNumber = prNumber
        };
    }

    private async Task ApplyChannelAsync(MauiProject project, ReleaseChannel channel, CancellationToken cancellationToken)
    {
        AnsiConsole.MarkupLine($"[yellow]→[/] Applying channel: [cyan]{channel.Name}[/]");

        switch (channel.Type)
        {
            case ReleaseChannelType.Stable:
                await _projectUpdater.UpdateToStableAsync(project, cancellationToken);
                break;

            case ReleaseChannelType.Nightly:
                if (channel.FeedUrl == null)
                {
                    throw new Exception("Feed URL is required for nightly channel");
                }
                await _projectUpdater.UpdateToNightlyAsync(project, channel.FeedUrl, cancellationToken);
                break;

            case ReleaseChannelType.PR:
                if (!channel.PrNumber.HasValue)
                {
                    throw new Exception("PR number is required for PR channel");
                }
                await ApplyPrBuildAsync(project, channel.PrNumber.Value, cancellationToken);
                break;
        }
    }

    private async Task ApplyPrBuildAsync(MauiProject project, int prNumber, CancellationToken cancellationToken)
    {
        // Get build info first (without spinner so we can see debug output)
        AnsiConsole.MarkupLine($"[yellow]Looking for build artifacts for PR #{prNumber}...[/]");
        var build = await _azureDevOpsService.GetBuildForPrAsync(prNumber, cancellationToken);
        
        if (build == null)
        {
            throw new Exception($"No successful build found for PR #{prNumber}. " +
                $"The PR may not have triggered CI builds yet (draft PRs don't auto-trigger builds), " +
                $"or the build may still be in progress or failed. " +
                $"Check the PR on GitHub to see if builds have completed: https://github.com/dotnet/maui/pull/{prNumber}");
        }

        AnsiConsole.MarkupLine($"[green]✓[/] Found build: {build.BuildNumber}");

        string artifactsPath = string.Empty;
        await AnsiConsole.Status()
            .StartAsync($"Downloading artifacts for build {build.Id}...", async ctx =>
            {
                ctx.Spinner(Spinner.Known.Dots);
                artifactsPath = await _azureDevOpsService.DownloadArtifactAsync(build.Id, build.Organization, build.Project, cancellationToken);
            });

        // Apply outside of spinner to allow prompts for TFM updates
        await _projectUpdater.UpdateToPrBuildAsync(project, artifactsPath, cancellationToken);
        AnsiConsole.MarkupLine("[green]✓[/] Applied PR build successfully");
    }
}
