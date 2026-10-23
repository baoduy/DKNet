# Represent a soft deletion in the domain

Part of [DKNet.EfCore.Abstractions](index.md). Implement `ISoftDeletableEntity` when deletion records a flag, user and time instead of removing the object.

Use the console-project setup in the [Quick Start](index.md#-quick-start), then replace `Program.cs` with this example.

```csharp
using DKNet.EfCore.Abstractions.Entities;
using FluentResults;

var document = new Document();
document.Delete("alice");
Console.WriteLine(document.IsDeleted); // True

public sealed class Document : Entity, ISoftDeletableEntity
{
    public bool IsDeleted { get; private set; }
    public DateTimeOffset? DeletedOn { get; private set; }
    public string? DeletedBy { get; private set; }
    public IResultBase Delete(string user, DateTimeOffset? deletedOn = null)
    {
        IsDeleted = true;
        DeletedBy = user;
        DeletedOn = deletedOn ?? DateTimeOffset.UtcNow;
        return Result.Ok();
    }
}
```

![Document.Delete sets IsDeleted, DeletedBy and DeletedOn and returns a FluentResults result.](diagrams/model-soft-delete.svg)

The interface declares the fields and `Delete` contract; your method decides whether deletion succeeds. `DeletedBy` carries `[MaxLength(250)]` on the interface.

## ⚙️ Configuration reference

| Argument | Type | Default | Effect |
|---|---|---|---|
| `deletedOn` | `DateTimeOffset?` | `null` | Your implementation decides how to handle an omitted time. |

## 🧱 Where it fits

The domain decides whether deletion is allowed and records the deletion state. The host must supply filtering and save behavior; implementing this interface alone installs neither.

![The package component map shows the host, contracts and consumers involved in this capability.](diagrams/components.svg)

## ⚠️ Gotchas & limits

- No built-in query filter or hard-delete conversion is installed by implementing this interface.
- Add your own [global filter](../DKNet.EfCore.Extensions/apply-global-filters.md) and persistence logic if deleted rows must be hidden.
