# Append items to an ICollection

Part of [DKNet.Fw.Extensions](index.md). Use `CollectionExtensions.AddRange` when the destination is exposed as `ICollection<T>`.

```csharp
using System;
using System.Collections.Generic;
using DKNet.Fw.Extensions.Collections;

ICollection<int> values = new List<int> { 1, 2 };
values.AddRange(new[] { 3, 4 });
Console.WriteLine(string.Join(",", values)); // 1,2,3,4
```

![AddRange enumerates each source item and calls ICollection.Add on the destination before advancing to the next item.](diagrams/collection-ranges.svg)

The helper is a `foreach` followed by one `Add` per item. Destination collection semantics determine duplicate handling and enumeration order.

## ⚠️ Gotchas & limits

- There is no rollback: an exception can leave earlier additions in place.
- A read-only destination can throw. Adding while enumerating the same collection may invalidate its enumerator.
- There is no list-specific bulk-copy path or explicit null guard.
