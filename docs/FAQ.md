# Frequently Asked Questions (FAQ)

Common questions and answers about the DKNet Framework.

## 📋 Table of Contents

- [General Questions](#general-questions)
- [Architecture & Design](#architecture--design)
- [Entity Framework Core](#entity-framework-core)
- [CQRS & Messaging](#cqrs--messaging)
- [Performance](#performance)
- [Testing](#testing)
- [Deployment](#deployment)
- [Troubleshooting](#troubleshooting)

---

## 🤔 General Questions

### What is DKNet Framework?

DKNet is a comprehensive .NET framework designed to enhance enterprise application development using Domain-Driven Design (DDD) principles and Onion Architecture patterns. It provides a collection of libraries that simplify building scalable, maintainable applications.

### Which .NET versions are supported?

DKNet Framework requires **.NET 10.0** or later. All packages are built and tested against .NET 10.0.

### Is DKNet Framework free to use?

Yes! DKNet Framework is released under the [MIT License](https://github.com/baoduy/DKNet/blob/main/LICENSE), making it free for both commercial and non-commercial use.

### How stable is DKNet Framework?

DKNet Framework follows semantic versioning and maintains high code coverage (99% for core libraries). It includes comprehensive testing, CI/CD pipelines, and code quality checks.

### Where can I get help?

- **Documentation**: [Complete Documentation](README.md)
- **Issues**: [GitHub Issues](https://github.com/baoduy/DKNet/issues)
- **Discussions**: [GitHub Discussions](https://github.com/baoduy/DKNet/discussions)
- **Examples**: [SlimBus Template](../src/Templates/SlimBus.ApiEndpoints/)

---

## 🏗️ Architecture & Design

### Why use Domain-Driven Design (DDD)?

DDD helps manage complexity in large applications by:
- **Focusing on business domain**: Core business logic is central
- **Ubiquitous language**: Shared terminology between developers and domain experts
- **Bounded contexts**: Clear boundaries between different parts of the system
- **Rich domain models**: Business rules are expressed in code

### What is Onion Architecture?

Onion Architecture is a architectural pattern that:
- **Inverts dependencies**: Inner layers don't depend on outer layers
- **Separates concerns**: Each layer has a specific responsibility
- **Enables testing**: Business logic can be tested in isolation
- **Supports flexibility**: Infrastructure can be easily replaced

### When should I use CQRS?

Consider CQRS (Command Query Responsibility Segregation) when:
- **Complex business logic**: Commands and queries have different requirements
- **Scalability needs**: Read and write operations need different scaling strategies
- **Event sourcing**: You want to capture all changes as events
- **Audit requirements**: Full traceability of operations is needed

### Can I use DKNet without DDD?

While DKNet is designed with DDD in mind, you can use individual components:
- **Core Extensions**: `DKNet.Fw.Extensions` works standalone
- **EF Core Extensions**: Repository patterns can be used without full DDD
- **Blob Storage**: Service abstractions work independently

---

## 🗄️ Entity Framework Core

### Do I need to use all EF Core packages?

No, you can pick and choose based on your needs:
- **Minimum**: `DKNet.EfCore.Abstractions` + `DKNet.EfCore.Extensions`
- **Repository Pattern**: Add `DKNet.EfCore.Repos`
- **Domain Events**: Add `DKNet.EfCore.Events`
- **Data Authorization**: Add `DKNet.EfCore.DataAuthorization`

### How do I handle database migrations?

```bash
# Create migration
dotnet ef migrations add YourMigrationName

# Update database
dotnet ef database update

# For production
dotnet ef script --idempotent > migration.sql
```

### Can I use multiple database contexts?

Yes! Each context can be configured independently:
```csharp
services.AddDbContext<CatalogContext>(options => 
    options.UseSqlServer(connectionString));
services.AddDbContext<IdentityContext>(options => 
    options.UseSqlServer(identityConnectionString));

services.AddDKNetRepositories<CatalogContext>();
services.AddDKNetRepositories<IdentityContext>();
```

### How do I handle multi-tenancy?

DKNet provides built-in multi-tenancy support:
1. **Implement ITenantEntity** on your entities
2. **Configure tenant provider** in DI
3. **Repositories automatically filter** by tenant

See [Multi-tenant Example](Examples/README.md#multi-tenant-application) for details.

### What about database performance?

DKNet includes several performance optimizations:
- **Specification pattern** for complex queries
- **Lazy loading support** where appropriate
- **Efficient change tracking** in repositories
- **Bulk operations** for large datasets

---

## 📨 CQRS & Messaging

### Do I need MediatR?

The SlimBus template uses a lightweight message bus, but you can use MediatR:
```csharp
services.AddMediatR(typeof(CreateProductHandler));
services.AddDKNetMediatRIntegration();
```

### How do I handle command validation?

Use FluentValidation with the command pipeline:
```csharp
public class CreateProductValidator : AbstractValidator<CreateProductCommand>
{
    public CreateProductValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Price).GreaterThan(0);
    }
}
```

### When are domain events dispatched?

Domain events are dispatched when `SaveChangesAsync()` is called on the DbContext. This ensures events are only sent after successful database commits.

### Can I use async event handlers?

Yes, all event handlers support async operations:
```csharp
public class ProductCreatedHandler : IDomainEventHandler<ProductCreatedEvent>
{
    public async Task Handle(ProductCreatedEvent domainEvent, CancellationToken cancellationToken)
    {
        await _emailService.SendNotificationAsync(...);
    }
}
```

---

## ⚡ Performance

### How does DKNet impact performance?

DKNet is designed for performance:
- **Minimal allocations** in hot paths
- **Efficient repository patterns** with proper caching
- **Optimized queries** using specifications
- **Async patterns** throughout

Benchmark results show minimal overhead compared to raw EF Core.

### Should I worry about domain events performance?

Domain events are designed to be lightweight:
- **In-memory dispatch** by default
- **Batched processing** during SaveChanges
- **Async handlers** don't block the main thread
- **Optional external messaging** for scalability

### How do I optimize queries?

1. **Use specifications** for complex queries
2. **Project to DTOs** for read operations
3. **Enable query splitting** for large includes
4. **Use raw SQL** for performance-critical scenarios

```csharp
// Projection example
var results = await repository.Gets()
    .Select(p => new ProductDto { Id = p.Id, Name = p.Name })
    .ToListAsync();
```

---

## 🧪 Testing

### How do I test with DKNet?

DKNet promotes testability:
- **TestContainers** for integration tests
- **In-memory providers** for unit tests
- **Mocking repositories** where needed
- **Domain event testing** utilities

### Should I use TestContainers or in-memory databases?

**TestContainers** for integration tests (recommended):
- Real database behavior
- SQL Server features work correctly
- Catches database-specific issues

**In-memory** for unit tests:
- Faster execution
- No external dependencies
- Focus on business logic

### How do I test domain events?

```csharp
[Test]
public async Task CreateProduct_ShouldRaiseEvent()
{
    // Arrange
    var product = new Product("Test", 10.0m, "user");
    
    // Act
    product.UpdatePrice(15.0m, "user");
    
    // Assert
    var events = product.GetUncommittedEvents();
    events.Should().ContainSingle<ProductPriceChangedEvent>();
}
```

### How do I test with multiple tenants?

```csharp
[Test]
public async Task Repository_ShouldFilterByTenant()
{
    // Arrange
    var tenantProvider = new Mock<ITenantProvider>();
    tenantProvider.Setup(x => x.GetCurrentTenant()).Returns("tenant1");
    
    var repository = new TenantProductRepository(context, tenantProvider.Object);
    
    // Act & Assert
    var products = await repository.GetAllAsync();
    products.Should().OnlyContain(p => p.TenantId == "tenant1");
}
```

---

## 🚀 Deployment

### How do I deploy DKNet applications?

DKNet applications can be deployed like any .NET application:
- **Docker containers** (recommended)
- **Azure App Service**
- **AWS ECS/EKS**
- **On-premises IIS**

The SlimBus template includes Docker support and deployment configurations.

### What about database migrations in production?

**Recommended approach**:
1. Generate idempotent scripts: `dotnet ef script --idempotent`
2. Run scripts during deployment pipeline
3. Never run migrations from application startup in production

**Alternative**: Use migration bundles for zero-downtime deployments.

### How do I handle configuration secrets?

Use standard .NET configuration providers:
- **Azure Key Vault** for Azure deployments
- **AWS Secrets Manager** for AWS deployments
- **Environment variables** for containerized deployments
- **User secrets** for development

### Do I need special considerations for scaling?

DKNet is designed for scale:
- **Stateless services** enable horizontal scaling
- **Event-driven architecture** supports distributed systems
- **Repository pattern** works with caching layers
- **CQRS** enables read/write scaling separation

---

## 🔧 Troubleshooting

### "Unable to resolve service" errors

This usually indicates missing service registration:
```csharp
// Ensure you've registered all required services
services.AddDKNetRepositories<AppDbContext>();
services.AddDKNetSlimBusIntegration();
services.AddDKNetBlobStorage(builder => /* configuration */);
```

### Domain events not firing

Check these common issues:
1. **SaveChangesAsync called**: Events dispatch during save
2. **Event handlers registered**: Use `AddDKNetEventHandlers()`
3. **Async handlers**: Ensure proper async/await usage

### Repository queries not working

Common issues:
1. **DbContext registration**: Ensure context is registered in DI
2. **Repository registration**: Call `AddDKNetRepositories<TContext>()`
3. **Specifications**: Check expression syntax for specifications

### Poor query performance

Performance optimization steps:
1. **Enable query logging**: See generated SQL queries
2. **Use specifications**: Instead of complex LINQ expressions
3. **Project to DTOs**: Avoid loading full entities for display
4. **Check indexes**: Ensure proper database indexing

### Migration fails

Migration troubleshooting:
1. **Check connection string**: Ensure database connectivity
2. **Permissions**: Verify database permissions
3. **Concurrent migrations**: Avoid running multiple migrations simultaneously
4. **Backup first**: Always backup before major migrations

---

## 💡 Best Practices

### Code Organization

1. **Follow Onion Architecture**: Keep dependencies pointing inward
2. **Use feature folders**: Organize by business capability
3. **Separate concerns**: Commands, queries, events in separate files
4. **Consistent naming**: Follow established conventions

### Domain Modeling

1. **Rich domain models**: Put business logic in entities
2. **Value objects**: Use for concepts without identity
3. **Aggregate boundaries**: Keep aggregates small and focused
4. **Domain events**: Use for side effects and integration

### Performance

1. **Project early**: Don't load full entities for display
2. **Lazy loading carefully**: Be aware of N+1 query problems
3. **Async all the way**: Use async/await consistently
4. **Monitor queries**: Use logging to identify problematic queries

### Testing

1. **Test business logic**: Focus on domain entities and services
2. **Use real databases**: TestContainers for integration tests
3. **Mock external dependencies**: Keep tests focused and fast
4. **Test edge cases**: Null values, empty collections, boundaries

---

## 📚 Additional Resources

- **[Getting Started Guide](Getting-Started.md)**: Step-by-step setup
- **[Configuration Guide](Configuration.md)**: Detailed setup options
- **[Examples & Recipes](Examples/README.md)**: Practical implementations
- **[Migration Guide](Migration-Guide.md)**: Upgrade instructions
- **[API Reference](API-Reference.md)**: Detailed API documentation

---

> 💡 **Still have questions?** Don't hesitate to [open an issue](https://github.com/baoduy/DKNet/issues) or start a [discussion](https://github.com/baoduy/DKNet/discussions). The community is here to help!