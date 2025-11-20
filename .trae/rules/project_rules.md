# WIXO.FW Project Rules

## 1. Coding Standards
- Follow .NET 10 conventions for all code.
- Use **PascalCase** for class and method names, **camelCase** for local variables and parameters.
- Prefix interfaces with 'I' (e.g., `IService`).
- Provide XML documentation for all public members.
- Use `async`/`await` for asynchronous operations.
- Avoid empty catch blocks; catch specific exceptions.
- Use LINQ for collection and array manipulations.
- Adhere to SOLID principles.

## 2. Architectural Rules
- **Layered Architecture**:
  - Core layer must only depend on itself.
  - EfCore layer can depend on EfCore, Core, and Microsoft.EntityFrameworkCore.
  - Services layer must not depend on EfCore.
- **Namespace Containment**:
  - Types should reside in their designated namespaces according to their layer.
- **Dependency Injection**:
  - All dependencies must be injected via constructor or DI container.
- **No Circular Dependencies**:
  - Projects must not reference each other in a circular manner.

## 3. Testing & Coverage
- All new features must include unit or integration tests.
- Use xUnit or NUnit as the test framework.
- Code coverage must be collected using `coverlet.collector`.
- Target a minimum of 80% code coverage for all projects.
- Tests must be isolated and not depend on external state.

## 4. Documentation
- Each project must have a README with:
  - Purpose and scope
  - Build and usage instructions
  - License information

## 5. Licensing
- All code and templates must be licensed under MIT License.

## 6. Build & CI
- All projects must target .NET 10.0.
- The solution must build without errors or warnings.
- All tests must pass in CI before merging.

## 7. Templates & Patterns
- Follow DDD and CQRS patterns for business logic and API endpoints.
- Use MediatR for internal domain events.
- Use SlimBus with Azure Service Bus for external events.

---

By following these rules, your solution will remain maintainable, testable, and aligned with modern .NET best practices.