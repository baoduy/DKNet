# WX.Framework.Extensions

## Overview

**WX.Framework.Extensions** is a library designed to extend and enhance functionality within the WX Framework. It provides additional tools, utilities, and helpers to simplify development and improve productivity. By integrating this extension library, developers can leverage pre-built features and abstractions, reducing boilerplate code and accelerating project timelines.

## Features

### Core Classes

1. **AsyncEnumerableExtensions**  
   Provides extension methods for working with asynchronous enumerable collections, making it easier to process data streams asynchronously with operations like filtering, mapping, and aggregation.

2. **AttributeExtensions**  
   Enhances functionality related to attributes by providing methods to retrieve, manipulate, and check for the presence of custom or built-in attributes in a streamlined way.

3. **DateTimeExtensions**  
   Offers utility methods for `DateTime` manipulation, including methods for calculating differences, adding/subtracting time spans, and formatting dates for various locales.

4. **EnumExtensions**  
   Adds helper methods for working with enumerations, such as parsing strings to enums, retrieving display names, and validating enum values against defined sets.

5. **EnumInfo**  
   Represents detailed information about enumeration values, including their names, integer values, and associated metadata like custom attributes or descriptions.

6. **PropertyAttributeInfo**  
   Provides metadata about properties and their associated attributes, enabling detailed analysis and reflection-based processing in applications.

7. **PropertyExtensions**  
   Simplifies access and manipulation of property information using reflection, including methods to get or set property values dynamically.

8. **ServiceCollectionExtensions**  
   Includes utilities for dependency injection, allowing developers to easily register common services, configure options, and apply conventions within the service container.

9. **StringExtensions**  
   Adds various string manipulation methods, such as trimming, case conversions, substring extraction, and enhanced string validation checks.

10. **TypeExtensions**  
    Contains helpers for working with types in .NET, providing methods for retrieving type information, analyzing inheritance, and working with generic types.

### Encryption

11. **Hashing**  
    Implements hashing algorithms for secure data processing, including support for commonly used algorithms like SHA256 and MD5, with options for salt generation.

12. **StringEncryption**  
    Provides methods for encrypting and decrypting strings using symmetric encryption algorithms, making it simple to secure sensitive data.

### TypeExtractor

13. **ITypeExtractor**  
    Interface defining methods for extracting type information from assemblies, providing a foundation for analyzing and working with metadata.

14. **TypeArrayExtractorExtensions**  
    Adds functionality for extracting specific type arrays from assemblies, allowing for quick filtering and categorization of types based on criteria.

15. **TypeCollectionExtractorExtensions**  
    Extends capabilities for extracting type collections, making it easier to work with grouped metadata from large projects or external libraries.

16. **TypeExtractor**  
    Core implementation for extracting type metadata, supporting features like assembly scanning, type filtering, and attribute-based selection.

## Requirements

- **Framework**: WX Framework vX.X or higher
- **Language**: .NET 9.0 or higher
- **Dependencies**: Refer to `WX.Framework.Extensions.csproj` for detailed dependency information.

## Installation

To install the WX.Framework.Extensions package, use the NuGet Package Manager:

```bash
Install-Package WX.Framework.Extensions
```

Alternatively, add the package to your project file:

```xml
<PackageReference Include="WX.Framework.Extensions" Version="X.X.X" />
```

Then, restore your dependencies:

```bash
dotnet restore
```

## Issues and Support

For issues, please submit a bug report or feature request via the [GitHub Issues](https://github.com/YourRepo/WX.Framework.Extensions/issues) page. Make sure to include:

- A clear description of the problem.
- Steps to reproduce (if applicable).
- Any error messages or logs.

## License

This project is licensed under the MIT License. See the LICENSE file for details.

## Acknowledgments

Special thanks to all contributors and maintainers of WX.Framework. This extension library builds upon their solid foundation.
