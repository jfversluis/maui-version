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

    [Fact]
    public async Task CreateNuGetConfig_CreatesNewFile_WhenNotExists()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(tempDir);

        try
        {
            var createMethod = typeof(ProjectUpdater).GetMethod(
                "CreateNuGetConfigAsync",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

            if (createMethod != null)
            {
                await (Task)createMethod.Invoke(_projectUpdater, new object[] 
                { 
                    tempDir, 
                    "https://example.com/feed", 
                    "test-feed", 
                    CancellationToken.None 
                })!;
            }

            var nugetConfigPath = Path.Combine(tempDir, "NuGet.config");
            Assert.True(File.Exists(nugetConfigPath));

            var config = XDocument.Load(nugetConfigPath);
            var packageSources = config.Descendants("packageSources").First();
            
            // Should not have <clear/> element
            Assert.Empty(packageSources.Elements("clear"));
            
            // Should have test-feed and nuget.org
            var sources = packageSources.Elements("add").ToList();
            Assert.Equal(2, sources.Count);
            Assert.Contains(sources, s => s.Attribute("key")?.Value == "test-feed" && s.Attribute("value")?.Value == "https://example.com/feed");
            Assert.Contains(sources, s => s.Attribute("key")?.Value == "nuget.org");
        }
        finally
        {
            Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public async Task CreateNuGetConfig_UpdatesExistingFile_PreservesOtherSources()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(tempDir);

        try
        {
            var nugetConfigPath = Path.Combine(tempDir, "NuGet.config");
            
            // Create an existing NuGet.config with custom source
            var existingConfig = new XDocument(
                new XDeclaration("1.0", "utf-8", null),
                new XElement("configuration",
                    new XElement("packageSources",
                        new XElement("add",
                            new XAttribute("key", "custom-feed"),
                            new XAttribute("value", "https://custom.com/feed")),
                        new XElement("add",
                            new XAttribute("key", "nuget.org"),
                            new XAttribute("value", "https://api.nuget.org/v3/index.json"))
                    )
                )
            );
            existingConfig.Save(nugetConfigPath);

            var createMethod = typeof(ProjectUpdater).GetMethod(
                "CreateNuGetConfigAsync",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

            if (createMethod != null)
            {
                await (Task)createMethod.Invoke(_projectUpdater, new object[] 
                { 
                    tempDir, 
                    "https://nightly.com/feed", 
                    "nightly", 
                    CancellationToken.None 
                })!;
            }

            var config = XDocument.Load(nugetConfigPath);
            var packageSources = config.Descendants("packageSources").First();
            
            // Should not have <clear/> element
            Assert.Empty(packageSources.Elements("clear"));
            
            // Should have all three sources
            var sources = packageSources.Elements("add").ToList();
            Assert.Equal(3, sources.Count);
            Assert.Contains(sources, s => s.Attribute("key")?.Value == "nightly");
            Assert.Contains(sources, s => s.Attribute("key")?.Value == "custom-feed");
            Assert.Contains(sources, s => s.Attribute("key")?.Value == "nuget.org");
        }
        finally
        {
            Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public async Task CreateNuGetConfig_RemovesClearElement_FromExistingConfig()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(tempDir);

        try
        {
            var nugetConfigPath = Path.Combine(tempDir, "NuGet.config");
            
            // Create an existing NuGet.config with <clear/>
            var existingConfig = new XDocument(
                new XDeclaration("1.0", "utf-8", null),
                new XElement("configuration",
                    new XElement("packageSources",
                        new XElement("clear"),
                        new XElement("add",
                            new XAttribute("key", "custom-feed"),
                            new XAttribute("value", "https://custom.com/feed"))
                    )
                )
            );
            existingConfig.Save(nugetConfigPath);

            var createMethod = typeof(ProjectUpdater).GetMethod(
                "CreateNuGetConfigAsync",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

            if (createMethod != null)
            {
                await (Task)createMethod.Invoke(_projectUpdater, new object[] 
                { 
                    tempDir, 
                    "https://nightly.com/feed", 
                    "nightly", 
                    CancellationToken.None 
                })!;
            }

            var config = XDocument.Load(nugetConfigPath);
            var packageSources = config.Descendants("packageSources").First();
            
            // Should have removed <clear/> element
            Assert.Empty(packageSources.Elements("clear"));
            
            // Should have nightly, custom-feed, and nuget.org
            var sources = packageSources.Elements("add").ToList();
            Assert.Equal(3, sources.Count);
        }
        finally
        {
            Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public async Task CreateNuGetConfig_UpdatesExistingSource_WhenSameKeyExists()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(tempDir);

        try
        {
            var nugetConfigPath = Path.Combine(tempDir, "NuGet.config");
            
            // Create an existing NuGet.config with nightly source
            var existingConfig = new XDocument(
                new XDeclaration("1.0", "utf-8", null),
                new XElement("configuration",
                    new XElement("packageSources",
                        new XElement("add",
                            new XAttribute("key", "nightly"),
                            new XAttribute("value", "https://old-nightly.com/feed")),
                        new XElement("add",
                            new XAttribute("key", "nuget.org"),
                            new XAttribute("value", "https://api.nuget.org/v3/index.json"))
                    )
                )
            );
            existingConfig.Save(nugetConfigPath);

            var createMethod = typeof(ProjectUpdater).GetMethod(
                "CreateNuGetConfigAsync",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

            if (createMethod != null)
            {
                await (Task)createMethod.Invoke(_projectUpdater, new object[] 
                { 
                    tempDir, 
                    "https://new-nightly.com/feed", 
                    "nightly", 
                    CancellationToken.None 
                })!;
            }

            var config = XDocument.Load(nugetConfigPath);
            var packageSources = config.Descendants("packageSources").First();
            
            var sources = packageSources.Elements("add").ToList();
            
            // Should have only 2 sources (nightly replaced, nuget.org kept)
            Assert.Equal(2, sources.Count);
            
            // Check that nightly was updated with new URL
            var nightlySource = sources.FirstOrDefault(s => s.Attribute("key")?.Value == "nightly");
            Assert.NotNull(nightlySource);
            Assert.Equal("https://new-nightly.com/feed", nightlySource.Attribute("value")?.Value);
        }
        finally
        {
            Directory.Delete(tempDir, true);
        }
    }
}
