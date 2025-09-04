# DKNet – GitHub Copilot Instructions

This document provides guidance for GitHub Copilot when generating code for the DKNet project. Follow these guidelines
to ensure that generated code aligns with the project's coding standards, architecture, and best practices.

If you are not sure, do not guess—ask clarifying questions or state that you don't know. Do not copy code that only
follows a pattern from a different context. Do not rely solely on names; always evaluate the intent and logic.

---

## Code Style

### General Guidelines

- Follow the language/platform's standard coding guidelines (e.g., for .NET,
  see [Microsoft .NET Coding Guidelines](https://github.com/dotnet/runtime/blob/main/docs/coding-guidelines/coding-style.md);
  for other languages, use their official style guides).
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

- Use PascalCase for: Classes, structs, enums, properties, methods, events, namespaces, delegates, public fields, static
  private fields, constants.
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

## NuGet Package Management

- Use centralized package version management (e.g., `Directory.Packages.props` or equivalent).
- **Do not specify the `Version` attribute in individual project/package references.**
- When adding or updating NuGet package references, only include the package name; versioning is handled centrally.
- Ensure all new dependencies are added to the central configuration file as needed.

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
- **When writing unit tests, prefer using [TestContainer](https://testcontainers.com/)
  or [Aspire host](https://learn.microsoft.com/en-us/dotnet/aspire/) for orchestrating dependencies and infrastructure.
  **
- **Only fall back to dummy or fake object frameworks (such as Moq, NSubstitute, etc.) if TestContainer or Aspire host
  are not practical or applicable for the scenario.**
- Use mocks or fakes for external dependencies when containerized or Aspire-based approaches are not feasible.
- Keep test methods focused and descriptive.
- Try to use `Shouldly` for all assertions in tests.
- For Collection assertions, prefer using `ShouldBeEquivalentTo` for comparing collections.

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

DKNet is a comprehensive .NET framework providing extensions, templates, and tools for building modern, scalable applications using Domain-Driven Design (DDD) principles and CQRS patterns.

### Main Concepts

- **Main API/Entry Point**: SlimBus template provides a complete API template with minimal endpoints using ASP.NET Core
- **Configuration**: Centralized configuration through `appsettings.json`, dependency injection, and options pattern
- **Core Workflow**: Request → Endpoint → Command/Query Handler → Domain Logic → Repository → Database
- **Extensibility Points**: Custom validators, event handlers, mapping configurations, and domain services
- **Supported Platforms/Frameworks**: .NET 9.0+, ASP.NET Core, Entity Framework Core, FluentValidation

### Architecture Layers

- **API Layer**: Minimal API endpoints with versioning and documentation
- **Application Services**: Command/Query handlers, validation, and business orchestration  
- **Domain Layer**: Entities, value objects, domain events, and business rules
- **Infrastructure Layer**: Data access, external services, and cross-cutting concerns

### Key Technologies

- **SlimBus**: Lightweight message bus for CQRS implementation
- **Entity Framework Core**: ORM for data persistence
- **FluentValidation**: Input validation framework
- **Mapster**: Object-to-object mapping
- **Result Pattern**: Error handling without exceptions
- **Domain Events**: Decoupled business event handling

---

## SlimBus Template CRUD Conventions

When generating CRUD operations for the SlimBus template, follow these established patterns and conventions. All examples are based on the Profile feature implementation in `src/Templates/SlimBus.ApiEndpoints/`.

### File Organization Structure

Organize CRUD components using this feature-based structure:
```
SlimBus.Api/ApiEndpoints/{Feature}Endpoints.cs
SlimBus.AppServices/{Feature}/V1/
├── Actions/
│   ├── Create.cs
│   ├── Update.cs
│   └── Delete.cs
├── Queries/
│   ├── {Feature}Result.cs
│   ├── Page{Feature}sQueryHandler.cs
│   └── Single{Feature}QueryHandler.cs
└── Events/
    └── {Feature}CreatedEventHandlers.cs
SlimBus.Domains/Features/{Feature}/
├── Entities/{Feature}.cs
└── Repos/I{Feature}Repo.cs
SlimBus.Infra/Features/{Feature}/
├── Repos/{Feature}Repo.cs
└── Mappers/{Feature}Mapper.cs
```

**Schema Organization:**
- Define database schemas in `SlimBus.Domains/Share/DomainSchemas.cs`
- Use short, meaningful schema names (e.g., "pro" for Profile, "ord" for Orders)
- Group related entities under the same schema

### 1. API Endpoint Conventions

Create endpoint classes that implement `IEndpointConfig`:

```csharp
internal sealed class {Feature}V1Endpoint : IEndpointConfig
{
    public string GroupEndpoint => "/{features}"; // lowercase plural
    public int Version => 1;

    public void Map(RouteGroupBuilder group)
    {
        // Standard CRUD mappings
        group.MapGetPage<Page{Feature}PageQuery, {Feature}Result>("")
            .WithDescription("Get all {features}");
        group.MapGet<{Feature}Query, {Feature}Result?>("{id:guid}")
            .WithDescription("Get {feature} by id");
        group.MapPost<Create{Feature}Command, {Feature}Result>("")
            .AddIdempotencyFilter()
            .WithDescription("Create {feature}. <br/><br/> Note: Idempotency key is required in the header. <br/>" +
                             "X-Idempotency-Key: {IdempotencyKey} <br/>");
        group.MapPut<Update{Feature}Command, {Feature}Result>("{id:guid}")
            .WithDescription("Update {feature} by id");
        group.MapDelete<Delete{Feature}Command>("{id:guid}")
            .WithDescription("Delete {feature} by id");
    }
}
```

**Key Patterns:**
- Use lowercase plural endpoint paths
- Always include descriptive documentation
- Add idempotency filter for creation operations using `.AddIdempotencyFilter()`
- Use Guid route constraints for ID parameters (`{id:guid}`)
- Version endpoints with separate classes for each version
- Standard HTTP status codes are automatically configured via `.ProducesCommons()`
- Endpoints return appropriate HTTP status codes (200, 404, 400, 500, etc.)

### 2. Command/Action Conventions

#### Create Command Pattern

```csharp
[MapsTo(typeof({Feature}))]
public sealed record Create{Feature}Command : BaseCommand, Fluents.Requests.IWitResponse<{Feature}Result>
{
    [Required] public string RequiredProperty { get; set; } = null!;
    [Optional] public string? OptionalProperty { get; set; }
    
    [JsonIgnore]
    [Description("Property set by business logic, not user input")]
    public string SystemProperty { get; set; } = null!;
}

internal sealed class Create{Feature}CommandValidator : AbstractValidator<Create{Feature}Command>
{
    public Create{Feature}CommandValidator()
    {
        RuleFor(a => a.RequiredProperty).NotEmpty().Length(1, 150);
        RuleFor(a => a.OptionalProperty).Length(0, 100).When(x => x.OptionalProperty != null);
    }
}

internal sealed class Create{Feature}CommandHandler(
    I{Feature}Repo repository,
    IRequiredService requiredService,
    IMapper mapper)
    : Fluents.Requests.IHandler<Create{Feature}Command, {Feature}Result>
{
    public async Task<IResult<{Feature}Result>> OnHandle(Create{Feature}Command request,
        CancellationToken cancellationToken)
    {
        // Business logic validation
        if (await repository.IsDuplicateAsync(request.UniqueProperty))
            return Result.Fail<{Feature}Result>($"{request.UniqueProperty} already exists.");

        // Map and create entity
        var entity = mapper.Map<{Feature}>(request);
        
        // Add to repository
        await repository.AddAsync(entity, cancellationToken);

        // Add domain event
        entity.AddEvent(new {Feature}CreatedEvent(entity.Id, entity.Name));

        // Return lazy mapped result
        return mapper.ResultOf<{Feature}Result>(entity);
    }
}
```

#### Update Command Pattern

```csharp
[MapsTo(typeof({Feature}))]
public record Update{Feature}Command : BaseCommand, Fluents.Requests.IWitResponse<{Feature}Result>
{
    public required Guid Id { get; init; }
    public string? PropertyToUpdate { get; init; }
    // Only include properties that can be updated
}

internal sealed class Update{Feature}CommandHandler(
    IMapper mapper,
    I{Feature}Repo repo) : Fluents.Requests.IHandler<Update{Feature}Command, {Feature}Result>
{
    public async Task<IResult<{Feature}Result>> OnHandle(Update{Feature}Command request,
        CancellationToken cancellationToken)
    {
        if (request.Id == Guid.Empty)
            return Result.Fail<{Feature}Result>("The Id is invalid.");

        var entity = await repo.FindAsync(request.Id, cancellationToken);
        if (entity == null)
            return Result.Fail<{Feature}Result>($"The {Feature} {request.Id} is not found.");

        // Call domain method for updates
        entity.Update(request.PropertyToUpdate, request.ByUser!);

        // Add events if needed
        // entity.AddEvent(new {Feature}UpdatedEvent(entity.Id));

        return Result.Ok(mapper.Map<{Feature}Result>(entity));
    }
}
```

#### Delete Command Pattern

```csharp
public record Delete{Feature}Command : BaseCommand, Fluents.Requests.INoResponse
{
    public required Guid Id { get; init; }
}

internal sealed class Delete{Feature}CommandHandler(I{Feature}Repo repository)
    : Fluents.Requests.IHandler<Delete{Feature}Command>
{
    public async Task<IResultBase> OnHandle(Delete{Feature}Command request, CancellationToken cancellationToken)
    {
        if (request.Id == Guid.Empty)
            return Result.Fail("The Id is invalid.")
                .WithError(new Error("The Id is invalid.") { Metadata = { ["field"] = nameof(request.Id) } });

        var entity = await repository.FindAsync(request.Id, cancellationToken);
        if (entity == null)
            return Result.Fail($"The {Feature} {request.Id} is not found.");

        repository.Delete(entity);
        return Result.Ok();
    }
}
```

### 3. Query Conventions

#### Result DTO Pattern

```csharp
public record {Feature}Result
{
    public required Guid Id { get; init; }
    public required string Name { get; init; }
    public required string RequiredProperty { get; init; }
    public string? OptionalProperty { get; init; }
}
```

#### Paginated Query Pattern

```csharp
public class Page{Feature}PageQuery : Fluents.Queries.IWitPageResponse<{Feature}Result>
{
    public int PageSize { get; init; } = 100;
    public int PageIndex { get; init; }
}

internal sealed class {Feature}PageableQueryValidator : AbstractValidator<Page{Feature}PageQuery>
{
    public {Feature}PageableQueryValidator()
    {
        RuleFor(x => x.PageSize).NotNull().InclusiveBetween(1, 1000);
        RuleFor(x => x.PageIndex).NotNull().InclusiveBetween(0, 1000);
    }
}

internal sealed class Page{Feature}sQueryHandler(
    IReadRepository<{Feature}> repo,
    IMapper mapper) : Fluents.Queries.IPageHandler<Page{Feature}PageQuery, {Feature}Result>
{
    public async Task<IPagedList<{Feature}Result>> OnHandle(Page{Feature}PageQuery request,
        CancellationToken cancellationToken)
    {
        return await repo.Gets()
            .ProjectToType<{Feature}Result>(mapper.Config)
            .OrderBy(p => p.Name) // Default ordering
            .ToPagedListAsync(request.PageIndex, request.PageSize, null, cancellationToken);
    }
}
```

#### Single Item Query Pattern

```csharp
public record {Feature}Query : Fluents.Queries.IWitResponse<{Feature}Result>
{
    public required Guid Id { get; init; }
}

internal sealed class Single{Feature}QueryHandler(
    IReadRepository<{Feature}> repo)
    : Fluents.Queries.IHandler<{Feature}Query, {Feature}Result>
{
    public async Task<{Feature}Result?> OnHandle({Feature}Query request, CancellationToken cancellationToken)
    {
        return await repo.GetDto<{Feature}Result>()
            .FirstOrDefaultAsync(p => p.Id == request.Id, cancellationToken);
    }
}
```

### 4. Event and Handler Conventions

```csharp
public sealed record {Feature}CreatedEvent(Guid Id, string Name);

internal sealed class {Feature}CreatedEventHandler : Fluents.EventsConsumers.IHandler<{Feature}CreatedEvent>
{
    public Task OnHandle({Feature}CreatedEvent notification, CancellationToken cancellationToken)
    {
        // Handle the event (logging, notifications, integration, etc.)
        return Task.CompletedTask;
    }
}
```

### 5. DDD Entity Conventions

```csharp
[Table("{Feature}s", Schema = DomainSchemas.{FeatureArea})]
public class {Feature} : AggregateRoot
{
    public {Feature}(string name, string requiredProperty, string byUser)
        : this(Guid.Empty, name, requiredProperty, byUser)
    {
    }

    internal {Feature}(Guid id, string name, string requiredProperty, string createdBy)
        : base(id, createdBy)
    {
        Name = name;
        RequiredProperty = requiredProperty;
    }

    public string Name { get; private set; }
    public string RequiredProperty { get; private set; }
    public string? OptionalProperty { get; private set; }

    public void Update(string? name, string? optionalProperty, string userId)
    {
        if (!string.IsNullOrEmpty(name))
            Name = name;
        
        OptionalProperty = optionalProperty;
        SetUpdatedBy(userId);
    }
}
```

**Key Patterns:**
- Private setters for all properties
- Constructor overloads (public with Guid.Empty, internal with explicit ID)
- Update methods that call `SetUpdatedBy(userId)`
- Use `[Table]` attribute with appropriate schema
- Domain behavior encapsulated in methods

### 6. Repository Conventions

#### Interface Pattern

```csharp
public interface I{Feature}Repo : IRepository<{Feature}>
{
    Task<bool> IsDuplicateAsync(string uniqueProperty);
    // Add other custom query methods
}
```

#### Implementation Pattern

```csharp
internal sealed class {Feature}Repo(CoreDbContext dbContext)
    : Repository<{Feature}>(dbContext), I{Feature}Repo
{
    public Task<bool> IsDuplicateAsync(string uniqueProperty)
    {
        return Gets().AnyAsync(f => f.UniqueProperty == uniqueProperty);
    }
}
```

### 7. EfCore Configuration Conventions

```csharp
internal sealed class {Feature}Mapper : DefaultEntityTypeConfiguration<{Feature}>
{
    public override void Configure(EntityTypeBuilder<{Feature}> builder)
    {
        base.Configure(builder);

        // Indexes
        builder.HasIndex(p => p.UniqueProperty).IsUnique();
        
        // Property configurations
        builder.Property(p => p.Name).HasMaxLength(150).IsRequired();
        builder.Property(p => p.RequiredProperty).HasMaxLength(100).IsRequired();
        builder.Property(p => p.OptionalProperty).HasMaxLength(50).IsRequired(false);
        
        // Special column types
        builder.Property(p => p.DateProperty).HasColumnType("Date");
        
        // Table mapping
        builder.ToTable("{Feature}s", DomainSchemas.{FeatureArea});
    }
}
```

### Common Imports and Attributes

Always include these common using statements based on the layer:

**Actions/Commands:**
```csharp
using System.ComponentModel;
using System.Text.Json.Serialization;
using SlimBus.AppServices.Extensions.LazyMapper;
using SlimBus.Domains.Features.{Feature}.Entities;
```

**Queries:**
```csharp
using DKNet.EfCore.Repos.Abstractions;
using Microsoft.EntityFrameworkCore;
using X.PagedList;
using X.PagedList.EF;
```

**Repositories:**
```csharp
using DKNet.EfCore.Repos;
using SlimBus.Domains.Features.{Feature}.Entities;
using SlimBus.Domains.Features.{Feature}.Repos;
using SlimBus.Infra.Contexts;
```

### Validation Patterns

- Use `FluentValidation` with `AbstractValidator<T>` for input validation
- Include appropriate length constraints and business rules
- Use `.When()` for conditional validation
- Validate business rules in command handlers, not validators
- Use `Result.Fail<T>()` for business rule violations
- Include field metadata in error results for better client experience:
  ```csharp
  return Result.Fail("The Id is invalid.")
      .WithError(new Error("The Id is invalid.") { Metadata = { ["field"] = nameof(request.Id) } });
  ```
- Return `IResult<T>` from command handlers for consistent error handling

### Naming Conventions

- **Commands**: `{Action}{Feature}Command` (e.g., `CreateProfileCommand`)
- **Queries**: `{Feature}Query` for single, `Page{Feature}PageQuery` for collections
- **Results**: `{Feature}Result`
- **Events**: `{Feature}{Action}Event` (e.g., `ProfileCreatedEvent`)
- **Handlers**: `{Command/Query/Event}Handler`
- **Validators**: `{Command/Query}Validator`
- **Repositories**: `{Feature}Repo` and `I{Feature}Repo`
- **Mappers**: `{Feature}Mapper`

---

## Additional DKNet-Specific Guidelines

- Follow Domain-Driven Design principles in entity design
- Use the Result pattern for error handling instead of exceptions
- Implement idempotency for creation operations
- Add domain events for significant business operations
- Use lazy mapping for command results to improve performance
- Prefer composition over inheritance in service design
- Use schema-based table organization for better database management

---

_Keep this document up-to-date as the project and its conventions evolve._