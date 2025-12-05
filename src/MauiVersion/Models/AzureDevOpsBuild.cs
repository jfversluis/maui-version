using System.Text.Json.Serialization;

namespace MauiVersion.Models;

public class AzureDevOpsBuild
{
    [JsonPropertyName("id")]
    public int Id { get; set; }

    [JsonPropertyName("buildNumber")]
    public string BuildNumber { get; set; } = string.Empty;

    [JsonPropertyName("status")]
    public string Status { get; set; } = string.Empty;

    [JsonPropertyName("result")]
    public string? Result { get; set; }

    [JsonPropertyName("sourceBranch")]
    public string SourceBranch { get; set; } = string.Empty;

    [JsonPropertyName("sourceVersion")]
    public string SourceVersion { get; set; } = string.Empty;

    // Organization and Project for constructing Azure DevOps URLs
    public string Organization { get; set; } = "xamarin";
    public string Project { get; set; } = "6fd3d886-57a5-4e31-8db7-52a1b47c07a8";
}

public class AzureDevOpsBuildsResponse
{
    [JsonPropertyName("count")]
    public int Count { get; set; }

    [JsonPropertyName("value")]
    public List<AzureDevOpsBuild> Value { get; set; } = new();
}

public class AzureDevOpsArtifact
{
    [JsonPropertyName("id")]
    public int Id { get; set; }

    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("resource")]
    public AzureDevOpsArtifactResource? Resource { get; set; }
}

public class AzureDevOpsArtifactResource
{
    [JsonPropertyName("downloadUrl")]
    public string DownloadUrl { get; set; } = string.Empty;
}

public class AzureDevOpsArtifactsResponse
{
    [JsonPropertyName("count")]
    public int Count { get; set; }

    [JsonPropertyName("value")]
    public List<AzureDevOpsArtifact> Value { get; set; } = new();
}
