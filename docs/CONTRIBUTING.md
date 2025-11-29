# Contributing to SheetLink

Thank you for considering contributing to SheetLink! This document provides guidelines and instructions for contributing to the project.

## Table of Contents

- [Code of Conduct](#code-of-conduct)
- [How Can I Contribute?](#how-can-i-contribute)
- [Development Setup](#development-setup)
- [Coding Standards](#coding-standards)
- [Pull Request Process](#pull-request-process)
- [Testing Requirements](#testing-requirements)
- [Documentation](#documentation)
- [Community](#community)

## Code of Conduct

### Our Pledge

We are committed to providing a welcoming and inclusive environment for all contributors, regardless of experience level, gender identity, sexual orientation, disability, personal appearance, race, ethnicity, age, religion, or nationality.

### Expected Behavior

- Be respectful and considerate in all interactions
- Provide constructive feedback
- Focus on what is best for the project and community
- Show empathy towards other community members
- Accept constructive criticism gracefully

### Unacceptable Behavior

- Harassment, trolling, or discriminatory comments
- Personal attacks or insults
- Publishing others' private information
- Any conduct that could reasonably be considered inappropriate

## How Can I Contribute?

### Reporting Bugs

Before creating a bug report, please:

1. **Check existing issues** - Your bug may already be reported
2. **Verify the bug** - Test with the latest version
3. **Gather information** - Collect details about your environment

**Creating a Bug Report**:

```markdown
**Description**: A clear description of the bug

**Steps to Reproduce**:
1. Go to '...'
2. Click on '...'
3. See error

**Expected Behavior**: What should happen

**Actual Behavior**: What actually happens

**Environment**:
- OS: [e.g., Windows 11, Ubuntu 24.04]
- Browser: [e.g., Chrome 120, Firefox 121]
- .NET Version: [e.g., .NET 10]

**Screenshots**: If applicable

**Additional Context**: Any other relevant information
```

### Suggesting Enhancements

Enhancement suggestions are tracked as GitHub issues. When creating an enhancement suggestion:

1. **Use a clear title** - Describe the enhancement concisely
2. **Provide detailed description** - Explain the enhancement and why it's useful
3. **Include examples** - Show how the enhancement would be used
4. **Consider alternatives** - Mention alternative solutions you've considered

**Example**:

```markdown
**Enhancement**: Add CSV export option

**Description**: Allow users to export extracted links as CSV in addition to Excel

**Use Case**: Users who want to import links into databases or other tools

**Implementation Ideas**: Add a "Download as CSV" button next to the existing download

**Alternatives Considered**: JSON export, but CSV is more universally supported
```

### Contributing Code

We welcome code contributions! Here's how to get started:

1. **Fork the repository**
2. **Create a feature branch** - `git checkout -b feature/your-feature-name`
3. **Make your changes** - Follow coding standards
4. **Write tests** - Ensure your changes are tested
5. **Update documentation** - Keep docs in sync with code
6. **Submit a pull request** - Follow the PR template

## Development Setup

### Prerequisites

- **.NET 10 SDK** - [Download](https://dotnet.microsoft.com/download/dotnet/10.0)
- **Git** - Version control
- **Code Editor** - Visual Studio 2022, VS Code, or Rider

### Clone and Build

```bash
# Clone your fork
git clone https://github.com/YOUR_USERNAME/sheetlink.git
cd sheetlink

# Restore dependencies
dotnet restore

# Build the project
dotnet build

# Run tests
dotnet test

# Run the application
cd ExcelLinkExtractorWeb
dotnet run
```

The application will be available at `http://localhost:5050`

### Project Structure

```
ExcelLinkExtractor/
├── ExcelLinkExtractorWeb/              # Main web application
│   ├── Components/
│   │   ├── Pages/                      # Blazor pages
│   │   └── Layout/                     # Layout components
│   ├── Services/                       # Business logic
│   │   ├── ILinkExtractorService.cs    # Service interface
│   │   └── LinkExtractorService.cs     # Core service implementation
│   ├── Configuration/                  # Configuration classes
│   └── wwwroot/                        # Static files
├── ExcelLinkExtractor.Tests/           # Unit tests
├── ARCHITECTURE.md                     # Architecture documentation
├── SECURITY.md                         # Security policy
└── CONFIGURATION.md                    # Configuration guide
```

### Configuration

Development configuration is in `appsettings.json`:

```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Information"
    }
  },
  "AllowedHosts": "localhost",
  "ExcelProcessing": {
    "MaxFileSizeMB": 10,
    "MaxHeaderSearchRows": 10,
    "MaxUrlLength": 2000,
    "RateLimitPerMinute": 1000
  }
}
```

## Coding Standards

### C# Style Guidelines

We follow [Microsoft's C# Coding Conventions](https://learn.microsoft.com/en-us/dotnet/csharp/fundamentals/coding-style/coding-conventions).

**Key Points**:

- **Naming**:
  - PascalCase for public members, classes, methods
  - camelCase for private fields, local variables
  - Prefix private fields with `_` (e.g., `_logger`)
  - Interfaces start with `I` (e.g., `ILinkExtractorService`)

- **Formatting**:
  - Use 4 spaces for indentation (not tabs)
  - Place opening braces on new lines
  - One statement per line
  - Include a space after keywords (e.g., `if (condition)`)

- **Code Organization**:
  - One class per file
  - Group members by access level (public → protected → private)
  - Group by type (fields → properties → constructors → methods)

**Example**:

```csharp
public class LinkExtractorService : ILinkExtractorService
{
    private readonly ILogger<LinkExtractorService> _logger;
    private readonly ExcelProcessingOptions _options;

    public LinkExtractorService(
        ILogger<LinkExtractorService> logger,
        IOptions<ExcelProcessingOptions> options)
    {
        _logger = logger;
        _options = options.Value;
    }

    public async Task<ExtractionResult> ExtractLinksAsync(Stream fileStream, string linkColumnName)
    {
        // Implementation
    }
}
```

### Documentation Standards

- **XML Documentation**: All public APIs must have XML documentation

```csharp
/// <summary>
/// Extracts hyperlinks from an Excel file and returns them as a new Excel file.
/// </summary>
/// <param name="fileStream">The Excel file stream to process</param>
/// <param name="linkColumnName">The name of the column containing hyperlinks</param>
/// <returns>An ExtractionResult containing the output file and metadata</returns>
/// <exception cref="InvalidFileFormatException">Thrown when the file is not a valid Excel file</exception>
public async Task<ExtractionResult> ExtractLinksAsync(Stream fileStream, string linkColumnName)
```

- **Inline Comments**: Use sparingly, prefer self-documenting code

```csharp
// Good: Code explains itself
var maxFileSize = _options.MaxFileSizeBytes;
if (fileStream.Length > maxFileSize)
{
    throw new InvalidFileFormatException($"File size exceeds maximum of {_options.MaxFileSizeMB}MB");
}

// Bad: Unnecessary comment
// Check if file is too big
if (fileStream.Length > maxFileSize) // File is too big
```

### Blazor Component Guidelines

- **Component Structure**: Follow this order:
  1. `@page` directive
  2. `@using` statements
  3. `@inject` statements
  4. `<PageTitle>` and `<HeadContent>`
  5. HTML markup
  6. `@code` block

- **State Management**: Use `PersistentComponentState` for data that should survive prerender/interactive transitions

- **Accessibility**: Always include proper ARIA labels, semantic HTML, and keyboard navigation

**Example**:

```razor
@page "/example"
@inject ILinkExtractorService ExtractorService

<PageTitle>Example - SheetLink</PageTitle>

<div class="container">
    <h1>Example Page</h1>
    <button @onclick="HandleClick" class="btn btn-primary">
        Click Me
    </button>
</div>

@code {
    private async Task HandleClick()
    {
        // Handle click
    }
}
```

## Pull Request Process

### Before Submitting

1. **Test your changes** - All tests must pass
2. **Update documentation** - Keep docs in sync with code changes
3. **Follow coding standards** - Consistent style with existing code
4. **Check for warnings** - Build should complete without warnings (nullable warnings OK)
5. **Update IMPROVEMENTS.md** - If implementing a planned improvement

### PR Title Format

Use conventional commit format:

- `feat:` - New feature
- `fix:` - Bug fix
- `docs:` - Documentation only
- `style:` - Formatting, missing semicolons, etc.
- `refactor:` - Code restructuring
- `test:` - Adding tests
- `chore:` - Maintenance tasks

**Examples**:
- `feat: Add CSV export option`
- `fix: Resolve null reference in link extraction`
- `docs: Update CONFIGURATION.md with new settings`
- `refactor: Extract shared upload component`

### PR Description Template

```markdown
## Description
Brief description of changes

## Type of Change
- [ ] Bug fix (non-breaking change which fixes an issue)
- [ ] New feature (non-breaking change which adds functionality)
- [ ] Breaking change (fix or feature that would cause existing functionality to not work as expected)
- [ ] Documentation update

## How Has This Been Tested?
Describe the tests you ran to verify your changes

## Checklist
- [ ] My code follows the style guidelines
- [ ] I have performed a self-review
- [ ] I have commented my code, particularly in hard-to-understand areas
- [ ] I have updated the documentation
- [ ] My changes generate no new warnings
- [ ] I have added tests that prove my fix is effective or that my feature works
- [ ] New and existing unit tests pass locally
- [ ] I have updated IMPROVEMENTS.md if applicable

## Screenshots (if applicable)
Add screenshots to help explain your changes
```

### Review Process

1. **Automated Checks** - CI builds and tests must pass
2. **Code Review** - At least one maintainer must approve
3. **Testing** - Reviewers may test your changes
4. **Merge** - Once approved, your PR will be merged

## Testing Requirements

### Unit Tests

All new features and bug fixes must include unit tests.

**Test Structure**:

```csharp
[Fact]
public async Task ExtractLinksAsync_Should_ReturnError_WhenFileIsEmpty()
{
    // Arrange
    var emptyStream = new MemoryStream();

    // Act
    var result = await _service.ExtractLinksAsync(emptyStream, "Title");

    // Assert
    result.Should().NotBeNull();
    result.ErrorMessage.Should().Contain("empty");
    result.OutputFile.Should().BeNull();
}
```

**Test Coverage**:

- **Happy Path** - Test normal, expected behavior
- **Edge Cases** - Empty inputs, boundary conditions
- **Error Handling** - Invalid inputs, exceptions
- **Integration** - Multiple components working together

### Running Tests

```bash
# Run all tests
dotnet test

# Run with coverage
dotnet test /p:CollectCoverage=true

# Run specific test
dotnet test --filter "FullyQualifiedName~ExtractLinksAsync_Should_ReturnError_WhenFileIsEmpty"
```

### Test Naming Convention

```
[MethodName]_Should_[ExpectedBehavior]_When[Condition]
```

**Examples**:
- `ExtractLinksAsync_Should_ReturnError_WhenFileIsEmpty`
- `ValidateExcelFile_Should_ThrowException_WhenFileTooLarge`
- `CreateTemplate_Should_ReturnValidExcelFile`

## Documentation

### What to Document

- **Public APIs** - XML documentation for all public methods and classes
- **Architecture Changes** - Update ARCHITECTURE.md for structural changes
- **Configuration** - Update CONFIGURATION.md for new settings
- **Security** - Update SECURITY.md for security-related changes
- **User Features** - Update README.md for user-facing features
- **FAQ** - Add common questions to FAQ.razor

### Documentation Checklist

When adding a new feature:

- [ ] XML documentation on public APIs
- [ ] README.md updated if user-facing
- [ ] ARCHITECTURE.md updated if structural changes
- [ ] CONFIGURATION.md updated if new settings
- [ ] FAQ.razor updated if common question
- [ ] Inline comments for complex logic

## Community

### Getting Help

- **GitHub Issues** - Ask questions, report bugs
- **GitHub Discussions** - General discussions, ideas
- **README.md** - Check documentation first

### Recognition

Contributors will be:
- Listed in release notes
- Credited in commits
- Thanked publicly (if desired)

### License

By contributing, you agree that your contributions will be licensed under the same license as the project (see LICENSE file).

## Additional Resources

- [Architecture Documentation](ARCHITECTURE.md) - System design and structure
- [Security Policy](SECURITY.md) - Security guidelines and reporting
- [Configuration Guide](CONFIGURATION.md) - Configuration options
- [ASP.NET Core Docs](https://learn.microsoft.com/en-us/aspnet/core/) - Framework documentation
- [Blazor Docs](https://learn.microsoft.com/en-us/aspnet/core/blazor/) - Blazor-specific guides

## Questions?

If you have questions about contributing, please:

1. Check this document
2. Search existing issues
3. Create a new issue with the `question` label

Thank you for contributing to SheetLink!
