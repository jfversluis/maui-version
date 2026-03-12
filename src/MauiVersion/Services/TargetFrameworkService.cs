using System.Text.RegularExpressions;
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

        var content = await File.ReadAllTextAsync(projectPath, cancellationToken);
        var originalContent = content;

        // Use text-based replacement to preserve original formatting/indentation
        content = Regex.Replace(content, @"(<TargetFrameworks?\b[^>]*>)([^<]*)(</TargetFrameworks?>)", m =>
        {
            var currentValue = m.Groups[2].Value;
            var updatedValue = Regex.Replace(currentValue, @"net\d+\.\d+", $"net{newDotNetVersion}");

            if (currentValue != updatedValue)
            {
                _logger.LogInformation("Updated TargetFrameworks from {Old} to {New}", currentValue, updatedValue);
            }

            return m.Groups[1].Value + updatedValue + m.Groups[3].Value;
        });

        if (content != originalContent)
        {
            await File.WriteAllTextAsync(projectPath, content, cancellationToken);
            _logger.LogInformation("Note: You may need to update other package dependencies to match .NET {Version}", newDotNetVersion);
        }
    }
}
