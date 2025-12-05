using Xunit;
using System.Diagnostics;

namespace MauiVersion.Tests;

public class IntegrationTests
{
    private readonly string _cliPath;

    public IntegrationTests()
    {
        var baseDir = AppContext.BaseDirectory;
        _cliPath = Path.Combine(baseDir, "..", "..", "..", "..", "..", "src", "MauiVersion", "bin", "Release", "net8.0", "maui-version.exe");
        _cliPath = Path.GetFullPath(_cliPath);
    }

    [Fact]
    public async Task CliExecutable_Exists()
    {
        Assert.True(File.Exists(_cliPath), $"CLI executable not found at {_cliPath}");
    }

    [Fact]
    public async Task Cli_ShowsHelp()
    {
        var (exitCode, output) = await RunCliAsync("--help");
        
        Assert.Equal(0, exitCode);
        Assert.Contains("MAUI CLI", output);
        Assert.Contains("apply", output);
    }

    [Fact]
    public async Task Cli_ShowsVersion()
    {
        var (exitCode, output) = await RunCliAsync("--version");
        
        Assert.Equal(0, exitCode);
        Assert.Matches(@"\d+\.\d+\.\d+", output);
    }

    [Fact]
    public async Task ApplyCommand_ShowsHelp()
    {
        var (exitCode, output) = await RunCliAsync("apply --help");
        
        Assert.Equal(0, exitCode);
        Assert.Contains("Apply a MAUI release channel", output);
        Assert.Contains("--project", output);
        Assert.Contains("--channel", output);
        Assert.Contains("--apply-pr", output);
    }

    [Fact]
    public async Task ApplyCommand_WithNonExistentProject_ReturnsError()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(tempDir);

        try
        {
            var (exitCode, output) = await RunCliAsync($"apply --channel stable --project \"{tempDir}\"");
            
            Assert.Equal(1, exitCode);
            Assert.Contains("No MAUI project found", output);
        }
        finally
        {
            Directory.Delete(tempDir, true);
        }
    }

    private async Task<(int exitCode, string output)> RunCliAsync(string arguments)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = _cliPath,
            Arguments = arguments,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        using var process = new Process { StartInfo = startInfo };
        process.Start();

        var output = await process.StandardOutput.ReadToEndAsync();
        var error = await process.StandardError.ReadToEndAsync();
        
        await process.WaitForExitAsync();

        return (process.ExitCode, output + error);
    }
}
