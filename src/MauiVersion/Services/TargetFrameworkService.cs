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

        var tfmElement = doc.Descendants("TargetFrameworks").FirstOrDefault();
        var usePlural = tfmElement != null;

        if (tfmElement == null)
        {
            tfmElement = doc.Descendants("TargetFramework").FirstOrDefault();
        }

        if (tfmElement != null)
        {
            var currentValue = tfmElement.Value;
            var updatedValue = Regex.Replace(currentValue, @"net\d+\.\d+", $"net{newDotNetVersion}");
            tfmElement.Value = updatedValue;

            await Task.Run(() => doc.Save(projectPath), cancellationToken);
            _logger.LogInformation("Updated TargetFrameworks from {Old} to {New}", currentValue, updatedValue);
        }
    }
}
