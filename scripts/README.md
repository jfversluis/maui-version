# .NET MAUI PR Build Scripts

One-line commands to test PR builds from the dotnet/maui repository in your local projects.

## 🚀 Quick Start (One-Line Commands)

### PowerShell (Windows, macOS, Linux)

```powershell
iwr https://raw.githubusercontent.com/dotnet/maui/main/scripts/Apply-MauiPR.ps1 -UseBasicParsing | iex; Apply-MauiPR -PrNumber 12345
```

### Bash (macOS, Linux)

```bash
curl -fsSL https://raw.githubusercontent.com/dotnet/maui/main/scripts/apply-maui-pr.sh | bash -s 12345
```

**That's it!** Just replace `12345` with your PR number and run the command in your MAUI project directory.

## 🎯 What These Scripts Do

When you run the one-line command:

1. ✅ Finds your MAUI project automatically
2. ✅ Fetches the PR's build artifacts from Azure DevOps
3. ✅ Downloads the NuGet packages
4. ✅ Configures your project to use the PR build
5. ✅ Updates package references
6. ✅ Handles .NET version compatibility

No installation needed - just run and test!

## 📋 Requirements

### PowerShell
- PowerShell 7+ (or Windows PowerShell 5.1+)
- .NET SDK
- Internet connection

### Bash
- bash, curl, unzip
- jq (optional, but recommended)
- .NET SDK
- Internet connection

**Install bash dependencies:**
```bash
# macOS
brew install jq

# Ubuntu/Debian
sudo apt-get install jq curl unzip

# Fedora/RHEL
sudo dnf install jq curl unzip
```

## 📖 Advanced Usage

If you prefer to download and inspect the scripts first:

### PowerShell
```powershell
# Download
Invoke-WebRequest -Uri "https://raw.githubusercontent.com/dotnet/maui/main/scripts/Apply-MauiPR.ps1" -OutFile "Apply-MauiPR.ps1"

# Run
./Apply-MauiPR.ps1 -PRNumber 12345

# Specify project path
./Apply-MauiPR.ps1 -PRNumber 12345 -ProjectPath ./MyApp/MyApp.csproj

# Get help
Get-Help ./Apply-MauiPR.ps1 -Detailed
```

### Bash
```bash
# Download
curl -O https://raw.githubusercontent.com/dotnet/maui/main/scripts/apply-maui-pr.sh
chmod +x apply-maui-pr.sh

# Run
./apply-maui-pr.sh 12345

# Specify project path
./apply-maui-pr.sh 12345 ./MyApp/MyApp.csproj
```

## 🚀 How It Works

Both scripts perform the same operations:

1. **Find Project**: Locates a .NET MAUI project (`.csproj` with `<UseMaui>true</UseMaui>`)
2. **Fetch PR Info**: Retrieves PR details from GitHub API
3. **Find Build**: Locates the completed Azure DevOps build from the PR's check runs
4. **Download Artifacts**: Downloads the `nuget` artifact containing the PR build packages
5. **Extract Packages**: Extracts NuGet packages to a temporary location
6. **Check Compatibility**: Verifies target framework compatibility and offers to update if needed
7. **Request Confirmation**: Shows a warning and asks for confirmation before applying changes
8. **Configure NuGet**: Creates or updates `NuGet.config` to include the local package source
9. **Update References**: Updates the `Microsoft.Maui.Controls` package reference to the PR version
10. **Show Revert Instructions**: Displays clear instructions on how to revert to a production version

## 🎨 Features

- ✅ **Automatic Detection**: Finds MAUI projects automatically
- ✅ **Framework Compatibility**: Checks and optionally updates target frameworks
- ✅ **$(MauiVersion) Support**: Handles projects using the `$(MauiVersion)` variable
- ✅ **NuGet.config Management**: Automatically configures package sources
- ✅ **Safety Confirmation**: Warns users about testing-only usage and requests confirmation
- ✅ **Revert Instructions**: Clear guidance on how to return to production versions
- ✅ **User-Friendly**: Colorful output with progress indicators
- ✅ **Error Handling**: Clear error messages and troubleshooting tips
- ✅ **No External Dependencies**: Everything needed is downloaded automatically

## 📝 Example Output

```
╔═══════════════════════════════════════════════════════════╗
║                                                           ║
║        .NET MAUI PR Build Applicator                     ║
║                                                           ║
╚═══════════════════════════════════════════════════════════╝

▶️  Finding MAUI project
✅ Found project: MyMauiApp.csproj

▶️  Fetching PR information
ℹ️  PR #33002: Fix SearchHandler suggestions gap
ℹ️  State: open

▶️  Detecting target framework
ℹ️  Current target framework: .NET 9.0

▶️  Finding build artifacts
ℹ️  Looking for build artifacts for commit 20c776f...
✅ Found build ID: 155100

▶️  Downloading artifacts
ℹ️  Downloading artifacts (this may take a moment)...
✅ Downloaded artifacts
ℹ️  Extracting artifacts...

▶️  Extracting package information
✅ Found package version: 10.0.0-ci.pr.12345.20

▶️  Configuring NuGet sources
✅ NuGet.config configured with local package source

▶️  Updating package reference
✅ Updated Microsoft.Maui.Controls to version 10.0.0-ci.pr.12345.20

╔═══════════════════════════════════════════════════════════╗
║                                                           ║
║        ✅ Successfully applied PR #33002!                  ║
║                                                           ║
╚═══════════════════════════════════════════════════════════╝
```

## 🤖 Bot Integration Example

These scripts are designed to be referenced in automated PR comments:

```markdown
🚀 **Test this PR build**

Run one of these commands in your MAUI project directory:

**PowerShell (Windows/macOS/Linux):**
```powershell
iwr https://raw.githubusercontent.com/dotnet/maui/main/scripts/Apply-MauiPR.ps1 -UseBasicParsing | iex; Apply-MauiPR -PrNumber 12345
```

**Bash (macOS/Linux):**
```bash
curl -fsSL https://raw.githubusercontent.com/dotnet/maui/main/scripts/apply-maui-pr.sh | bash -s 12345
```
```

## ⚠️ Important Notes

1. **Testing Only**: PR builds are for testing purposes only and should not be used in production
2. **Confirmation Required**: The script will ask for confirmation before applying changes
3. **Backup Projects**: Consider committing your changes or creating a backup before applying PR builds
4. **Revert When Done**: The script provides instructions for reverting to production versions
5. **Build Status**: The script will warn you if the PR build failed but allows you to continue
6. **Framework Versions**: PR builds typically target the latest .NET version (currently .NET 10)
7. **Report Feedback**: Please report your testing results on the PR page

## 🔧 Troubleshooting

### "No completed build found"
- The PR may not have triggered a build yet
- The build may still be in progress
- The build may have failed (check the PR's checks tab)

### "Could not find NuGet packages"
- The artifact structure may have changed
- The build may not have produced packages (some builds skip packaging)

### "Command not found" (Bash script)
- Install missing dependencies (jq, curl, unzip)
- Ensure the script has execute permissions: `chmod +x apply-maui-pr.sh`

### Package restore fails
- Run `dotnet nuget locals all --clear` to clear package caches
- Verify the `NuGet.config` was created correctly
- Check that the downloaded packages are in the temp directory

## 📚 Additional Resources

- [Testing PR Builds Wiki](https://github.com/dotnet/maui/wiki/Testing-PR-Builds)
- [MAUI Nightly Builds](https://github.com/dotnet/maui/wiki/Nightly-Builds)
- [.NET MAUI Documentation](https://docs.microsoft.com/dotnet/maui/)

## 🙏 Contributing

These scripts are part of the MauiVersion CLI tool project. To contribute or report issues, visit:
https://github.com/joverslu/maui-cli-nuget
