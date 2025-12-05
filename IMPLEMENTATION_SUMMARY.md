# MauiVersion Implementation Summary

## Overview
MauiVersion is a CLI tool and script collection for managing .NET MAUI versions in projects. It provides easy ways to switch between stable, nightly, and PR builds of .NET MAUI.

## Key Features

### 1. CLI Tool (`maui-version`)
A cross-platform .NET global tool that helps developers manage MAUI versions:

**Channels:**
- **Stable**: Latest stable release from NuGet.org
- **Nightly**: Latest nightly builds from dotnet/maui CI
- **PR**: Specific PR builds from Azure DevOps

**Key Capabilities:**
- Automatically detects MAUI projects in the current directory
- Updates `Microsoft.Maui.Controls` package references
- Manages NuGet.config for non-standard package sources
- Validates .NET version compatibility between project TFMs and MAUI packages
- Offers to update TargetFrameworks when needed for nightly/PR builds
- Replaces `$(MauiVersion)` variables with concrete versions

**Smart TFM Handling:**
- Detects current TargetFrameworks (.NET 8, 9, 10, etc.)
- For stable: Only fetches compatible versions
- For nightly/PR: Prompts to update TFMs if package requires newer .NET version
- Preserves existing TFM structure (respects multi-targeting)

### 2. Standalone Scripts
Two standalone scripts that can be hosted on GitHub and run with a single command:

#### PowerShell Script (`Apply-MauiPR.ps1`)
```powershell
iex "& { $(irm https://raw.githubusercontent.com/dotnet/maui/main/scripts/Apply-MauiPR.ps1) } -PrNumber 12345"
```

#### Bash Script (`apply-maui-pr.sh`)
```bash
curl -fsSL https://raw.githubusercontent.com/dotnet/maui/main/scripts/apply-maui-pr.sh | bash -s -- 12345
```

**Script Features:**
- Self-contained - no dependencies on the CLI tool
- Downloads PR artifacts from Azure DevOps
- Extracts NuGet packages
- Creates local NuGet.config
- Updates project file with PR version
- Includes safety confirmations
- Provides revert instructions
- Works on Windows, macOS, and Linux

## Architecture

### Services
- **ProjectLocator**: Finds MAUI projects in directories
- **NuGetService**: Queries NuGet.org and Azure Artifacts feeds
- **AzureDevOpsService**: Finds PR builds using GitHub Checks API
- **TargetFrameworkService**: Manages TargetFrameworks and version compatibility
- **ProjectUpdater**: Updates .csproj files and manages NuGet.config

### Models
- **MauiProject**: Represents a MAUI project with its metadata
- **ReleaseChannel**: Enum for Stable/Nightly/PR channels
- **BuildInfo**: Azure DevOps build information
- **ArtifactInfo**: Package artifact metadata

## Testing
Comprehensive test suite covering:
- Unit tests for all services
- Integration tests for CLI commands
- Cross-platform executable detection
- TFM compatibility validation
- $(MauiVersion) variable replacement
- NuGet.config management

## CI/CD
- **CI Workflow**: Builds and tests on Windows, macOS, and Linux
- **Release Workflow**: Publishes to NuGet.org on version tags
- Automated test result uploads
- Multi-platform artifact generation

## Distribution
1. **NuGet Package**: `dotnet tool install -g MauiVersion`
2. **Direct Scripts**: One-liners for PR testing
3. **Source**: Available on GitHub for contributions

## Usage Patterns

### For End Users (Stable/Nightly)
```bash
# Install
dotnet tool install -g MauiVersion

# Switch to stable
maui-version apply --channel stable

# Switch to nightly
maui-version apply --channel nightly
```

### For PR Testing (Scripts)
Bot comments on PRs with:
```
Test this PR:
PowerShell: iex "& { $(irm https://raw.githubusercontent.com/dotnet/maui/main/scripts/Apply-MauiPR.ps1) } -PrNumber 32931"
Bash: curl -fsSL https://raw.githubusercontent.com/dotnet/maui/main/scripts/apply-maui-pr.sh | bash -s -- 32931
```

### For PR Testing (CLI)
```bash
maui-version apply --pr 32931
# or interactive
maui-version apply --channel pr
```

## Design Decisions

### Why Both CLI and Scripts?
- **CLI**: Full-featured, best for regular users, requires installation
- **Scripts**: Zero-install, perfect for one-time PR testing, easy to share in comments

### Why Standalone Scripts?
- No dependencies on the CLI tool
- Can be updated independently
- Easier to customize per-repository
- Lower barrier to entry for PR testers

### TFM Handling Philosophy
- Non-destructive: Never removes or adds platforms (android, ios, etc.)
- Only updates the .NET version portion
- Respects user's project structure
- Provides clear prompts before making changes

### Safety Features
- Confirmation prompts before applying PR builds
- Clear warnings about non-production use
- Detailed revert instructions
- Recommends using Git branches for testing

## Future Enhancements
Potential additions:
- Support for other MAUI packages (Essentials, CommunityToolkit, etc.)
- Automatic creation of test branches
- Build artifact caching
- Support for private Azure DevOps instances
- Integration with `dotnet workload` commands

## Implementation Notes

### GitHub Checks API
We use GitHub's Checks API instead of Azure DevOps API directly because:
- No authentication required
- More reliable for public repos
- Provides direct links to Azure DevOps builds
- Works consistently across all PRs

### Artifact Extraction
- Downloads artifacts as ZIP from Azure DevOps
- Extracts NuGet packages to temporary directory
- Creates local package source
- Cleans up temporary files after use

### Version Detection
- Uses NuGet V3 API for latest versions
- Parses Azure Artifacts feeds for nightly builds
- Extracts version from .nupkg filenames
- Validates version format before applying

## Contributing
The codebase follows:
- Async/await patterns throughout
- Dependency injection for services
- Structured logging
- Comprehensive error handling
- Cross-platform compatibility
