using System.CommandLine;
using MauiVersion.Commands;
using MauiVersion.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace MauiVersion;

class Program
{
    static async Task<int> Main(string[] args)
    {
        var services = new ServiceCollection();
        
        services.AddLogging(builder =>
        {
            builder.AddConsole();
            builder.SetMinimumLevel(LogLevel.Warning);
        });

        services.AddSingleton<IProjectLocator, ProjectLocator>();
        services.AddSingleton<IProjectUpdater, ProjectUpdater>();
        services.AddSingleton<IAzureDevOpsService, AzureDevOpsService>();
        services.AddSingleton<ITargetFrameworkService, TargetFrameworkService>();
        
        services.AddSingleton<ApplyCommand>();

        var serviceProvider = services.BuildServiceProvider();

        var rootCommand = new RootCommand("MAUI CLI - Manage .NET MAUI release channels");
        
        var applyCommand = serviceProvider.GetRequiredService<ApplyCommand>();
        rootCommand.AddCommand(applyCommand);

        return await rootCommand.InvokeAsync(args);
    }
}
