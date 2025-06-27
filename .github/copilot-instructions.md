# DKNet – GitHub Copilot Instructions

This document provides guidance for GitHub Copilot when generating code for the DKNet project. Follow these guidelines to ensure that generated code aligns with the project's coding standards, architecture, and best practices.

If you are not sure, do not guess—ask clarifying questions or state that you don't know. Do not copy code that only follows a pattern from a different context. Do not rely solely on names; always evaluate the intent and logic.

---

## Code Style

### General Guidelines

- Follow the language/platform's standard coding guidelines (e.g., for .NET, see [Microsoft .NET Coding Guidelines](https://github.com/dotnet/runtime/blob/main/docs/coding-guidelines/coding-style.md); for other languages, use their official style guides).
- Adhere to rules defined in the `.editorconfig` or equivalent configuration files.
- Write code that is clean, maintainable, and easy to understand.
- Favor readability over brevity. Keep methods focused and concise.
- Add comments only when necessary to explain non-obvious solutions; otherwise, code should be self-explanatory.
- Add the appropriate license header to all files, if applicable.
- Do not add UTF-8 BOM unless required for non-ASCII files.
- Avoid breaking public APIs. If you must, mark the old API as obsolete and provide a migration path.

### Formatting

- Use spaces for indentation (4 spaces unless otherwise specified).
- Use braces for all code blocks, including single-line blocks.
- Place braces on new lines.
- Limit line length to 140 characters.
- Trim trailing whitespace.
- Begin all declarations on a new line.
- Use a single blank line to separate logical code sections.
- Insert a final newline at the end of each file.

### Language-Specific Guidelines

- Use file-scoped namespace declarations (for C#).
- Use `var` for local variables (for C# or similar conventions in other languages).
- Use expression-bodied members where appropriate.
- Prefer modern language features (e.g., pattern matching, range/index operators) when available and beneficial.
- Prefer concise property and method declarations.
- Avoid redundant using/import statements.

### Naming Conventions

- Use PascalCase for: Classes, structs, enums, properties, methods, events, namespaces, delegates, public fields, static private fields, constants.
- Use camelCase for: Parameters, local variables.
- Use `_camelCase` for private instance fields.
- Prefix interfaces with `I`.
- Prefix type parameters with `T`.
- Use meaningful and descriptive names.

### Nullability

- Use nullable reference types where supported.
- Use proper null-checking patterns.
- Use null-conditional (`?.`) and null-coalescing (`??`) operators when appropriate.

---

## Architecture and Design Patterns

- Favor dependency injection for services or components that may need to be replaced, mocked, or extended.
- Structure the codebase for modularity and separation of concerns.
- Use records/DTOs for immutable data transfer where appropriate.
- For internal APIs or infrastructure code, clearly document their intended limited use.

---

## Testing

- Follow existing test patterns and conventions.
- Write both unit and integration tests where appropriate.
- Ensure tests are isolated and reproducible.
- Use mocks or fakes for external dependencies.
- Keep test methods focused and descriptive.

---

## Documentation

- Include XML/Docstring/documentation comments for all public APIs.
- Use `<inheritdoc />` where appropriate for overriding documentation.
- Add code examples in documentation when helpful.
- For key concepts or non-trivial logic, link to relevant external docs or project wiki.

---

## Error Handling

- Use appropriate exception types.
- Provide helpful error messages.
- Avoid catching exceptions without rethrowing or handling them.
- Log errors where relevant, but avoid exposing sensitive data.

---

## Asynchronous Programming

- Provide both synchronous and asynchronous methods where appropriate.
- Use the `Async` suffix for asynchronous methods.
- Return `Task`/`ValueTask`/Promise/etc. from async methods.
- Support cancellation tokens or equivalents.
- Avoid `async void` methods except for event handlers.

---

## Performance Considerations

- Be mindful of performance, especially in I/O, networking, and database operations.
- Avoid unnecessary allocations and expensive operations in performance-critical code.
- Optimize hot paths, but not at the expense of clarity elsewhere.

---

## Implementation Guidelines

- Write secure code by default; avoid exposing sensitive data.
- Make the code compatible with relevant deployment targets (e.g., AOT, cloud, cross-platform).
- Avoid dynamic code generation/reflection unless required and document such usage.

---

## Repository Structure

- `src/`: Main product source code.
- `test/`: All test projects, including unit and integration tests.
- `docs/`: Documentation files for contributors and users.
- `.github/`: GitHub-specific files, workflows, and Copilot instructions.
- `tools/`: Utility scripts and developer resources.
- `eng/` or equivalent: Build/test infrastructure files.
- Add or adapt sections as needed for your repo.

---

## DKNet Overview

_Describe the project's core architecture and concepts here. Example:_

DKNet is a [brief description of the project's purpose and domain]. Its core components include:

### Main Concepts

- **Main API/Entry Point**: [Describe main class/module/function]
- **Configuration**: [How is configuration managed?]
- **Core Workflow**: [How does data flow through the main components?]
- **Extensibility Points**: [How can users extend/customize the library?]
- **Supported Platforms/Frameworks**: [.NET, Python, etc.—list as relevant]

---

## Additional DKNet-Specific Guidelines

- [Add any project-specific best practices, patterns, or caveats here.]
- [For example: Use project-specific logging, adhere to security or compliance requirements, follow specific database/provider patterns, etc.]

---

_Keep this document up-to-date as the project and its conventions evolve._