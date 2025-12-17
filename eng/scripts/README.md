# .NET MAUI PR Artifact Retrieval Scripts

This directory contains scripts to fetch and apply NuGet artifacts from .NET MAUI pull request builds.

## Scripts

- **`get-maui-pr.sh`** - Bash script for Unix-like systems (Linux, macOS)
- **`get-maui-pr.ps1`** - PowerShell script for cross-platform use (Windows, Linux, macOS)

## Quick Start

### PowerShell
```powershell
iex "& { $(irm https://raw.githubusercontent.com/dotnet/maui/main/eng/scripts/get-maui-pr.ps1) } 33002"
```

### Bash
```bash
curl -fsSL https://raw.githubusercontent.com/dotnet/maui/main/eng/scripts/get-maui-pr.sh | bash -s -- 33002
```

## Local Usage

```bash
# Bash - apply PR #33002 to current directory's MAUI project  
./get-maui-pr.sh 33002

# PowerShell
./get-maui-pr.ps1 33002

# Specify project path (optional - auto-detects by default)
./get-maui-pr.sh 33002 ./MyApp/MyApp.csproj
./get-maui-pr.ps1 -PrNumber 33002 -ProjectPath ./MyApp/MyApp.csproj
```

## Package Storage (Hive Path)

Packages are downloaded to a hive directory:
- **Unix**: `~/.maui/hives/pr-<PR_NUMBER>/packages`
- **Windows**: `%USERPROFILE%\.maui\hives\pr-<PR_NUMBER>\packages`

This isolated approach prevents conflicts between different PR builds.

## Repository Override

You can test PRs from forks by setting the `MAUI_REPO` environment variable:

```bash
# Bash
export MAUI_REPO=myfork/maui
./get-maui-pr.sh 1234
```

```powershell
# PowerShell
$env:MAUI_REPO = 'myfork/maui'
./get-maui-pr.ps1 1234
```

## What These Scripts Do

1. Fetch PR information from GitHub
2. Find the associated Azure DevOps build (looking for "maui-pr" check)
3. Download PackageArtifacts from the build
4. Extract packages to hive directory
5. Detect your project's target framework
6. Create/update NuGet.config with local package source
7. Update package references in your project
8. Optionally update target frameworks if needed (with confirmation)

## Requirements

- .NET SDK installed
- A .NET MAUI project (`.csproj` with `<UseMaui>true</UseMaui>`)
- Internet connection
- **Bash only**: `curl` and `unzip` (standard on macOS/Linux)

## Parameters

### PowerShell Script

| Parameter | Description | Required |
|-----------|-------------|----------|
| `-PrNumber` | PR number to apply | Yes (Position 0) |
| `-ProjectPath` | Path to .csproj (auto-detects if omitted) | No |

### Bash Script

| Argument | Description | Required |
|----------|-------------|----------|
| 1st | PR number to apply | Yes |
| 2nd | Path to .csproj (auto-detects if omitted) | No |

## Safety Features

- ✅ Asks for confirmation before making changes
- ✅ Shows what will be modified
- ✅ Warns about production usage
- ✅ Recommends using a separate Git branch
- ✅ Detects framework mismatches and offers to upgrade
- ✅ Provides clear revert instructions
- ✅ Backs up existing NuGet.config

## After Running

1. Run `dotnet restore` to download packages
2. Build and test your project
3. Report findings on the PR

## Reverting Changes

1. Edit your `.csproj` - change package version back to stable (check [NuGet](https://www.nuget.org/packages/Microsoft.Maui.Controls))
2. Remove the `maui-pr-#` source from `NuGet.config`
3. Run `dotnet restore --force`

**TIP**: Use a separate Git branch for testing!
```bash
git checkout -b test-pr-33002
# ... test changes ...
git checkout main  # Easy revert!
```

## Troubleshooting

### "No build found for PR"
- PR may not have a completed build yet
- Check PR on GitHub for build status (look for "maui-pr" check)
- Verify the build completed successfully on Azure DevOps
- Some PRs may not trigger builds (e.g., documentation-only changes)

### "No .NET MAUI project found"
- Ensure you're in a directory with a `.csproj` file
- Project must have `<UseMaui>true</UseMaui>`

### "Failed to download artifacts"
- Check internet connection
- Artifacts may have expired (Azure DevOps retention policy)
- Try a more recent PR

### "PackageArtifacts not found"
- The build may not have completed the packaging step
- Check if the build succeeded on Azure DevOps
- Some builds may fail before producing artifacts

## Azure DevOps Configuration

The scripts look for builds in:
- **Organization**: `dnceng-public`
- **Project**: `public`
- **Check name**: `maui-pr`
- **Artifact name**: `PackageArtifacts`

## More Information

- [Testing PR Builds Wiki](https://github.com/dotnet/maui/wiki/Testing-PR-Builds)
- [.NET MAUI Nightly Builds](https://github.com/dotnet/maui/wiki/Nightly-Builds)
- [Contributing Guide](https://github.com/dotnet/maui/blob/main/CONTRIBUTING.md)
