# Contributing to MAUI Version Manager

Thank you for your interest in contributing to MAUI Version Manager! 🎉

## Ways to Contribute

- 🐛 Report bugs
- 💡 Suggest new features
- 📝 Improve documentation
- 🔧 Submit pull requests

## Development Setup

### Prerequisites

- [.NET 8.0 SDK](https://dotnet.microsoft.com/download) or later
- A .NET MAUI project for testing (or use the included test project)
- Git

### Getting Started

1. **Fork and Clone**

```bash
git fork https://github.com/jfversluis/maui-version.git
git clone https://github.com/jfversluis/maui-version.git
cd maui-version
```

2. **Restore Dependencies**

```bash
dotnet restore
```

3. **Build the Project**

```bash
dotnet build
```

4. **Run Tests**

```bash
dotnet test
```

5. **Install Locally for Testing**

```bash
# Create package
dotnet pack src/MauiVersion/MauiVersion.csproj -o ./nupkg

# Uninstall any existing version
dotnet tool uninstall -g maui-version

# Install your local version
dotnet tool install -g version-maui --add-source ./nupkg --version 1.0.0
```

6. **Test Your Changes**

```bash
cd test-projects/TestMauiApp
maui-version apply
```

## Project Structure

```
maui-version/
├── .github/
│   └── workflows/        # GitHub Actions CI/CD
│       ├── ci.yml        # Continuous integration
│       ├── release.yml   # Release automation
│       └── README.md     # Workflow documentation
├── src/
│   └── MauiVersion/      # Main CLI project
│       ├── Commands/     # Command implementations
│       ├── Models/       # Data models
│       ├── Services/     # Business logic services
│       └── Program.cs    # Entry point
├── tests/
│   └── MauiVersion.Tests/  # Unit and integration tests
├── test-projects/
│   └── TestMauiApp/      # Sample MAUI project for testing
└── README.md
```

## Making Changes

### 1. Create a Branch

```bash
git checkout -b feature/my-new-feature
# or
git checkout -b fix/bug-description
```

### 2. Code Style

- Follow C# coding conventions
- Use meaningful variable and method names
- Add XML documentation comments for public APIs
- Keep methods focused and concise

### 3. Write Tests

- Add unit tests for new functionality
- Ensure all tests pass before submitting PR
- Aim for good code coverage

```bash
# Run tests
dotnet test

# Run with coverage (if configured)
dotnet test /p:CollectCoverage=true
```

### 4. Commit Messages

Use clear, descriptive commit messages:

```
feat: Add support for preview releases
fix: Handle missing NuGet.config correctly
docs: Update README with new examples
test: Add integration tests for PR builds
```

Prefixes:
- `feat:` - New feature
- `fix:` - Bug fix
- `docs:` - Documentation changes
- `test:` - Test additions/changes
- `refactor:` - Code refactoring
- `chore:` - Maintenance tasks

### 5. Submit Pull Request

1. Push your branch:
```bash
git push origin feature/my-new-feature
```

2. Create a Pull Request on GitHub
3. Fill out the PR template
4. Wait for CI checks to pass
5. Respond to review feedback

## Testing

### Running All Tests

```bash
dotnet test
```

### Running Specific Tests

```bash
# Run specific test class
dotnet test --filter "FullyQualifiedName~IntegrationTests"

# Run specific test method
dotnet test --filter "FullyQualifiedName~IntegrationTests.Cli_ShowsHelp"
```

### Manual Testing Checklist

Before submitting a PR, manually test:

- [ ] `maui-version --help` shows help
- [ ] `maui-version --version` shows version
- [ ] `maui-version apply` in interactive mode works
- [ ] `maui-version apply --channel stable` updates to stable
- [ ] `maui-version apply --channel nightly` updates to nightly
- [ ] `maui-version apply --apply-pr 12345` handles PR builds
- [ ] Error messages are clear and helpful
- [ ] Works on your operating system

## CI/CD

### Continuous Integration

Every push and PR triggers CI builds that:
- Build on Ubuntu, Windows, and macOS
- Test against multiple .NET versions
- Upload test results and artifacts

### Release Process

Releases are automated via GitHub Actions:

1. Update version in `src/MauiVersion/MauiVersion.csproj` if needed
2. Create and push a tag:
```bash
git tag v1.0.1
git push origin v1.0.1
```
3. GitHub Actions will:
   - Build and test
   - Create NuGet package
   - Publish to NuGet.org (maintainers only)
   - Create GitHub Release

## Reporting Issues

### Bug Reports

Include:
- **Description**: Clear description of the bug
- **Steps to Reproduce**: Detailed steps to reproduce the issue
- **Expected Behavior**: What you expected to happen
- **Actual Behavior**: What actually happened
- **Environment**:
  - OS: Windows/macOS/Linux
  - .NET Version: `dotnet --version`
  - Tool Version: `maui-version --version`
- **Logs**: Any error messages or stack traces

### Feature Requests

Include:
- **Use Case**: Why is this feature needed?
- **Proposed Solution**: How should it work?
- **Alternatives**: What alternatives have you considered?

## Code of Conduct

- Be respectful and inclusive
- Focus on constructive feedback
- Help create a welcoming environment
- Follow GitHub's Community Guidelines

## Questions?

- Open a [Discussion](https://github.com/jfversluis/maui-version/discussions)
- Check existing [Issues](https://github.com/jfversluis/maui-version/issues)
- Review the [README](README.md)

## License

By contributing, you agree that your contributions will be licensed under the MIT License.

---

Thank you for contributing! 🙏
