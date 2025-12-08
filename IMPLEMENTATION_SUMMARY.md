# MauiVersion CLI - Implementation Summary

## ✅ Completed Implementation

This document summarizes the complete implementation of the MauiVersion CLI tool and associated scripts for applying .NET MAUI releases and PR builds to projects.

## 📦 Deliverables

### 1. MauiVersion CLI Tool (.NET Global Tool)
**Location**: `src/MauiVersion/`

A .NET global tool that allows users to apply different .NET MAUI release channels to their projects:
- **Stable**: Latest production release from NuGet.org
- **Nightly**: Latest nightly build from the CI pipeline  
- **PR**: Specific pull request build artifacts from Azure DevOps

**Installation**:
```bash
dotnet tool install -g MauiVersion
```

**Usage**:
```bash
# Interactive mode (choose channel from menu)
maui-version apply

# Stable channel
maui-version apply --channel stable

# Nightly channel
maui-version apply --channel nightly

# PR channel (interactive)
maui-version apply --channel pr

# PR channel (direct)
maui-version apply --apply-pr 32931
```

### 2. Standalone PowerShell Script
**Location**: `scripts/Apply-MauiPR.ps1`

One-line PowerShell command to apply PR builds without installing the CLI:
```powershell
iwr https://raw.githubusercontent.com/dotnet/maui/main/scripts/Apply-MauiPR.ps1 -UseBasicParsing | iex; Apply-MauiPR -PrNumber 12345
```

### 3. Standalone Bash Script
**Location**: `scripts/apply-maui-pr.sh`

One-line Bash command to apply PR builds without installing the CLI:
```bash
curl -fsSL https://raw.githubusercontent.com/dotnet/maui/main/scripts/apply-maui-pr.sh | bash -s 12345
```

## 🎯 Key Features

### Automatic Project Detection
- Finds `.csproj` files with `<UseMaui>true</UseMaui>`
- Detects current MAUI package version
- Reads target framework versions (handles multi-targeting)

### Target Framework Management
- Detects .NET version from `<TargetFrameworks>` or `<TargetFramework>`
- For stable channel: Only installs versions compatible with current TFM
- For nightly/PR channels: Offers to update TFM if package requires newer .NET version
- Updates all conditional TargetFrameworks (e.g., Windows 10 TFM)
- Warns users that other dependencies may need manual updates

### $(MauiVersion) Variable Support
- Detects when projects use `$(MauiVersion)` instead of explicit version
- Replaces with concrete version during apply operation
- Successfully tested with test projects

### Azure DevOps Integration
- Fetches PR information from GitHub API
- Queries GitHub Check Runs API to find associated Azure DevOps builds
- Downloads build artifacts from Azure DevOps
- Supports multiple PR pipeline names (handles both draft and regular PRs)
- Gracefully handles missing builds

### NuGet Configuration
- Creates or updates `NuGet.config` in project directory
- Adds local package source pointing to downloaded artifacts
- For PR builds: Uses hive directory pattern `~/.maui/hives/pr-<NUMBER>/packages`
- Allows testing multiple PR builds side-by-side without conflicts

### User Safety Features
- **Confirmation prompts**: Warns users about testing-only usage
- **Git branch tip**: Suggests creating a test branch
- **Revert instructions**: Clear steps to return to production versions
- **Latest stable version lookup**: Fetches current stable version from NuGet.org for revert guidance

### Repository Override Support
- Environment variable `MAUI_REPO` allows testing forks
- Default: `dotnet/maui`
- Example: `export MAUI_REPO=myfork/maui`

## 🏗️ Architecture

### CLI Tool Structure
```
src/MauiVersion/
├── Commands/
│   ├── ApplyCommand.cs       # Main apply command
│   └── ReleaseChannel.cs     # Channel enum
├── Models/
│   └── MauiProject.cs        # Project model
├── Services/
│   ├── AzureDevOpsService.cs # Azure DevOps API
│   ├── NuGetService.cs       # NuGet.org API
│   ├── NightlyFeedService.cs # Nightly feed API
│   └── ProjectUpdater.cs     # Project file updates
└── Program.cs                # CLI entry point
```

### Script Structure
Both scripts are self-contained with no external dependencies beyond system tools:
- PowerShell: Uses built-in cmdlets
- Bash: Requires `curl`, `unzip`, optionally `jq` for JSON parsing

## 🧪 Testing

### Unit Tests
**Location**: `tests/MauiVersion.Tests/`

Test coverage includes:
- Project detection and parsing
- Version extraction from packages
- Target framework detection and updates
- NuGet.config creation and modification
- $(MauiVersion) variable replacement

### Integration Tests
- End-to-end testing of apply command
- Real API calls to GitHub and Azure DevOps (in CI)
- Package download and extraction
- Project file modifications

### Manual Testing Performed
✅ Applied stable channel to test project  
✅ Applied nightly channel to test project  
✅ Applied PR #32931 build to test project  
✅ Verified hive directory structure  
✅ Tested $(MauiVersion) variable replacement  
✅ Tested target framework updates  
✅ Verified NuGet.config creation  
✅ Tested PowerShell script with PR 32931  
✅ Verified one-line installation commands  

## 📋 GitHub Actions

### CI Workflow
**Location**: `.github/workflows/ci.yml`

Runs on every push and PR:
- Builds the solution
- Runs all tests
- Validates code quality

### Release Workflow
**Location**: `.github/workflows/release.yml`

Triggered on version tags (e.g., `v1.0.0`):
- Builds the tool
- Packs as NuGet package
- Publishes to NuGet.org
- Creates GitHub release with artifacts

## 📄 Documentation

### README.md (Root)
- Overview of the project
- Quick start guide
- Installation instructions
- Usage examples for CLI and scripts
- Feature highlights

### scripts/README.md
- Dedicated documentation for standalone scripts
- One-line command examples
- Requirements and dependencies
- Advanced usage scenarios
- Hive directory pattern explanation
- Repository override instructions
- Bot integration examples
- Troubleshooting guide

### CONTRIBUTING.md
- Development setup guide
- Build and test instructions
- PR guidelines

## 🔄 Release Process

1. Update version in `src/MauiVersion/MauiVersion.csproj`
2. Commit and push changes
3. Create and push a version tag: `git tag v1.0.0 && git push origin v1.0.0`
4. GitHub Actions automatically:
   - Builds the package
   - Publishes to NuGet.org
   - Creates GitHub release

## 🎨 User Experience Highlights

### CLI Tool
- Colorful, intuitive UI using Spectre.Console
- Progress indicators for long-running operations
- Clear error messages with actionable guidance
- Interactive menus for channel selection
- Smart defaults (auto-detect project, latest versions)

### Standalone Scripts
- Single command to apply PR builds
- Beautiful ASCII art headers
- Color-coded output (success, warning, error, info)
- Progress indicators
- Comprehensive confirmation and revert instructions

## 🔐 Security Considerations

- Scripts can be inspected before running (download first approach)
- NuGet packages verified via Azure DevOps artifact download
- No credentials required (public APIs only)
- Local package sources clearly marked in NuGet.config
- Users explicitly warned about testing-only usage

## 📊 Package Distribution

- **NuGet.org**: Primary distribution for the CLI tool
- **GitHub Releases**: Backup distribution with standalone binaries
- **Raw GitHub URLs**: For one-line script execution

## 🌟 Inspiration

This project takes heavy inspiration from the [.NET Aspire CLI](https://github.com/dotnet/aspire/tree/main/src/Aspire.Cli), particularly:
- Channel-based release model
- Update command structure
- PR artifact retrieval scripts
- Hive directory pattern for isolated testing
- Repository override via environment variable

## 🚀 Future Enhancements (Potential)

- Support for updating all MAUI-related packages (not just Microsoft.Maui.Controls)
- Workload synchronization (update workload to match package version)
- List command to show available versions
- Rollback command to revert to previous version
- Configuration file support for project-specific defaults
- Verbose logging mode for troubleshooting
- Support for private Azure DevOps feeds

## ✅ Success Criteria Met

All original requirements have been successfully implemented:

✅ CLI modeled after Aspire CLI  
✅ Multiple release channels (stable, nightly, PR)  
✅ Stable channel updates from NuGet.org  
✅ Nightly channel support with feed configuration  
✅ PR build support with Azure DevOps artifact download  
✅ Interactive PR selection menu  
✅ Command-line PR option (--apply-pr)  
✅ Automatic NuGet.config management  
✅ Project file updates  
✅ Target framework compatibility handling  
✅ Missing build detection and error handling  
✅ Comprehensive testing (unit and integration)  
✅ CI/CD pipeline with automated releases  
✅ Standalone PowerShell and Bash scripts  
✅ One-line command support  
✅ Documentation for end-users and developers  

## 📝 Notes

- The tool is production-ready and fully tested
- All CI tests pass successfully
- Scripts have been manually tested with real PR builds
- Documentation is comprehensive and user-friendly
- The project follows .NET and PowerShell best practices
- Code is clean, maintainable, and well-structured

---

**Project Status**: ✅ Complete and Ready for Release

**Repository**: https://github.com/jfversluis/maui-version
