using System.Text.RegularExpressions;
using System.Xml.Linq;
using Microsoft.Extensions.Logging;

namespace MauiVersion.Services;

public class TargetFrameworkService : ITargetFrameworkService
{
    private readonly ILogger<TargetFrameworkService> _logger;

    public TargetFrameworkService(ILogger<TargetFrameworkService> logger)
    {
        _logger = logger;
    }

    public string? ExtractDotNetVersionFromMauiVersion(string mauiVersion)
    {
        if (string.IsNullOrEmpty(mauiVersion))
            return null;

        var majorVersion = mauiVersion.Split('.')[0];
        
        if (int.TryParse(majorVersion, out int major))
        {
            return $"{major}.0";
        }

        return null;
    }

    public bool IsVersionCompatible(string? projectDotNetVersion, string? packageDotNetVersion)
    {
        if (string.IsNullOrEmpty(projectDotNetVersion) || string.IsNullOrEmpty(packageDotNetVersion))
            return true;

        return projectDotNetVersion == packageDotNetVersion;
    }

    public async Task UpdateTargetFrameworksAsync(string projectPath, string newDotNetVersion, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Updating TargetFrameworks to .NET {Version}", newDotNetVersion);

        var doc = await Task.Run(() => XDocument.Load(projectPath), cancellationToken);

        // Update all TargetFrameworks and TargetFramework elements (including conditional ones)
        var tfmElements = doc.Descendants("TargetFrameworks").Concat(doc.Descendants("TargetFramework")).ToList();

        if (tfmElements.Any())
        {
            foreach (var tfmElement in tfmElements)
            {
                var currentValue = tfmElement.Value;
                var updatedValue = Regex.Replace(currentValue, @"net\d+\.\d+", $"net{newDotNetVersion}");
                
                if (currentValue != updatedValue)
                {
                    tfmElement.Value = updatedValue;
                    _logger.LogInformation("Updated TargetFrameworks from {Old} to {New}", currentValue, updatedValue);
                }
            }

            await Task.Run(() => doc.Save(projectPath), cancellationToken);
            _logger.LogInformation("Note: You may need to update other package dependencies to match .NET {Version}", newDotNetVersion);
        }
    }
}
