# User Rules for .NET Developers

## 1. General Coding Practices
- Write clean, readable, and maintainable code.
- Use meaningful names for variables, methods, and classes.
- Keep methods short and focused on a single responsibility.
- Avoid code duplication; use reusable methods and classes.
- Comment only where necessary; prefer self-explanatory code.

## 2. .NET Conventions
- Follow the official .NET naming conventions:
  - **PascalCase** for public members, types, and namespaces.
  - **camelCase** for local variables and parameters.
  - Prefix interfaces with 'I' (e.g., `IRepository`).
- Use XML documentation comments for all public APIs.
- Prefer `var` when the type is obvious from the right side of the assignment; otherwise, use explicit types.

## 3. Error Handling
- Use exceptions for exceptional cases, not for control flow.
- Catch only specific exceptions you can handle.
- Always clean up resources with `using` statements or `try/finally`.
- Never swallow exceptions silently.

## 4. Asynchronous Programming
- Use `async` and `await` for asynchronous operations.
- Avoid blocking calls in asynchronous code (e.g., `.Result`, `.Wait()`).

## 5. Dependency Management
- Use dependency injection for managing dependencies.
- Avoid static classes for shared state.
- Register services with the appropriate lifetime (Transient, Scoped, Singleton).

## 6. Testing
- Write unit tests for all new code.
- Use xUnit, NUnit, or MSTest as the test framework.
- Ensure tests are isolated and repeatable.
- Aim for at least 80% code coverage.
- Use mocking frameworks to isolate dependencies.

## 7. Source Control
- Commit code frequently with clear, descriptive messages.
- Use feature branches for new work.
- Review and test code before merging to main branches.

## 8. Security
- Never hard-code secrets or credentials in source code.
- Use secure storage for sensitive information (e.g., Azure Key Vault, user secrets).
- Validate all user input to prevent injection attacks.

## 9. Documentation
- Maintain up-to-date README files for each project.
- Document setup, build, and deployment instructions.
- Provide usage examples for APIs and libraries.

## 10. Build & CI/CD
- Ensure the solution builds without errors or warnings.
- All tests must pass before merging changes.
- Use automated CI/CD pipelines for build, test, and deployment.

---

By following these user rules, you will help ensure code quality, maintainability, and security across your .NET projects.