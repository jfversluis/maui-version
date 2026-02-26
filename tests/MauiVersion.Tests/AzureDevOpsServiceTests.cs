using MauiVersion.Services;
using Xunit;

namespace MauiVersion.Tests;

public class AzureDevOpsServiceTests
{
    [Fact]
    public void HasInProgressMauiPrBuild_WithInProgressBuild_ReturnsTrue()
    {
        var json = """
        {
            "count": 1,
            "value": [
                {
                    "id": 12345,
                    "buildNumber": "20260226.1",
                    "status": "inProgress",
                    "sourceBranch": "refs/pull/34212/merge",
                    "sourceVersion": "abc123",
                    "definition": { "name": "maui-pr" }
                }
            ]
        }
        """;

        Assert.True(AzureDevOpsService.HasInProgressMauiPrBuild(json));
    }

    [Fact]
    public void HasInProgressMauiPrBuild_WithNotStartedBuild_ReturnsTrue()
    {
        var json = """
        {
            "count": 1,
            "value": [
                {
                    "id": 12345,
                    "buildNumber": "20260226.1",
                    "status": "notStarted",
                    "sourceBranch": "refs/pull/34212/merge",
                    "sourceVersion": "abc123",
                    "definition": { "name": "maui-pr" }
                }
            ]
        }
        """;

        Assert.True(AzureDevOpsService.HasInProgressMauiPrBuild(json));
    }

    [Fact]
    public void HasInProgressMauiPrBuild_WithPostponedBuild_ReturnsTrue()
    {
        var json = """
        {
            "count": 1,
            "value": [
                {
                    "id": 12345,
                    "buildNumber": "20260226.1",
                    "status": "postponed",
                    "sourceBranch": "refs/pull/34212/merge",
                    "sourceVersion": "abc123",
                    "definition": { "name": "maui-pr" }
                }
            ]
        }
        """;

        Assert.True(AzureDevOpsService.HasInProgressMauiPrBuild(json));
    }

    [Fact]
    public void HasInProgressMauiPrBuild_WithCompletedBuild_ReturnsFalse()
    {
        var json = """
        {
            "count": 1,
            "value": [
                {
                    "id": 12345,
                    "buildNumber": "20260226.1",
                    "status": "completed",
                    "result": "succeeded",
                    "sourceBranch": "refs/pull/34212/merge",
                    "sourceVersion": "abc123",
                    "definition": { "name": "maui-pr" }
                }
            ]
        }
        """;

        Assert.False(AzureDevOpsService.HasInProgressMauiPrBuild(json));
    }

    [Fact]
    public void HasInProgressMauiPrBuild_WithInProgressNonMauiPrBuild_ReturnsFalse()
    {
        var json = """
        {
            "count": 1,
            "value": [
                {
                    "id": 12345,
                    "buildNumber": "20260226.1",
                    "status": "inProgress",
                    "sourceBranch": "refs/pull/34212/merge",
                    "sourceVersion": "abc123",
                    "definition": { "name": "maui-pr-uitests" }
                }
            ]
        }
        """;

        Assert.False(AzureDevOpsService.HasInProgressMauiPrBuild(json));
    }

    [Fact]
    public void HasInProgressMauiPrBuild_WithNoBuilds_ReturnsFalse()
    {
        var json = """
        {
            "count": 0,
            "value": []
        }
        """;

        Assert.False(AzureDevOpsService.HasInProgressMauiPrBuild(json));
    }

    [Fact]
    public void HasInProgressMauiPrBuild_WithInvalidJson_ReturnsFalse()
    {
        Assert.False(AzureDevOpsService.HasInProgressMauiPrBuild("invalid json"));
    }

    [Fact]
    public void HasInProgressMauiPrBuild_WithMissingValueProperty_ReturnsFalse()
    {
        var json = """{ "count": 0 }""";

        Assert.False(AzureDevOpsService.HasInProgressMauiPrBuild(json));
    }

    [Fact]
    public void HasInProgressMauiPrBuild_WithMixedBuilds_ReturnsTrueWhenAnyInProgress()
    {
        var json = """
        {
            "count": 2,
            "value": [
                {
                    "id": 12346,
                    "buildNumber": "20260226.2",
                    "status": "inProgress",
                    "sourceBranch": "refs/pull/34212/merge",
                    "sourceVersion": "def456",
                    "definition": { "name": "maui-pr" }
                },
                {
                    "id": 12345,
                    "buildNumber": "20260226.1",
                    "status": "completed",
                    "result": "succeeded",
                    "sourceBranch": "refs/pull/34212/merge",
                    "sourceVersion": "abc123",
                    "definition": { "name": "maui-pr" }
                }
            ]
        }
        """;

        Assert.True(AzureDevOpsService.HasInProgressMauiPrBuild(json));
    }

    [Fact]
    public void HasInProgressMauiPrBuild_WithCancellingBuild_ReturnsFalse()
    {
        var json = """
        {
            "count": 1,
            "value": [
                {
                    "id": 12345,
                    "buildNumber": "20260226.1",
                    "status": "cancelling",
                    "sourceBranch": "refs/pull/34212/merge",
                    "sourceVersion": "abc123",
                    "definition": { "name": "maui-pr" }
                }
            ]
        }
        """;

        Assert.False(AzureDevOpsService.HasInProgressMauiPrBuild(json));
    }

    [Fact]
    public void HasInProgressMauiPrBuild_WithMissingDefinition_ReturnsFalse()
    {
        var json = """
        {
            "count": 1,
            "value": [
                {
                    "id": 12345,
                    "buildNumber": "20260226.1",
                    "status": "inProgress",
                    "sourceBranch": "refs/pull/34212/merge",
                    "sourceVersion": "abc123"
                }
            ]
        }
        """;

        Assert.False(AzureDevOpsService.HasInProgressMauiPrBuild(json));
    }
}
