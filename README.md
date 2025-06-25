# DKNet Framework
[![codecov](https://codecov.io/github/baoduy/DKNet/graph/badge.svg?token=xtNN7AtB1O)](https://codecov.io/github/baoduy/DKNet)
[![.NET](https://img.shields.io/badge/.NET-9.0-blue)](https://dotnet.microsoft.com/)
[![License](https://img.shields.io/badge/License-MIT-green.svg)](LICENSE)
[![NuGet](https://img.shields.io/nuget/v/DKNet.Fw.Extensions)](https://www.nuget.org/packages/DKNet.Fw.Extensions/)
[![CodeQL Advanced](https://github.com/baoduy/DKNet/actions/workflows/codeql.yml/badge.svg)](https://github.com/baoduy/DKNet/actions/workflows/codeql.yml)
[![Quality Gate Status](https://sonarcloud.io/api/project_badges/measure?project=baoduy_DKNet&metric=alert_status)](https://sonarcloud.io/summary/new_code?id=baoduy_DKNet)

## Overview

**DKNet Framework** is a comprehensive collection of .NET libraries designed to enhance and simplify enterprise application development using **SlimMessageBus as a MediatR alternative** with **Domain-Driven Design (DDD)** principles. The framework provides robust foundations for building secure, scalable, and maintainable APIs while promoting clean architecture patterns and separation of concerns.

All projects in this framework serve as reference implementations and template foundations to help developers understand how to design their APIs securely and implement modern architectural patterns effectively.

---

## 🚀 Key Features

- **SlimMessageBus Integration**: Lightweight alternative to MediatR for CQRS and messaging patterns
- **Domain-Driven Design**: Full DDD implementation with aggregates, entities, and domain events
- **Clean Architecture**: Layered architecture with strict separation of concerns
- **EF Core Extensions**: Comprehensive Entity Framework Core enhancements and patterns
- **Security-First**: Built-in security patterns and data authorization mechanisms
- **Template Projects**: Ready-to-use project templates for rapid development
- **Aspire Integration**: .NET Aspire hosting extensions for cloud-native applications
- **Architectural Governance**: Automated enforcement of architectural rules using ArchUnitNET

---

## 📁 Project Structure

### Core Framework
- **[DKNet.Fw.Extensions](Core/DKNet.Fw.Extensions)** - Framework-level extensions and utilities

### Entity Framework Core Extensions
- **[DKNet.EfCore.Abstractions](EfCore/DKNet.EfCore.Abstractions)** - Core abstractions and interfaces
- **[DKNet.EfCore.DataAuthorization](EfCore/DKNet.EfCore.DataAuthorization)** - Data authorization and access control
- **[DKNet.EfCore.Events](EfCore/DKNet.EfCore.Events)** - Domain event handling and dispatching
- **[DKNet.EfCore.Extensions](EfCore/DKNet.EfCore.Extensions)** - EF Core functionality enhancements
- **[DKNet.EfCore.Hooks](EfCore/DKNet.EfCore.Hooks)** - Lifecycle hooks for EF Core operations
- **[DKNet.EfCore.Relational.Helpers](EfCore/DKNet.EfCore.Relational.Helpers)** - Relational database utilities
- **[DKNet.EfCore.Repos](EfCore/DKNet.EfCore.Repos)** - Repository pattern implementations
- **[DKNet.EfCore.Repos.Abstractions](EfCore/DKNet.EfCore.Repos.Abstractions)** - Repository abstractions

### Messaging & CQRS
- **[DKNet.SlimBus.Extensions](SlimBus/DKNet.SlimBus.Extensions)** - SlimMessageBus extensions for EF Core
- **DKNet.EfCore.SlimBus.Events** - SlimMessageBus event integration for EF Core

### Service Layer
- **[DKNet.Svc.BlobStorage.Abstractions](Services/DKNet.Svc.BlobStorage.Abstractions)** - File storage service abstractions
- **[DKNet.Svc.BlobStorage.AwsS3](Services/DKNet.Svc.BlobStorage.AwsS3)** - AWS S3 storage adapter
- **[DKNet.Svc.BlobStorage.AzureStorage](Services/DKNet.Svc.BlobStorage.AzureStorage)** - Azure Blob storage adapter
- **[DKNet.Svc.BlobStorage.Local](Services/DKNet.Svc.BlobStorage.Local)** - Local file system storage
- **[DKNet.Svc.Transformation](Services/DKNet.Svc.Transformation)** - Data transformation services

### Cloud-Native & Hosting
- **[Aspire.Hosting.ServiceBus](Aspire/Aspire.Hosting.ServiceBus)** - .NET Aspire Service Bus hosting extensions

### Templates
- **[SlimBus.ApiEndpoints](z_Templates/SlimBus.ApiEndpoints)** - Complete API template using SlimMessageBus

---

## 🏗️ Architecture Overview

### Domain-Driven Design Implementation

The framework implements a full DDD approach with:

![Diagram](Diagram.png)

```
┌─────────────────┐    ┌─────────────────┐    ┌─────────────────┐
│   API Layer     │    │  Application    │    │   Domain        │
│                 │    │   Services      │    │                 │
│ • Controllers   │◄──►│ • Commands      │◄──►│ • Entities      │
│ • Endpoints     │    │ • Queries       │    │ • Aggregates    │
│ • Validation    │    │ • Events        │    │ • Value Objects │
└─────────────────┘    └─────────────────┘    └─────────────────┘
         │                       │                       │
         ▼                       ▼                       ▼
┌─────────────────┐    ┌─────────────────┐    ┌─────────────────┐
│ Infrastructure  │    │   SlimBus       │    │   EF Core       │
│                 │    │                 │    │                 │
│ • Repositories  │    │ • Message Bus   │    │ • DbContext     │
│ • External APIs │    │ • Event Handlers│    │ • Change Tracking│
│ • File Storage  │    │ • CQRS Pipeline │    │ • Interceptors  │
└─────────────────┘    └─────────────────┘    └─────────────────┘
```

### Messaging Architecture with SlimMessageBus

SlimMessageBus serves as the central nervous system, replacing MediatR with a more flexible and lightweight approach:

```csharp
// Request/Response Pattern
public record CreateProfileCommand(string Name, string Email) : Fluents.Requests.IWitResponse<Result<ProfileDto>>;

// Query Pattern  
public record GetProfileQuery(int Id) : Fluents.Queries.IWitResponse<Result<ProfileDto>>;

// Event Pattern
public record ProfileCreatedEvent(int ProfileId, string Name) : IEventHandler;

// Service Registration
services.AddSlimBusForEfCore(mbb =>
{
    mbb.AddJsonSerializer();
    
    // Memory bus for internal operations
    mbb.AddChildBus("InMemory", mb => 
        mb.WithProviderMemory()
          .AutoDeclareFrom(typeof(CreateProfileCommand).Assembly));
    
    // Azure Service Bus for external events
    mbb.AddChildBus("AzureBus", mb => 
        mb.WithProviderServiceBus(cfg => cfg.ConnectionString = connectionString));
});
```

---

## 💡 Key Design Patterns

### 1. CQRS with SlimMessageBus

**Commands** (Write Operations):
```csharp
public record UpdateProfileCommand(int Id, string Name) : Fluents.Requests.IWitResponse<Result>;

public class UpdateProfileHandler : Fluents.Requests.IHandler<UpdateProfileCommand, Result>
{
    public async Task<IResult<Result>> Handle(UpdateProfileCommand request)
    {
        // Business logic here
        return Result.Ok();
    }
}
```

**Queries** (Read Operations):
```csharp
public record GetProfilesQuery(int PageSize, int PageNumber) : Fluents.Queries.IWitPageResponse<ProfileDto>;

public class GetProfilesHandler : Fluents.Queries.IPageHandler<GetProfilesQuery, ProfileDto>
{
    public async Task<IPagedList<ProfileDto>> Handle(GetProfilesQuery request)
    {
        // Query logic here
        return profiles.ToPagedList(request.PageNumber, request.PageSize);
    }
}
```

### 2. Domain Events Integration

Automatic event publishing after successful operations:

```csharp
public class EfAutoSavePostProcessor<TRequest, TResponse> : IRequestHandlerInterceptor<TRequest, TResponse>
{
    public async Task<TResponse> OnHandle(TRequest request, Func<Task<TResponse>> next, IConsumerContext context)
    {
        var response = await next();
        
        // Auto-save EF Core changes for successful operations
        if (response is IResultBase { IsSuccess: true })
        {
            var dbContexts = serviceProvider.GetServices<DbContext>();
            foreach (var db in dbContexts.Where(db => db.ChangeTracker.HasChanges()))
                await db.SaveChangesAsync(context.CancellationToken);
        }
        
        return response;
    }
}
```

### 3. Repository Pattern with EF Core

```csharp
public interface IProfileRepository : IGenericRepository<Profile>
{
    Task<Profile?> GetByEmailAsync(string email);
    Task<PagedResult<Profile>> GetPagedAsync(int page, int size);
}

public class ProfileRepository : GenericRepository<Profile>, IProfileRepository
{
    public async Task<Profile?> GetByEmailAsync(string email)
    {
        return await GetAll().FirstOrDefaultAsync(p => p.Email == email);
    }
}
```

### 4. Data Authorization

Role-based and policy-based data access control:

```csharp
public class DataAuthorizationService : IDataAuthorizationService
{
    public async Task<bool> CanAccessAsync<T>(T entity, string operation, ClaimsPrincipal user)
    {
        // Implement authorization logic
        return await EvaluatePoliciesAsync(entity, operation, user);
    }
}
```

---

## 🛠️ Getting Started

### Prerequisites

- .NET 9.0 SDK
- Entity Framework Core 9.0+
- Azure Service Bus (for distributed scenarios)

### Quick Start

1. **Install the Core Package**:
   ```bash
   dotnet add package DKNet.SlimBus.Extensions
   dotnet add package DKNet.EfCore.Extensions
   ```

2. **Configure Services**:
   ```csharp
   public void ConfigureServices(IServiceCollection services)
   {
       // Add EF Core
       services.AddDbContext<AppDbContext>(options =>
           options.UseSqlServer(connectionString));
       
       // Add SlimMessageBus with EF Core integration
       services.AddSlimBusForEfCore(mbb =>
       {
           mbb.AddJsonSerializer()
              .AddChildBus("Memory", mb => 
                  mb.WithProviderMemory()
                    .AutoDeclareFrom(typeof(Program).Assembly));
       });
       
       // Add repositories
       services.AddScoped<IProfileRepository, ProfileRepository>();
   }
   ```

3. **Use the Template**:
   ```bash
   # Clone the template project
   git clone https://github.com/baoduy/DKNet
   cd DKNet/z_Templates/SlimBus.ApiEndpoints
   
   # Customize for your needs
   # The template includes complete setup for API, domain, infrastructure, and tests
   ```

---

## 📚 Template Projects

### SlimBus.ApiEndpoints Template

A complete, production-ready API template featuring:

- **Clean Architecture**: Separate layers for API, Application, Domain, and Infrastructure
- **Security**: CSRF protection, JWT authentication, data authorization
- **API Documentation**: Swagger/OpenAPI with Scalar integration  
- **Health Checks**: Comprehensive health monitoring
- **Error Handling**: Global exception handling with problem details
- **Testing**: Unit and integration tests with high coverage
- **DevOps**: Docker support and CI/CD pipeline configuration

**Project Structure**:
```
SlimBus.ApiEndpoints/
├── SlimBus.Api/              # API layer with endpoints and configuration
├── SlimBus.AppServices/      # Application services and business logic
├── SlimBus.Domains/          # Domain entities and business rules
├── SlimBus.Infra/            # Infrastructure and data access
├── SlimBus.Share/            # Shared contracts and DTOs
├── SlimBus.AppHost/          # .NET Aspire hosting project
└── SlimBus.App.Tests/        # Comprehensive test suite
```

---

## 🔒 Security Features

### 1. Data Authorization
- Role-based access control (RBAC)
- Policy-based authorization
- Field-level security
- Audit logging

### 2. API Security
- CSRF protection with automatic token management
- JWT token validation
- Rate limiting and throttling
- Input validation and sanitization

### 3. Secure Development Practices
- Principle of least privilege
- Defense in depth
- Secure defaults
- Regular security scanning

---

## 🧪 Testing Strategy

The framework promotes comprehensive testing with:

- **Unit Tests**: Isolated component testing
- **Integration Tests**: End-to-end workflow validation
- **Architecture Tests**: Automated architectural rule enforcement using ArchUnitNET
- **Performance Tests**: Load testing and benchmarking

### Architectural Testing Example

```csharp
[Fact]
public void DomainLayer_ShouldNotDependOn_InfrastructureLayer()
{
    var rule = ArchRuleDefinition.Types()
        .That().ResideInNamespace("*.Domains")
        .Should().NotDependOnAnyTypesThat()
        .ResideInNamespace("*.Infrastructure");
        
    rule.Check(Assembly.GetExecutingAssembly());
}
```

---

## 🚀 Performance & Scalability

### SlimMessageBus Advantages
- **Lightweight**: Minimal overhead compared to MediatR
- **Flexible**: Multiple transport providers (Memory, Azure Service Bus, RabbitMQ)
- **Scalable**: Supports distributed messaging patterns
- **Efficient**: Optimized for high-throughput scenarios

### EF Core Optimizations
- **Change Tracking**: Automatic save interceptors
- **Query Optimization**: Repository pattern with IQueryable support
- **Connection Management**: Proper DbContext lifecycle management
- **Bulk Operations**: Batch processing support

---

## 📖 Documentation

Each component includes detailed documentation:

- **API Documentation**: Generated with Swagger/OpenAPI
- **Architecture Decision Records**: Located in `/memory-bank/`
- **Code Examples**: Comprehensive samples in template projects
- **Best Practices**: Documented patterns and anti-patterns

---

## 🤝 Contributing

We welcome contributions! Please see our [Contributing Guide](CONTRIBUTING.md) for details on:

- Code standards and conventions
- Pull request process
- Testing requirements
- Documentation standards

### Development Setup

1. Clone the repository
2. Install .NET 9.0 SDK
3. Run `dotnet restore`
4. Execute tests: `dotnet test`

---

## 📄 License

This project is licensed under the [MIT License](LICENSE).

---

## 🙏 Acknowledgments

- **SlimMessageBus Team**: For providing an excellent messaging framework
- **Entity Framework Team**: For the robust ORM foundation
- **Domain-Driven Design Community**: For architectural guidance and patterns
- **Contributors**: All developers who have contributed to this framework

---

## 📞 Support

- **Issues**: [GitHub Issues](https://github.com/baoduy/DKNet/issues)
- **Discussions**: [GitHub Discussions](https://github.com/baoduy/DKNet/discussions)
- **Documentation**: [Wiki](https://github.com/baoduy/DKNet/wiki)

For detailed implementation examples, please refer to the template projects in the `z_Templates` directory.