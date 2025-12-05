using MauiVersion.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace MauiVersion.Tests;

public class ProjectLocatorTests
{
    private readonly ProjectLocator _projectLocator;

    public ProjectLocatorTests()
    {
        _projectLocator = new ProjectLocator(NullLogger<ProjectLocator>.Instance);
    }

    [Fact]
    public async Task FindMauiProjectAsync_WithValidMauiProject_ReturnsProject()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(tempDir);

        try
        {
            var projectFile = Path.Combine(tempDir, "TestProject.csproj");
            var projectContent = @"<Project Sdk=""Microsoft.NET.Sdk"">
  <PropertyGroup>
    <TargetFramework>net9.0</TargetFramework>
    <UseMaui>true</UseMaui>
  </PropertyGroup>
  <ItemGroup>
    <PackageReference Include=""Microsoft.Maui.Controls"" Version=""10.0.0"" />
  </ItemGroup>
</Project>";
            await File.WriteAllTextAsync(projectFile, projectContent);

            var result = await _projectLocator.FindMauiProjectAsync(tempDir);

            Assert.NotNull(result);
            Assert.Equal(projectFile, result.ProjectFilePath);
            Assert.Equal("10.0.0", result.CurrentMauiVersion);
            Assert.Contains("Microsoft.Maui.Controls", result.MauiPackageReferences);
        }
        finally
        {
            Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public async Task FindMauiProjectAsync_WithNonMauiProject_ReturnsNull()
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

            var result = await _projectLocator.FindMauiProjectAsync(tempDir);

            Assert.Null(result);
        }
        finally
        {
            Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public async Task FindMauiProjectAsync_WithNoProject_ReturnsNull()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(tempDir);

        try
        {
            var result = await _projectLocator.FindMauiProjectAsync(tempDir);

            Assert.Null(result);
        }
        finally
        {
            Directory.Delete(tempDir, true);
        }
    }
}
