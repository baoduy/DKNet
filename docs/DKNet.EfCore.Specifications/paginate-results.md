# Page and stream results

Part of [DKNet.EfCore.Specifications](index.md). Use the ordered Quick Start specification for buffered streaming or a repository cursor page:

```csharp
await foreach (var product in repo.ToPageEnumerable(new MinimumPrice(0)))
    Console.WriteLine(product.Name); // Coffee, then Tea
var next = await repo.ToKeysetPageAsync(new MinimumPrice(0), p => p.Id, cursor: 1, pageSize: 20);
Console.WriteLine(next.Single().Name); // Tea
```

![Paging chooses offset materialization, buffered ordered enumeration, or a keyset cursor predicate before fetching rows; arbitrary keysets additionally check whether previous and next pages exist.](diagrams/paginate-results.svg)

| Surface | Ordering owner | Result |
|---|---|---|
| `repo.ToPagedListAsync(spec, pageNumber, pageSize)` | Specification | X.PagedList page; page number is 1-based. |
| `repo.ToPageEnumerable(spec)` | Specification, required | Async rows buffered in offset pages of 100. |
| `query.AfterKeyset` / `BeforeKeyset` | Caller | WHERE only; one or two ascending comparable keys. |
| `repo.ToKeysetPageAsync` | Specification, required | List after a one-key or two-key ascending cursor. |
| `query.ToKeysetPageAsync(configureKeyset, ...)` | Configure delegate | `KeysetPage<TEntity>` with Items, HasPrevious, HasNext. |

The queryable arbitrary-key overload uses MR.EntityFrameworkCore.KeysetPagination, supports mixed directions and forward/backward navigation, and accepts an optional reference object with matching key property names. It owns ordering as well as filtering. The two-key helper uses a lexicographic OR comparison with an additional leading bound on the first key; cursor values are captured as parameters.

## ⚙️ Configuration reference

| Input | Type | Default | Effect |
|---|---|---|---|
| Buffered page size | Internal constant | `100` | Not exposed by repository enumeration helpers. |
| `pageSize` for keysets | `int` | Required | Must be positive; otherwise throws ArgumentOutOfRangeException. |
| Arbitrary-key `direction` | `KeysetPaginationDirection` | `Forward` | Cursor navigation direction. |
| Arbitrary-key `reference` | `object?` | `null` | Null selects the initial page. |
| Async cancellation | `CancellationToken` | Default | Observed by row materialization/enumeration; arbitrary-key existence checks do not accept it. |

## 🧱 Where it fits

The [query architecture](index.md#-architecture) shows the shared DbContext. Both entity and projected repository queries offer paged lists and buffered enumeration; keyset repository helpers return entities.

## ⚠️ Gotchas & limits

- Repository streaming and keyset helpers require at least one declared order clause, otherwise NotSupportedException is thrown. Choose a unique tie-breaker for stable pages.
- Simple AfterKeyset/BeforeKeyset helpers do not sort. Match their ascending comparison with your ordering; use the configured arbitrary-key API for mixed directions.
- Arbitrary-key paging makes a page query plus previous/next existence queries; only the page query observes the cancellation token.
- Buffered enumeration is offset paging rather than a database cursor or a stable snapshot. Concurrent writes can shift page boundaries; tracking queries can retain entities across pages.
- Do not pre-order the arbitrary-key overload and expect that ordering to win: pass all key columns to its configure delegate.
