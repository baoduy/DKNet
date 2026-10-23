# Build a paged response envelope

Part of [DKNet.AspCore.Extensions](index.md). `PagedResponse<TResult>` snapshots a page's items and metadata into a minimal-API response:

```csharp
using DKNet.AspCore.Extensions.Responses;
using X.PagedList;
var page = new StaticPagedList<string>(["one", "two"], 1, 2, 5);
var response = new PagedResponse<string>(page);
Console.WriteLine($"{response.Items.Count}/{response.TotalItemCount}; next={response.HasNextPage}");
// 2/5; next=True
```

![PagedResponse copies an IPagedList’s items and page metadata into a response record.](diagrams/paged-envelope.svg)

The page constructor copies `Items` to a list and sets integer `PageNumber`, `PageSize`, `PageCount`, `TotalItemCount`, plus booleans `HasNextPage` and `HasPreviousPage`. The parameterless constructor yields empty items, zero counts and false flags. Both repository lists and bus `MapGetPage` use this envelope.

## ⚠️ Gotchas & limits

- Pagination flags are calculated at construction, not dynamically from later edits to the init properties or mutable `Items` list.
- A null `IPagedList` throws `ArgumentNullException`.
- Constructing the envelope does not execute a query or compute a database count.
