using System.Xml.Linq;
using MauiVersion.Models;
using Microsoft.Extensions.Logging;

namespace MauiVersion.Services;

public class ProjectLocator : IProjectLocator
{
    private readonly ILogger<ProjectLocator> _logger;

    public ProjectLocator(ILogger<ProjectLocator> logger)
    {
        _logger = logger;
    }

    public async Task<MauiProject?> FindMauiProjectAsync(string? projectPath, CancellationToken cancellationToken = default)
    {
        string? projectFile = null;

        if (!string.IsNullOrEmpty(projectPath))
        {
            if (File.Exists(projectPath) && projectPath.EndsWith(".csproj", StringComparison.OrdinalIgnoreCase))
            {
                projectFile = projectPath;
            }
            else if (Directory.Exists(projectPath))
            {
                projectFile = Directory.GetFiles(projectPath, "*.csproj").FirstOrDefault();
            }
        }
        else
        {
            projectFile = Directory.GetFiles(Directory.GetCurrentDirectory(), "*.csproj").FirstOrDefault();
        }

        if (projectFile == null)
        {
            _logger.LogError("No .csproj file found");
            return null;
        }

        var doc = await Task.Run(() => XDocument.Load(projectFile), cancellationToken);
        
        var isMauiProject = doc.Descendants("UseMaui")
            .Any(e => e.Value.Equals("true", StringComparison.OrdinalIgnoreCase));

        if (!isMauiProject)
        {
            _logger.LogError("Project {ProjectFile} is not a MAUI project", projectFile);
            return null;
        }

        var mauiPackages = doc.Descendants("PackageReference")
            .Where(e => e.Attribute("Include")?.Value.StartsWith("Microsoft.Maui", StringComparison.OrdinalIgnoreCase) == true)
            .Select(e => e.Attribute("Include")!.Value)
            .Distinct()
            .ToList();

        var currentVersion = doc.Descendants("PackageReference")
            .Where(e => e.Attribute("Include")?.Value == "Microsoft.Maui.Controls")
            .Select(e => e.Attribute("Version")?.Value)
            .FirstOrDefault();

        var targetFrameworks = new List<string>();
        string? dotNetVersion = null;
        
        var tfmElement = doc.Descendants("TargetFrameworks").FirstOrDefault() 
                         ?? doc.Descendants("TargetFramework").FirstOrDefault();
        
        if (tfmElement != null)
        {
            var tfmValue = tfmElement.Value;
            targetFrameworks = tfmValue.Split(';', StringSplitOptions.RemoveEmptyEntries)
                .Select(t => t.Trim())
                .ToList();
            
            var firstTfm = targetFrameworks.FirstOrDefault();
            if (firstTfm != null)
            {
                var netMatch = System.Text.RegularExpressions.Regex.Match(firstTfm, @"net(\d+\.\d+)");
                if (netMatch.Success)
                {
                    dotNetVersion = netMatch.Groups[1].Value;
                }
            }
        }

        return new MauiProject
        {
            ProjectFilePath = projectFile,
            CurrentMauiVersion = currentVersion,
            MauiPackageReferences = mauiPackages,
            TargetFrameworks = targetFrameworks,
            DotNetVersion = dotNetVersion
        };
    }
}
