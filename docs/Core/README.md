# Core

Foundational, dependency-light utilities that sit at the bottom of the DKNet dependency graph. Nothing here depends
on EF Core, ASP.NET Core, or messaging — these packages are safe to reference from a domain model, an
infrastructure adapter, or a plain console app.

## Packages

| Package | Description |
|---|---|
| [`DKNet.Fw.Extensions`](./DKNet.Fw.Extensions.md) | Extension methods and reflection helpers — string/type/enum/DateTime/property/attribute extensions, DI registration guards, and fluent assembly/type scanning (`TypeExtractors`). |
| [`DKNet.RandomCreator`](./DKNet.RandomCreator.md) | Cryptographically secure random string/char generation with digit and symbol quotas, for passwords, tokens, and other secrets. |

## Install

```bash
dotnet add package DKNet.Fw.Extensions
dotnet add package DKNet.RandomCreator
```

Both packages are standalone — no DI wiring or startup configuration is required beyond referencing the NuGet
package (`DKNet.Fw.Extensions` additionally exposes a few DI-registration guard helpers you can opt into).

See each package's page for the full feature list, compiling examples, and gotchas.
