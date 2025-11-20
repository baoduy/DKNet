# Contributing to DKNet

Thank you for your interest in contributing to DKNet! This document provides guidelines and information for contributors.

## Table of Contents

- [Code of Conduct](#code-of-conduct)
- [Getting Started](#getting-started)
- [Development Setup](#development-setup)
- [Building the Project](#building-the-project)
- [Running Tests](#running-tests)
- [Coding Standards](#coding-standards)
- [Pull Request Process](#pull-request-process)
- [Release Process](#release-process)
- [Documentation Guidelines](#documentation-guidelines)

## Code of Conduct

This project adheres to the [Microsoft Open Source Code of Conduct](https://opensource.microsoft.com/codeofconduct/). By participating, you are expected to uphold this code.

## Getting Started

### Prerequisites

- .NET 10.0 SDK or later
- Git
- Visual Studio 2022 (17.13+), Visual Studio Code, or JetBrains Rider (recommended)
- SQL Server or SQL Server LocalDB (for integration tests)

### Fork and Clone

1. Fork the repository on GitHub
2. Clone your fork locally:
   ```bash
   git clone https://github.com/YOUR_USERNAME/DKNet.git
   cd DKNet
   ```
3. Add the upstream remote:
   ```bash
   git remote add upstream https://github.com/baoduy/DKNet.git
   ```

## Development Setup

### Initial Setup

1. **Install .NET 10.0 SDK**:
   ```bash
   # Use the provided script
   ./src/dotnet-install.sh --version 10.0.100
   ```

2. **Restore NuGet packages**:
   ```bash
   cd src
   dotnet restore DKNet.FW.sln
   ```

3. **Verify setup**:
   ```bash
   dotnet build DKNet.FW.sln
   ```

### IDE Configuration

#### Visual Studio 2022
- Install version 17.13 or later with .NET 10.0 support
- Recommended extensions:
  - CodeMaid (code cleanup)
  - SonarLint (code quality)
  - Meziantou.Analyzer (already included in projects)

#### Visual Studio Code
- Install the C# extension (with .NET 10 SDK support)
- Install the .NET Core Test Explorer extension
- Configure EditorConfig support

#### JetBrains Rider
- Use the latest version with .NET 10.0 support
- Enable EditorConfig support
- Configure code style according to `.editorconfig`

## Building the Project

### Full Solution Build

```bash
cd src
dotnet build DKNet.FW.sln --configuration Release
```

### Individual Package Build

```bash
cd src/Core/DKNet.Fw.Extensions
dotnet build --configuration Release
```

### Build Configurations

- **Debug**: Default development configuration with full debugging symbols
- **Release**: Optimized build for production use

## Running Tests

### All Tests

```bash
cd src
dotnet test DKNet.FW.sln --configuration Release
```

### Specific Test Project

```bash
cd src/Core/Fw.Extensions.Tests
dotnet test --configuration Release
```

### Test Categories

- **Unit Tests**: Fast, isolated tests for individual components
- **Integration Tests**: Tests that verify component interactions
- **End-to-End Tests**: Full application flow tests (in Templates)

### Test Requirements

- All public APIs must have corresponding unit tests
- Integration tests must use TestContainers or Aspire hosting when possible
- Use Shouldly for all assertions
- Tests must be deterministic and isolated

## Coding Standards

### General Guidelines

Follow the [DKNet Coding Guidelines](.github/copilot-instructions.md) and Microsoft .NET guidelines:

- Use C# 14.0 language features appropriately
- Follow `.editorconfig` rules
- Write self-documenting code with meaningful names
- Add XML documentation for all public APIs

### Code Style

```csharp
// ✅ Good
public class ProductService : IProductService
{
    private readonly IProductRepository _productRepository;
    private readonly ILogger<ProductService> _logger;

    public ProductService(IProductRepository productRepository, ILogger<ProductService> logger)
    {
        _productRepository = productRepository;
        _logger = logger;
    }

    /// <summary>
    /// Creates a new product with the specified details.
    /// </summary>
    /// <param name="name">The product name.</param>
    /// <param name="price">The product price.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The created product.</returns>
    public async Task<Product> CreateProductAsync(string name, decimal price, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(name);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(price);

        var product = new Product(name, price);
        await _productRepository.AddAsync(product, cancellationToken);
        
        _logger.LogInformation("Created product {ProductId} with name {Name}", product.Id, name);
        
        return product;
    }
}
```

### Naming Conventions

- **Classes/Interfaces**: PascalCase (e.g., `ProductService`, `IProductRepository`)
- **Methods/Properties**: PascalCase (e.g., `CreateProduct`, `TotalAmount`)
- **Fields**: `_camelCase` for private fields (e.g., `_productRepository`)
- **Parameters/Locals**: camelCase (e.g., `productName`, `totalAmount`)
- **Constants**: PascalCase (e.g., `MaxRetryCount`)

### Package Guidelines

- Use centralized package version management in `Directory.Packages.props`
- Do not specify versions in individual `.csproj` files
- Prefer stable package versions over pre-release
- Document breaking changes in CHANGELOG.md

## Pull Request Process

### Before Submitting

1. **Ensure your fork is up-to-date**:
   ```bash
   git fetch upstream
   git checkout main
   git merge upstream/main
   ```

2. **Create a feature branch**:
   ```bash
   git checkout -b feature/your-feature-name
   ```

3. **Make your changes following coding standards**

4. **Add/update tests for your changes**

5. **Update documentation**:
   - Update README.md if adding new features
   - Update CHANGELOG.md for your package
   - Add XML documentation for public APIs

6. **Build and test locally**:
   ```bash
   dotnet build DKNet.FW.sln --configuration Release
   dotnet test DKNet.FW.sln --configuration Release
   ```

### Pull Request Requirements

- **Description**: Clear description of what the PR does and why
- **Tests**: All new code must have appropriate test coverage
- **Documentation**: Updated documentation for any API changes
- **Changelog**: Updated CHANGELOG.md for affected packages
- **Breaking Changes**: Clearly marked and documented if any
- **Small Scope**: Keep PRs focused and reasonably sized

### PR Template

```markdown
## Description
Brief description of changes

## Type of Change
- [ ] Bug fix (non-breaking change which fixes an issue)
- [ ] New feature (non-breaking change which adds functionality)
- [ ] Breaking change (fix or feature that would cause existing functionality to not work as expected)
- [ ] Documentation update

## Testing
- [ ] Unit tests added/updated
- [ ] Integration tests added/updated
- [ ] Manual testing completed

## Documentation
- [ ] README.md updated
- [ ] CHANGELOG.md updated
- [ ] XML documentation added for public APIs

## Checklist
- [ ] Code follows project style guidelines
- [ ] Self-review completed
- [ ] Tests pass locally
- [ ] Documentation is up-to-date
```

## Release Process

### Version Strategy

DKNet follows [Semantic Versioning](https://semver.org/):

- **MAJOR**: Breaking changes
- **MINOR**: New features (backward compatible)
- **PATCH**: Bug fixes (backward compatible)

### Release Steps

1. **Update Version Numbers**:
   - Update `Directory.Packages.props` with new versions
   - Update CHANGELOG.md files for affected packages

2. **Create Release Branch**:
   ```bash
   git checkout -b release/v1.2.0
   ```

3. **Final Testing**:
   ```bash
   dotnet build DKNet.FW.sln --configuration Release
   dotnet test DKNet.FW.sln --configuration Release
   ```

4. **Create Release**:
   - Create GitHub release with tag
   - Include release notes from CHANGELOG.md
   - Publish to NuGet (automated via GitHub Actions)

### Hotfix Process

For critical bugs requiring immediate release:

1. **Create Hotfix Branch** from main:
   ```bash
   git checkout -b hotfix/v1.1.1
   ```

2. **Apply Minimal Fix**

3. **Test Thoroughly**

4. **Release Following Standard Process**

## Documentation Guidelines

### README Structure

Each package should have a README.md with:

- **Title and Badges**: Package name, NuGet badges, license
- **Description**: What the package does
- **Features**: Key capabilities
- **Installation**: NuGet installation instructions
- **Quick Start**: Basic usage examples
- **Configuration**: Setup and configuration options
- **API Reference**: Key classes and methods
- **Advanced Usage**: Complex scenarios
- **Contributing**: Link to this file
- **License**: License information

### Code Documentation

- **XML Documentation**: All public APIs must have XML docs
- **Examples**: Include code examples in documentation
- **Thread Safety**: Document thread safety characteristics
- **Performance**: Note any performance considerations

### CHANGELOG Format

Follow [Keep a Changelog](https://keepachangelog.com/en/1.0.0/) format:

```markdown
## [1.2.0] - 2024-01-15

### Added
- New feature description

### Changed
- Changed behavior description

### Deprecated
- Feature marked for removal

### Removed
- Removed feature description

### Fixed
- Bug fix description

### Security
- Security fix description
```

## Questions and Support

- **General Questions**: Open a GitHub Discussion
- **Bug Reports**: Create a GitHub Issue with reproduction steps
- **Feature Requests**: Open a GitHub Issue with detailed requirements
- **Security Issues**: Email security@drunkcoding.net

## Recognition

Contributors will be recognized in:
- GitHub contributors list
- Release notes for significant contributions
- Special recognition for first-time contributors

Thank you for contributing to DKNet! 🚀