# DKNet.EfCore.DataAuthorization — AI Skill File

> **Package**: `DKNet.EfCore.DataAuthorization`  
> **Minimum Version**: `10.0.0`  
> **Minimum .NET**: `net10.0`  
> **Source**: `src/EfCore/DKNet.EfCore.DataAuthorization/`

---

## Purpose

Enforces row-level data ownership by automatically injecting a global EF Core query filter that restricts every query to rows the current user/tenant is allowed to see — without requiring manual `.Where(x => x.OwnedBy == userId)` in every handler.

---

## When To Use

- ✅ Multi-tenant applications where each tenant must see only their own data
- ✅ User-owned resources where rows belong to a specific user
- ✅ Any scenario requiring automatic row-level security applied consistently at the data layer

## When NOT To Use

- ❌ Role-based access control at the endpoint level — use ASP.NET Core authorization policies for that
- ❌ Column-level restrictions (hiding sensitive fields) — use `DKNet.EfCore.Encryption` or DTO projection for that
- ❌ Entities that are legitimately shared/global across tenants — do not implement `IOwnedBy` on those

---

## Installation

```bash
dotnet add package DKNet.EfCore.DataAuthorization
```

---

## Setup / DI Registration

```csharp
// 1. Entity implements IOwnedBy
public class Document : IOwnedBy
{
    public Guid Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? OwnedBy { get; private set; }  // ← ownership key (tenant ID or user ID)

    public void SetOwnedBy(string ownerKey) => OwnedBy = ownerKey;
    public string? GetOwnedBy() => OwnedBy;
}

// 2. Implement IDataOwnerProvider — resolve current user/tenant from HTTP context
public class HttpContextOwnerProvider(IHttpContextAccessor accessor) : IDataOwnerProvider
{
    // Key assigned to new entities
    public string GetOwnershipKey()
        => accessor.HttpContext?.User.FindFirstValue(ClaimTypes.NameIdentifier) ?? string.Empty;

    // Keys the current user can see (e.g., their own + shared keys)
    public IEnumerable<string> GetAccessibleKeys()
        => [GetOwnershipKey()];
}

// 3. Register in DI
services.AddHttpContextAccessor();
services.AddAutoDataKeyProvider<AppDbContext, HttpContextOwnerProvider>();
```

---

## Key API Surface

| Type / Method | Role |
|---|---|
| `IOwnedBy` | Entity interface — implement to opt-in to row-level filtering |
| `IDataOwnerProvider` | Service interface — implement to supply ownership key + accessible keys |
| `IDataOwnerDbContext` | DbContext interface — auto-implemented when using `AddAutoDataKeyProvider` |
| `services.AddAutoDataKeyProvider<TDbContext, TProvider>()` | Register the global query filter + DI wiring |

---

## Usage Pattern

```csharp
// ── All queries are automatically filtered ────────────────────────────────
// No changes needed in handlers — the global filter is transparent

public class GetDocumentHandler(IReadRepository<Document> repo)
    : Fluents.Queries.IHandler<GetDocumentQuery, DocumentDto>
{
    public async Task<DocumentDto?> Handle(
        GetDocumentQuery request, CancellationToken cancellationToken)
        // OwnedBy filter is applied automatically by EF Core global query filter
        => await repo.Query(d => d.Id == request.Id)
                     .Select(d => new DocumentDto(d.Id, d.Title))
                     .FirstOrDefaultAsync(cancellationToken);
}

// ── New entities get their OwnedBy set automatically ─────────────────────
public class CreateDocumentHandler(IWriteRepository<Document> repo)
    : Fluents.Requests.IHandler<CreateDocumentCommand, DocumentDto>
{
    public async Task<IResult<DocumentDto>> Handle(
        CreateDocumentCommand request, CancellationToken cancellationToken)
    {
        // OwnedBy is set automatically by the DbContext interceptor — no manual call needed
        var doc = new Document { Title = request.Title };
        await repo.AddAsync(doc, cancellationToken);
        return Results.Ok(new DocumentDto(doc.Id, doc.Title));
    }
}
```

---

## Anti-Patterns

```csharp
// ❌ WRONG — manually filtering by owner in every query
public async Task<DocumentDto?> Handle(GetDocumentQuery req, CancellationToken ct)
    => await repo.Query(d => d.Id == req.Id && d.OwnedBy == _currentUser.Id)
                 .Select(...)
                 .FirstOrDefaultAsync(ct);
// ← duplicated in every handler, breaks when filter logic changes

// ✅ CORRECT — implement IOwnedBy, register AddAutoDataKeyProvider, and write no filter at all
=> await repo.Query(d => d.Id == req.Id)  // OwnedBy filter added automatically
             .Select(...)
             .FirstOrDefaultAsync(ct);

// ❌ WRONG — calling IgnoreQueryFilters() on all queries (defeats the purpose)
repo.Query().IgnoreQueryFilters().ToListAsync();   // ← bypasses ownership filter for all users

// ✅ CORRECT — only use IgnoreQueryFilters inside admin-scoped specifications
public class AdminAllDocumentsSpec : Specification<Document>
{
    public AdminAllDocumentsSpec() { IgnoreQueryFilters(); }  // ← admin use only
}
```

---

## Composes With

| Library | How |
|---|---|
| `DKNet.EfCore.Extensions` | Auto-entity config applies `IOwnedBy` mapping conventions |
| `DKNet.EfCore.Repos` / `DKNet.EfCore.Repos.Abstractions` | Global filter is applied at the `IQueryable` level before any repo method executes |
| `DKNet.EfCore.Specifications` | Use `IgnoreQueryFilters()` on admin specifications that legitimately need all rows |

---

## Security Notes

- `GetOwnershipKey()` MUST return the current user's identifier from a trusted source (JWT claim, session) — never from a query parameter.
- `GetAccessibleKeys()` controls what the user can *read*. Never include keys based on user-supplied input without validation.
- Use `IgnoreQueryFilters()` only in explicitly admin-scoped code paths, never in standard user-facing queries.

---

## Test Example

```csharp
// Uses TestContainers.MsSql-backed integration test environment.
[Fact]
public async Task Query_ReturnsOnlyCurrentTenantDocuments()
{
    // Arrange
    await using var db = CreateDbContext(_container.GetConnectionString());
    var tenantA = "tenant-a";
    var tenantB = "tenant-b";
    db.Documents.AddRange(
        new Document { Title = "A Doc" }.WithOwner(tenantA),
        new Document { Title = "B Doc" }.WithOwner(tenantB));
    await db.SaveChangesAsync();

    // Set up context with tenant A's owner provider
    var ownerProvider = new StaticOwnerProvider(tenantA);
    db.SetDataOwner(ownerProvider);

    // Act
    var docs = await db.Documents.ToListAsync();

    // Assert
    docs.Count.ShouldBe(1);
    docs[0].Title.ShouldBe("A Doc");
}
```

---

## Quick Decision Guide

- Use this package for row-level ownership filtering.
- Keep shared/global entities outside `IOwnedBy` when cross-tenant visibility is required.
- Limit `IgnoreQueryFilters()` to explicit admin-only scenarios.

---

## Version

| Version | Notes |
|---|---|
| `10.0.0` | Initial documentation — `IOwnedBy`, `IDataOwnerProvider`, `AddAutoDataKeyProvider` |

