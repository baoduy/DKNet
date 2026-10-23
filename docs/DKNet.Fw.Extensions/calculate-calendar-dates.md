# Find calendar quarters and month ends

Part of [DKNet.Fw.Extensions](index.md). Use `DateTimeExtensions` for calendar quarter numbers and the last day of the same month.

```csharp
using System;
using DKNet.Fw.Extensions.Primitives;

var date = new DateTime(2026, 2, 10, 12, 30, 0, DateTimeKind.Utc);
Console.WriteLine(date.InQuarter()); // 1
Console.WriteLine(date.LastDayOfMonth().ToString("O")); // 2026-02-28T12:30:00.0000000Z
Console.WriteLine(((DateTime?)null).LastDayOfMonth() is null); // True
```

![LastDayOfMonth calculates DaysInMonth minus the current day, then adds that many days to the original DateTime.](diagrams/calendar-dates.svg)

`InQuarter()` computes `((Month - 1) / 3) + 1`. `LastDayOfMonth()` uses `DateTime.AddDays`, preserving the original time, subsecond precision and `Kind`. Its nullable overload returns null for null input.

## ⚠️ Gotchas & limits

- Month end is not midnight unless the input time was midnight.
- These are calendar operations on `DateTime`; there is no time-zone conversion or fiscal-calendar setting.
