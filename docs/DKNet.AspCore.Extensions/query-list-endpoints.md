# Filter, search and page list endpoints

Part of [DKNet.AspCore.Extensions](index.md). `MapGetList` binds `ListQueryRequest` from query parameters. `ListFilter` is also usable directly as a colon-separated, JSON-string-compatible condition:

```csharp
using DKNet.AspCore.Extensions.Endpoints;
var filter = ListFilter.Parse("price:GreaterThan:100", null);
Console.WriteLine(filter.Operation); // GreaterThan
Console.WriteLine(filter); // price:GreaterThan:100
```

![List query validation combines filter, text search and activity bounds before specification ordering, projection and paging.](diagrams/list-query.svg)

| Query parameter | Property type | Absent behavior |
|---|---|---|
| `pageNumber` | `int?` | Page 1; values below 1 also become 1. |
| `pageSize` | `int?` | Configured default, bounded by maximum; below 1 uses default. |
| `filter` | `ListFilter[]?` | No named-field conditions. Repeat for AND; at most 20. |
| `search` | `string?` | No text condition; blank also means absent. |
| `orderBy` | `string?` | Default entity ordering. |
| `desc` | `bool?` | False; ignored without `orderBy`. |
| `fromDate` / `toDate` | `DateTimeOffset?` | Default activity lower bound on audited records if neither is supplied. |

Conditions use `field:operation:value`; only the first two colons split. `IsNull` and `IsNotNull` allow the two-part form. Other supported operations are `Equal`, `NotEqual`, `GreaterThan`, `GreaterThanOrEqual`, `LessThan`, `LessThanOrEqual`, `Contains`, `NotContains`, `StartsWith`, `EndsWith`, `In` and `NotIn`. Operation matching is case-insensitive. `In`/`NotIn` split comma-separated values, trim them and remove empty entries. `ListFilterJsonConverter` reads/writes that same string form; `TryParse` returns false for malformed syntax and `Parse` throws `FormatException`.

Fields normalize to PascalCase, accepting snake/kebab spelling and case-insensitive lookup. Filters must name a returned-model field and produce a valid entity predicate; sorting must name a field on both model and entity. Invalid conditions return 400 rather than disappearing.

Search trims input, requires at least 2 characters, and ORs `Contains` expressions over shared model/entity string fields, then ANDs that with filters. Discovery reaches two levels, including supported nested objects/collections; dictionaries and byte arrays are excluded. No searchable fields produces a false predicate. Case sensitivity and translation follow the database.

Default ordering is `CreatedOn` descending then `Id` descending for `IAuditedEntity<TKey>`, otherwise `Id` descending. Explicit ordering replaces it and appends descending `Id` unless ordering by `Id` already.

### Page-size defaults and ceiling

Every path clamps to `MaxPageSize`, including an omitted page size: `min(DefaultPageSize, MaxPageSize)`. Oversized requests are trimmed, not rejected. Register options in code with `AddListQueryOptions`, or bind `ListQueryOptions.ConfigSectionName` yourself with `Configure<ListQueryOptions>`.

### Default recent-activity window

For `IAuditedProperties`, omitted bounds apply a lower bound of UTC now minus the configured months, with no upper bound. A row qualifies when either `CreatedOn` or non-null `UpdatedOn` is in range. Either explicit bound replaces that default; `fromDate=0001-01-01T00:00:00Z` requests all history. Setting the default months to 0 disables the implicit bound. Records without audit properties ignore the window; an inverted explicit range is still refused before that check. The predicate applies before paging and counting, so totals follow the same window.

## ⚙️ Configuration reference

| `ListQueryOptions` option | Type | Default | Effect |
|---|---|---|---|
| `DefaultPageSize` | `int` | 1000 | Size for omitted or invalid-small page sizes. |
| `MaxPageSize` | `int` | 1000 | Ceiling for every page. |
| `DefaultActivityWindowMonths` | `int` | 3 | Implicit lower activity bound; 0 disables. |
| `ConfigSectionName` | `const string` | `DKNet:ListQuery` | Suggested configuration section name. |

`AddListQueryOptions(Action<ListQueryOptions>?)` binds no configuration automatically. It adds data-annotation validation on start: sizes at least 1 and months at least 0. Combined `Configure` registrations affect the final validated options.

## ⚠️ Gotchas & limits

- Date window detection uses `IAuditedProperties`; default ordering uses the narrower `IAuditedEntity<TKey>` contract.
- A supplied search shorter than 2 trimmed characters and more than 20 filters are 400 refusals.
- The sort error names the model even when the missing property is on the entity.
- These rules apply to the repository list mapper; `MapGetPage` dispatches the query handler's own paging contract.
