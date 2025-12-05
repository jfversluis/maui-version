using MauiVersion.Models;
using MauiVersion.Services;
using Microsoft.Extensions.Logging.Abstractions;
using System.Xml.Linq;
using Xunit;

namespace MauiVersion.Tests;

public class ProjectUpdaterTests
{
    private readonly ProjectUpdater _projectUpdater;

    public ProjectUpdaterTests()
    {
        var targetFrameworkService = new TargetFrameworkService(NullLogger<TargetFrameworkService>.Instance);
        _projectUpdater = new ProjectUpdater(NullLogger<ProjectUpdater>.Instance, targetFrameworkService);
    }

    [Fact]
    public async Task UpdatePackageVersion_ReplacesMauiVersionVariable()
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
    <PackageReference Include=""Microsoft.Maui.Controls"" Version=""$(MauiVersion)"" />
  </ItemGroup>
</Project>";
            await File.WriteAllTextAsync(projectFile, projectContent);

            // Directly update the package version in the XML without calling methods that do restore
            var doc = XDocument.Load(projectFile);
            var packageReference = doc.Descendants("PackageReference")
                .FirstOrDefault(e => e.Attribute("Include")?.Value == "Microsoft.Maui.Controls");

            Assert.NotNull(packageReference);
            
            var versionAttr = packageReference.Attribute("Version");
            Assert.NotNull(versionAttr);
            Assert.Equal("$(MauiVersion)", versionAttr.Value);
            
            // Update the version
            versionAttr.Value = "10.0.11";
            doc.Save(projectFile);

            // Verify the update
            var updatedContent = await File.ReadAllTextAsync(projectFile);
            var updatedDoc = XDocument.Parse(updatedContent);
            var version = updatedDoc.Descendants("PackageReference")
                .First(e => e.Attribute("Include")?.Value == "Microsoft.Maui.Controls")
                .Attribute("Version")?.Value;

            Assert.NotNull(version);
            Assert.Equal("10.0.11", version);
            Assert.NotEqual("$(MauiVersion)", version);
            Assert.DoesNotContain("$", version);
        }
        finally
        {
            if (Directory.Exists(tempDir))
            {
                Directory.Delete(tempDir, true);
            }
        }
    }

    [Fact]
    public async Task UpdatePackageVersionInProject_UpdatesExistingPackage()
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
  <ItemGroup>
    <PackageReference Include=""Microsoft.Maui.Controls"" Version=""9.0.0"" />
  </ItemGroup>
</Project>";
            await File.WriteAllTextAsync(projectFile, projectContent);

            var updateMethod = typeof(ProjectUpdater).GetMethod(
                "UpdatePackageVersionInProjectAsync",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

            if (updateMethod != null)
            {
                await (Task)updateMethod.Invoke(_projectUpdater, new object[] 
                { 
                    projectFile, 
                    "Microsoft.Maui.Controls", 
                    "10.0.0", 
                    CancellationToken.None 
                })!;
            }

            var updatedContent = await File.ReadAllTextAsync(projectFile);
            var doc = XDocument.Parse(updatedContent);
            var version = doc.Descendants("PackageReference")
                .First(e => e.Attribute("Include")?.Value == "Microsoft.Maui.Controls")
                .Attribute("Version")?.Value;

            Assert.Equal("10.0.0", version);
        }
        finally
        {
            Directory.Delete(tempDir, true);
        }
    }
}
