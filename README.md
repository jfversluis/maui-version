# MAUI Version Manager

```
 __  __    _    _   _ ___  __     __             _             
|  \/  |  / \  | | | |_ _| \ \   / /__ _ __ ___(_) ___  _ __  
| |\/| | / _ \ | | | || |   \ \ / / _ \ '__/ __| |/ _ \| '_ \ 
| |  | |/ ___ \| |_| || |    \ V /  __/ |  \__ \ | (_) | | | |
|_|  |_/_/   \_\\___/|___|    \_/ \___|_|  |___/_|\___/|_| |_|
```

[![CI Build](https://github.com/jfversluis/maui-version/actions/workflows/ci.yml/badge.svg)](https://github.com/jfversluis/maui-version/actions/workflows/ci.yml)
[![NuGet](https://img.shields.io/nuget/v/MauiVersion.svg)](https://www.nuget.org/packages/MauiVersion/)
[![Downloads](https://img.shields.io/nuget/dt/MauiVersion.svg)](https://www.nuget.org/packages/MauiVersion/)

A command-line tool for managing .NET MAUI release channels, inspired by the [Aspire CLI](https://github.com/dotnet/aspire/tree/main/src/Aspire.Cli). Easily switch between stable releases, nightly builds, and PR builds for your .NET MAUI projects.

## ✨ Features

- **📦 Stable Channel**: Update to the latest stable release from NuGet.org
- **🌙 Nightly Channel**: Apply nightly builds from the MAUI development feeds
- **🔧 PR Builds**: Test pull request builds before they're merged
- **🎨 Interactive Mode**: Beautiful, user-friendly menus powered by Spectre.Console
- **⚡ Command-Line Mode**: Fully scriptable for CI/CD and automation
- **🎯 Smart TargetFrameworks**: Automatically detects and prompts to update .NET versions
- **🔍 Auto-Discovery**: Finds your MAUI project automatically

## 📥 Installation

### Global Tool (Recommended)

```bash
dotnet tool install --global MauiVersion
```

### Update Existing Installation

```bash
dotnet tool update --global MauiVersion
```

### Uninstall

```bash
dotnet tool uninstall --global MauiVersion
```

## 🎯 Quick Start

Navigate to your .NET MAUI project directory and run:

```bash
maui-version apply
```

The CLI will:
1. 🔍 Automatically detect your MAUI project
2. 📊 Display your current MAUI version and .NET target framework
3. 🎨 Show an interactive menu to select a channel
4. ✅ Apply the selected channel and update your project

## 📖 Usage

### Interactive Mode (Recommended)

```bash
# Run in your MAUI project directory
maui-version apply

# Or specify the project path
maui-version apply --project ./src/MyMauiApp
```

**Example Output:**
```
✓ Found MAUI project: MyApp.csproj
✓ Current version: 9.0.10
✓ Target .NET version: .NET 9.0

Select a release channel:
> Stable
  Nightly
  PR Build
```

### Command-Line Mode

Perfect for automation and CI/CD pipelines!

#### 📦 Stable Channel

```bash
# Apply latest stable release
maui-version apply --channel stable

# With specific project path
maui-version apply --channel stable --project ./src/MyMauiApp
```

The CLI automatically selects the correct stable version based on your project's TargetFrameworks.

#### 🌙 Nightly Channel

```bash
# Apply latest nightly build (interactive prompt for .NET version)
maui-version apply --channel nightly

# Non-interactive with auto-detection
maui-version apply --channel nightly --project ./MyApp
```

Nightly builds come from:
- `https://aka.ms/maui-nightly/index.json` (combined feed)

#### 🔧 PR Builds

Test a specific pull request before it's merged:

```bash
# Apply PR build by number
maui-version apply --apply-pr 32931

# With project path
maui-version apply --apply-pr 32931 --project ./src/MyApp
```

**How PR Builds Work:**
1. The CLI queries GitHub's Checks API for the PR
2. Finds the associated Azure DevOps build
3. Downloads the build artifacts (NuGet packages)
4. Creates a local NuGet.config pointing to the artifacts
5. Updates your project to use the PR build version

**Example PRs you can test:**
```bash
# Test a recent PR
maui-version apply --apply-pr 33002
```

#### Other Options

```bash
# Show help
maui-version apply --help

# Show version
maui-version --version
```

## 🔍 How It Works

### 📦 Stable Channel

1. **Detect**: Reads your project's `TargetFrameworks` (e.g., `net9.0-android;net9.0-ios`)
2. **Query**: Searches NuGet.org for the latest stable MAUI version compatible with your .NET version
3. **Update**: Modifies your `.csproj` to use the latest stable version
4. **Clean**: Removes any custom `NuGet.config` files to use NuGet.org defaults
5. **Restore**: Runs `dotnet restore` to download packages

**Smart Version Selection**: Automatically picks the right MAUI version for your .NET TFM:
- `.NET 9` projects → Latest MAUI 9.x stable
- `.NET 10` projects → Latest MAUI 10.x stable

### 🌙 Nightly Channel

1. **Select**: Choose your .NET version (auto-detected from TargetFrameworks)
2. **Configure**: Creates `NuGet.config` with the MAUI nightly feed
3. **Version Check**: Compares package .NET version with your project's TFM
4. **Prompt**: If mismatch detected, offers to update TargetFrameworks
5. **Update**: Applies the latest nightly version
6. **Restore**: Downloads packages from the nightly feed

**Feed URL**: `https://aka.ms/maui-nightly/index.json`

### 🔧 PR Build Channel

The most advanced feature! Here's how it works:

1. **GitHub Integration**: Queries GitHub Checks API for the PR's commit SHA
2. **Build Discovery**: Finds Azure DevOps builds associated with the PR
   - Supports multiple build configurations (`xamarin/public`, `xamarin/GUID-project`)
   - Works with both main build checks and sub-jobs
   - Handles draft and merged PRs
3. **Download**: Fetches build artifacts from Azure DevOps (no auth required for public repos)
4. **Extract**: Unpacks NuGet packages to a local temp directory
5. **Configure**: Creates a `NuGet.config` pointing to the local packages
6. **Version Check**: Detects if PR build requires a different .NET version
7. **Prompt**: Offers to update TargetFrameworks if needed
8. **Apply**: Updates your project to use the PR build version

**Example Workflow**:
```bash
maui-version apply --apply-pr 32931
```
```
✓ Found PR #32931 on GitHub
✓ Retrieved commit SHA: 20c776fe
✓ Found build check: MAUI-public
✓ Extracted build ID: 155100
✓ Downloading artifacts...
✓ Found MAUI version: 10.0.20-ci.main.25604.11
? Package requires .NET 10.0, but project uses .NET 9.0
? Update TargetFrameworks to .NET 10.0? (y/n)
```

## ⚙️ Requirements

- **.NET 8.0 SDK** or later (to run the CLI tool)
- A **.NET MAUI project** with:
  - `<UseMaui>true</UseMaui>` in the `.csproj`
  - Valid `TargetFrameworks` property (e.g., `net9.0-android;net9.0-ios`)
  - `Microsoft.Maui.Controls` package reference

## 🎯 Use Cases

### Test a PR Before Merging
```bash
# Your team member created PR #33002
# Test it locally before approving
maui-version apply --apply-pr 33002

# Test your app with the PR changes
dotnet build
dotnet run

# Switch back to stable when done
maui-version apply --channel stable
```

### Stay on the Bleeding Edge
```bash
# Use nightly builds in your dev environment
maui-version apply --channel nightly

# Restore to stable for production
maui-version apply --channel stable
```

### Automate Version Updates (CI/CD)
```bash
# In your CI pipeline
- name: Update MAUI to Stable
  run: |
    dotnet tool install --global MauiVersion
    maui-version apply --channel stable --project ./src/MyApp
```

## 🏗️ Architecture

The CLI uses clean architecture principles:

- **Commands Layer**: `System.CommandLine` for parsing and routing
- **Services Layer**: Business logic for project manipulation, Azure DevOps integration, NuGet management
- **Models Layer**: Data structures for channels, projects, builds
- **UI Layer**: `Spectre.Console` for beautiful terminal output

**Key Services:**
- `ProjectLocator` - Finds and validates MAUI projects
- `ProjectUpdater` - Modifies .csproj files and NuGet.config
- `AzureDevOpsService` - GitHub Checks API integration for PR builds
- `TargetFrameworkService` - Manages .NET version compatibility

## 🤖 PR Testing Scripts

Don't want to install the CLI? We've got you covered! Use our standalone scripts to quickly test PR builds with a single command.

### PowerShell (Windows, macOS, Linux)

```powershell
# One-liner to download and apply PR build
iex "& { $(irm https://raw.githubusercontent.com/dotnet/maui/main/eng/scripts/get-maui-pr.ps1) } 12345"
```

### Bash (macOS, Linux)

```bash
# One-liner to download and apply PR build  
curl -fsSL https://raw.githubusercontent.com/dotnet/maui/main/eng/scripts/get-maui-pr.sh | bash -s -- 12345
```

### What These Scripts Do

These standalone scripts provide the same PR build functionality as the CLI without any installation! They will:

1. 🔍 Find your MAUI project in the current directory
2. 🔎 Query GitHub to find the PR and associated build
3. 📦 Download build artifacts from Azure DevOps to hive directory (`~/.maui/hives/pr-#/packages`)
4. ⚙️ Create NuGet.config with local package source
5. ✅ Update your project to use the PR build
6. 🎯 Check and optionally update TargetFrameworks if needed

Perfect for:
- 🚀 Quick PR testing without installation
- 🤖 Bot-generated PR comments (coming soon!)
- 📱 One-off testing scenarios
- 🔄 CI/CD environments

**Repository Override**: Set `MAUI_REPO=owner/name` to test forks.

See [eng/scripts/README.md](eng/scripts/README.md) for full documentation and advanced usage.

## 🤝 Contributing

Contributions are welcome! To contribute:

1. Fork the repository
2. Create a feature branch
3. Make your changes with tests
4. Ensure all tests pass: `dotnet test`
5. Submit a pull request

See the [Contributing section](#🤝-contributing-1) below for detailed development setup.

## 🆘 Troubleshooting

### "No MAUI project found in the current directory"

**Solution**: Make sure you're in a directory with a `.csproj` file that contains `<UseMaui>true</UseMaui>`.

```bash
# Check your current directory
dir *.csproj

# Or specify the project path
maui-version apply --project ./src/MyApp
```

### "No successful build found for PR #XXXXX"

**Possible causes:**
1. The PR hasn't triggered a build yet (draft PRs sometimes don't auto-build)
2. The build is still in progress
3. The build failed
4. The PR is old and artifacts have been cleaned up

**Solution**: Check the PR on GitHub to verify builds completed successfully.

### Nightly packages fail to restore

**Solution**: 
1. Ensure you have internet access
2. Try clearing NuGet cache: `dotnet nuget locals all --clear`
3. Check the feed is accessible: `https://aka.ms/maui-nightly/index.json`

### TargetFrameworks mismatch prompt keeps appearing

This is expected when applying a nightly or PR build that targets a different .NET version than your project.

**Options:**
- Choose "Yes" to update TargetFrameworks (recommended for testing)
- Choose "No" to cancel and stay on current .NET version

### "Cannot show selection prompt" error

This happens when running in a non-interactive terminal.

**Solution**: Use explicit command-line options:
```bash
maui-version apply --channel stable --project .
```


## 🤝 Contributing

Contributions are welcome! Here's how to get started:

### Development Setup

```bash
# Clone the repository
git clone https://github.com/jfversluis/maui-version.git
cd maui-version

# Restore and build
dotnet restore
dotnet build

# Run tests
dotnet test
```

### Local Installation for Testing

Use the provided scripts to quickly build and install the tool locally:

**Windows (PowerShell):**
```powershell
.\eng\install-local.ps1
```

**macOS/Linux:**
```bash
chmod +x eng/install-local.sh
./eng/install-local.sh
```

**To uninstall:**
```powershell
.\eng\install-local.ps1 -Uninstall
```

These scripts will:
- Build the project in Release mode
- Pack the NuGet package
- Uninstall any existing version
- Install the tool globally from the local package

### Running Tests

```bash
# Run all tests
dotnet test

# Run with detailed output
dotnet test --verbosity normal

# Run with coverage
dotnet test --collect:"XPlat Code Coverage"
```

### CI/CD

This project uses GitHub Actions for continuous integration and releases:

- **CI Build** (`.github/workflows/ci.yml`): Runs on every push/PR
  - Multi-OS testing (Ubuntu, Windows, macOS)
  - Tests against .NET 8 and 9
  - Uploads test results and package artifacts

- **Release** (`.github/workflows/release.yml`): Creates releases
  - Triggered by version tags (e.g., `v1.0.0`)
  - Publishes to NuGet.org
  - Creates GitHub Release with changelog

### Creating a Release

```bash
# Create and push a version tag
git tag v1.0.0
git push origin v1.0.0

# GitHub Actions will automatically:
# 1. Build and test
# 2. Create NuGet package
# 3. Publish to NuGet.org
# 4. Create GitHub Release
```
## 💡 Inspiration

This tool is heavily inspired by the [Aspire CLI](https://github.com/dotnet/aspire/tree/main/src/Aspire.Cli):
- Channel-based version management
- Interactive terminal experience with Spectre.Console
- Project file manipulation patterns

Special thanks to the .NET MAUI team for:
- The nightly build infrastructure
- Azure DevOps build artifacts
- Public GitHub Checks API integration

## 📝 Related Resources

- [.NET MAUI Nightly Builds Wiki](https://github.com/dotnet/maui/wiki/Nightly-Builds)
- [Testing PR Builds Guide](https://github.com/dotnet/maui/wiki/Testing-PR-Builds)
- [Aspire CLI Source](https://github.com/dotnet/aspire/tree/main/src/Aspire.Cli)
- [.NET MAUI GitHub](https://github.com/dotnet/maui)

## 📄 License

MIT License

---

**Made with ❤️ for the .NET MAUI community**
