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

        var rawBytes = await File.ReadAllBytesAsync(projectPath, cancellationToken);
        var hasBom = rawBytes.Length >= 3 && rawBytes[0] == 0xEF && rawBytes[1] == 0xBB && rawBytes[2] == 0xBF;
        var content = await File.ReadAllTextAsync(projectPath, cancellationToken);
        var originalContent = content;

        // Find all comment regions to exclude from replacement
        var commentRanges = Regex.Matches(content, @"<!--[\s\S]*?-->")
            .Select(m => (Start: m.Index, End: m.Index + m.Length))
            .ToList();

        bool IsInsideComment(int index) => commentRanges.Any(r => index >= r.Start && index < r.End);

        // Use text-based replacement to preserve original formatting/indentation
        // Only replace matches that are not inside comments
        var pattern = @"(<TargetFrameworks?\b[^>]*>)([^<]*)(</TargetFrameworks?>)";
        var matches = Regex.Matches(content, pattern).Cast<Match>().Reverse().ToList();
        
        foreach (var m in matches)
        {
            if (IsInsideComment(m.Index))
                continue;

            var currentValue = m.Groups[2].Value;
            var updatedValue = Regex.Replace(currentValue, @"net\d+\.\d+", $"net{newDotNetVersion}");

            if (currentValue != updatedValue)
            {
                content = content.Remove(m.Index, m.Length)
                    .Insert(m.Index, m.Groups[1].Value + updatedValue + m.Groups[3].Value);
                _logger.LogInformation("Updated TargetFrameworks from {Old} to {New}", currentValue, updatedValue);
            }
        }

        if (content != originalContent)
        {
            var encoding = new System.Text.UTF8Encoding(hasBom);
            await File.WriteAllTextAsync(projectPath, content, encoding, cancellationToken);
            _logger.LogInformation("Note: You may need to update other package dependencies to match .NET {Version}", newDotNetVersion);
        }
    }
}
