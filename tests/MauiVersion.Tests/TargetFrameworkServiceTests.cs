using MauiVersion.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace MauiVersion.Tests;

public class TargetFrameworkServiceTests
{
    private readonly TargetFrameworkService _service;

    public TargetFrameworkServiceTests()
    {
        _service = new TargetFrameworkService(NullLogger<TargetFrameworkService>.Instance);
    }

    [Theory]
    [InlineData("9.0.120", "9.0")]
    [InlineData("10.0.11", "10.0")]
    [InlineData("10.0.0-ci.inflight.25553.5", "10.0")]
    [InlineData("8.0.100", "8.0")]
    public void ExtractDotNetVersionFromMauiVersion_ExtractsCorrectly(string mauiVersion, string expectedDotNetVersion)
    {
        var result = _service.ExtractDotNetVersionFromMauiVersion(mauiVersion);
        Assert.Equal(expectedDotNetVersion, result);
    }

    [Theory]
    [InlineData("9.0", "9.0", true)]
    [InlineData("9.0", "10.0", false)]
    [InlineData("10.0", "10.0", true)]
    [InlineData(null, "10.0", true)] // null is considered compatible
    [InlineData("9.0", null, true)] // null is considered compatible
    public void IsVersionCompatible_ChecksCorrectly(string? projectVersion, string? packageVersion, bool expected)
    {
        var result = _service.IsVersionCompatible(projectVersion, packageVersion);
        Assert.Equal(expected, result);
    }

    [Fact]
    public async Task UpdateTargetFrameworksAsync_UpdatesAllTfms()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(tempDir);

        try
        {
            var projectFile = Path.Combine(tempDir, "TestProject.csproj");
            var projectContent = @"<Project Sdk=""Microsoft.NET.Sdk"">
  <PropertyGroup>
    <TargetFrameworks>net9.0-android;net9.0-ios;net9.0-maccatalyst</TargetFrameworks>
    <UseMaui>true</UseMaui>
  </PropertyGroup>
</Project>";
            await File.WriteAllTextAsync(projectFile, projectContent);

            await _service.UpdateTargetFrameworksAsync(projectFile, "10.0", CancellationToken.None);

            var updatedContent = await File.ReadAllTextAsync(projectFile);
            
            Assert.Contains("net10.0-android", updatedContent);
            Assert.Contains("net10.0-ios", updatedContent);
            Assert.Contains("net10.0-maccatalyst", updatedContent);
            Assert.DoesNotContain("net9.0", updatedContent);
        }
        finally
        {
            Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public async Task UpdateTargetFrameworksAsync_UpdatesSingleTfm()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(tempDir);

        try
        {
            var projectFile = Path.Combine(tempDir, "TestProject.csproj");
            var projectContent = @"<Project Sdk=""Microsoft.NET.Sdk"">
  <PropertyGroup>
    <TargetFramework>net9.0</TargetFramework>
  </PropertyGroup>
</Project>";
            await File.WriteAllTextAsync(projectFile, projectContent);

            await _service.UpdateTargetFrameworksAsync(projectFile, "10.0", CancellationToken.None);

            var updatedContent = await File.ReadAllTextAsync(projectFile);
            
            Assert.Contains("net10.0", updatedContent);
            Assert.DoesNotContain("net9.0", updatedContent);
        }
        finally
        {
            Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public async Task UpdateTargetFrameworksAsync_PreservesOriginalFormatting()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(tempDir);

        try
        {
            var projectFile = Path.Combine(tempDir, "TestProject.csproj");
            // Use 4-space indentation to verify it's preserved (not reset to 2 spaces)
            var projectContent = "<Project Sdk=\"Microsoft.NET.Sdk\">\r\n" +
                "    <PropertyGroup>\r\n" +
                "        <TargetFrameworks>net9.0-android;net9.0-ios;net9.0-maccatalyst</TargetFrameworks>\r\n" +
                "        <UseMaui>true</UseMaui>\r\n" +
                "    </PropertyGroup>\r\n" +
                "</Project>";
            await File.WriteAllTextAsync(projectFile, projectContent);

            await _service.UpdateTargetFrameworksAsync(projectFile, "10.0", CancellationToken.None);

            var updatedContent = await File.ReadAllTextAsync(projectFile);

            // Verify TFMs were updated
            Assert.Contains("net10.0-android", updatedContent);
            Assert.Contains("net10.0-ios", updatedContent);
            Assert.DoesNotContain("net9.0", updatedContent);

            // Verify original 4-space indentation is preserved
            Assert.Contains("    <PropertyGroup>", updatedContent);
            Assert.Contains("        <TargetFrameworks>", updatedContent);
            Assert.Contains("        <UseMaui>", updatedContent);

            // Verify no XML declaration was added
            Assert.DoesNotContain("<?xml", updatedContent);
        }
        finally
        {
            Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public async Task UpdateTargetFrameworksAsync_UpdatesConditionalTargetFrameworks()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(tempDir);

        try
        {
            var projectFile = Path.Combine(tempDir, "TestProject.csproj");
            var projectContent = "<Project Sdk=\"Microsoft.NET.Sdk\">\r\n" +
                "    <PropertyGroup>\r\n" +
                "        <TargetFrameworks Condition=\"'$(TargetFrameworks)' == ''\">net9.0-android;net9.0-ios</TargetFrameworks>\r\n" +
                "        <UseMaui>true</UseMaui>\r\n" +
                "    </PropertyGroup>\r\n" +
                "</Project>";
            await File.WriteAllTextAsync(projectFile, projectContent);

            await _service.UpdateTargetFrameworksAsync(projectFile, "10.0", CancellationToken.None);

            var updatedContent = await File.ReadAllTextAsync(projectFile);

            // Verify TFMs were updated
            Assert.Contains("net10.0-android", updatedContent);
            Assert.Contains("net10.0-ios", updatedContent);
            Assert.DoesNotContain("net9.0", updatedContent);

            // Verify Condition attribute is preserved
            Assert.Contains("Condition=", updatedContent);

            // Verify original 4-space indentation is preserved
            Assert.Contains("    <PropertyGroup>", updatedContent);
            Assert.Contains("        <TargetFrameworks Condition=", updatedContent);
        }
        finally
        {
            Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public async Task UpdateTargetFrameworksAsync_DoesNotModifyCommentedOutTargetFrameworks()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(tempDir);

        try
        {
            var projectFile = Path.Combine(tempDir, "TestProject.csproj");
            var projectContent = "<Project Sdk=\"Microsoft.NET.Sdk\">\r\n" +
                "    <PropertyGroup>\r\n" +
                "        <!-- <TargetFramework>net8.0</TargetFramework> -->\r\n" +
                "        <TargetFramework>net9.0</TargetFramework>\r\n" +
                "    </PropertyGroup>\r\n" +
                "</Project>";
            await File.WriteAllTextAsync(projectFile, projectContent);

            await _service.UpdateTargetFrameworksAsync(projectFile, "10.0", CancellationToken.None);

            var updatedContent = await File.ReadAllTextAsync(projectFile);

            // Verify only the non-commented TFM was updated
            Assert.Contains("<TargetFramework>net10.0</TargetFramework>", updatedContent);

            // Verify commented-out TFM was NOT modified (still has net8.0)
            Assert.Contains("<!-- <TargetFramework>net8.0</TargetFramework> -->", updatedContent);
        }
        finally
        {
            Directory.Delete(tempDir, true);
        }
    }
}
