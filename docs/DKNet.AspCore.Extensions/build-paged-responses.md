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

None — no DI registration needed; `PagedResponse<TResult>` is a record you construct directly.

## ⚙️ Configuration reference

None — `PagedResponse<TResult>` reads no options and exposes no override point. It is a record you construct directly from an `IPagedList<TResult>`.

## 🧱 Where it fits

| DKNet consumer | Package | What it does with this feature |
|---|---|---|
| [`MapGetList`](map-entity-endpoints.md) | DKNet.AspCore.Extensions (same package) | Wraps a repository's paged query result in `PagedResponse<TModel>` for the 200 response. |
| [`MapGetPage`](dispatch-bus-requests.md) | DKNet.AspCore.Extensions (same package) | Wraps an `IWitPageResponse` query handler's result in `PagedResponse<TResponse>` for the 200 response. |

**Entry point in your app:** construct `new PagedResponse<TResult>(page)` from a hand-written minimal-API endpoint, or let `MapGetList`/`MapGetPage` build it for you.

## ⚠️ Gotchas & limits

- Pagination flags are calculated at construction, not dynamically from later edits to the init properties or mutable `Items` list.
- A null `IPagedList` throws `ArgumentNullException`.
- Constructing the envelope does not execute a query or compute a database count.
