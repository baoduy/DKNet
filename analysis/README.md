# DKNet `src/` Review — Performance & Simplification

Read-only analysis. **No code was changed.** Every finding below is a proposal for review.

| Report | Contents |
|---|---|
| [performance.md](performance.md) | Runtime cost: allocations, round trips, reflection, caching, build-time |
| [simplification.md](simplification.md) | Custom code replaceable by the BCL, EF Core, ASP.NET Core, or an already-referenced package |
| [correctness-notes.md](correctness-notes.md) | Defects found incidentally while reading (not requested, but they change behaviour) |

> **Correction (verified after publication):** **C3 does not hold** — the hook DI-lifetime defect it describes was already fixed on `dev` before HEAD (commits `61ef4d4`, `5696361`), and `HookScopeResolutionTests` already covers it. **C11** is downgraded to Low as a consequence, since its premise was C3. Both entries are annotated in place. Line numbers throughout these reports are accurate to within a few lines — some were taken from comment-stripped working copies, and they drift further as fixes land; navigate by symbol name where a citation looks off.

## Status

Findings marked ✅ in the two actioned reports are fixed and verified in the working tree. **13 of 18** correctness items and **11 of 23** simplification items are applied (three of those partially — their **Applied** notes say why). `performance.md` is deliberately untouched pending separate review.

## Scope

- Reviewed: all 32 non-test projects under `src/` (`Core`, `EfCore`, `AspNet`, `Services`, `SlimBus`, `Aspire`), ~25,700 lines.
- Target framework is `net10.0` for every shipping library (`Directory.Packages.props:3`); the two Roslyn generators target `netstandard2.0`. Several findings depend on that — .NET 9/10 BCL APIs are available everywhere except inside the generators.
- Not reviewed: test projects, migrations, generated snapshots, docs.

## Executive summary

The framework is well structured and the hot paths are mostly correct. The wins cluster in five places:

1. **Two paging/streaming implementations do the wrong thing at scale.** `ToPageEnumerable` re-queries the database with `Skip`/`Take` per page instead of streaming one reader (`P1`), and `IdempotencyRelationalStore`/`IdempotencyRedisStore` each spend two round trips where one suffices (`P2`, `P3`).
2. **Dynamic filtering parses strings at request time.** Every `(property, Ops, value)` triple and every free-text search clause is rendered to a Dynamic LINQ string and re-parsed per request (`P4`, `P5`), when the same expression trees are already built by hand elsewhere in the same package.
3. **Per-save reflection in the interceptor stack.** Audit logs, data-owner stamping, and navigation scanning re-resolve attributes and `PropertyInfo` on every `SaveChanges` (`P6`–`P8`, `P22`); EF Core's own metadata and change tracker do all of it without reflection.
4. **Cloud blob clients are per-scope, and each one does a control-plane call on first use.** `ListBuckets`+`PutBucket` (S3) and `CreateIfNotExists` (Azure) run on the first blob call of every request (`P10`, `P11`). The whole abstraction is also buffer-only — `BinaryData` end to end, with an extra full copy on local save (`P12`).
5. **Both source generators re-run on every compilation.** `CrudGenerator` drives off `CompilationProvider`, and `DtoGenerator` puts `INamedTypeSymbol` and `HashSet<string>` in its pipeline model, so incremental caching never hits and the IDE re-generates on every keystroke (`P13`, `P14`).

On the simplification side the recurring theme is code written before the BCL grew the API: `ToListAsync` for `IAsyncEnumerable` (now in .NET 10), `Base64.IsValid`, `Convert.ToHexStringLower`, `File.WriteAllBytesAsync(ReadOnlyMemory<byte>)`, `Path.GetRelativePath`, `IValueHttpResult`, `Database.IsSqlServer()`, `TryAddScoped`. Roughly **900–1,100 lines are deletable** without losing a feature, the largest single block being the legacy two-phase ordering path in `SpecificationExtensions` plus its two redundant backing lists.

## Top 15 by value/effort

Ordered by impact first, effort second. "Effort" is implementation plus test adjustment.

| # | Finding | Area | Impact | Effort |
|---|---|---|---|---|
| 1 | [P1](performance.md#p1) — `ToPageEnumerable` offset-paginates instead of streaming | Specifications | N round trips + N server-side sorts per enumeration → 1 | S |
| 2 | [P13](performance.md#p13) / [P14](performance.md#p14) — generators re-run per compilation | Generators | IDE typing latency, full rebuild cost | M |
| 3 | [P4](performance.md#p4) — Dynamic LINQ string parse per filter condition | Specifications | Removes a parser from the request path | M |
| 4 | [P10](performance.md#p10) / [P11](performance.md#p11) — cloud client per scope + control-plane call | Blob storage | One extra network RTT per request; new connection pool per scope | S |
| 5 | [S1](simplification.md#s1) — delete the legacy two-phase ordering path | Specifications | −110 lines, one ordering model instead of two | S |
| 6 | [P6](performance.md#p6) — cache audit-log attribute lookups per entity type | AuditLogs | Reflection per property per entity per save → once | S |
| 7 | [P2](performance.md#p2) — insert-first idempotency reservation | Idempotency | 2 DB round trips → 1 on the common path | M |
| 8 | [P3](performance.md#p3) — `SET NX GET` single-call Redis reservation | Idempotency | 2 Redis RTT → 1 | S |
| 9 | [P15](performance.md#p15) — `SafeKey`/`CompositeKey` recomputed on every read | Idempotency | ~6 string rebuilds + a regex cache lookup per request | S |
| 10 | [P16](performance.md#p16) — endpoint filter runs even with no contextual members | AspCore.Extensions | Removes per-request work from every endpoint | S |
| 11 | [P7](performance.md#p7) — `DataOwnerHook` reflection stamping | DataAuthorization | Reflection + `Convert.ChangeType` per entity per save → EF metadata | S |
| 12 | [P17](performance.md#p17) — `TypeExtractor` filters via `IQueryable`/expression trees | Core | Expression interpretation on an in-memory array | S |
| 13 | [P18](performance.md#p18) — Puppeteer browser launched per PDF | PdfGenerators | ~300–800 ms + a Chromium process per document | M |
| 14 | [S2](simplification.md#s2) — delete `AsyncEnumerableExtensions.ToListAsync` | Core | Removes an API that now collides with .NET 10's | S |
| 15 | [P12](performance.md#p12) — `blob.Data.ToArray()` on local save | Blob storage | Removes a full second copy of every payload | S |

## Suggested sequencing

- **Batch 1 — mechanical, low risk (1 day).** `P15`, `P16`, `P12`, `P8`, `P19`, `P20`, `S2`, `S4`, `S5`, `S6`, `S7`, `S9`, `S12`. All local, all covered by existing tests.
- **Batch 2 — measurable wins, contained blast radius (2–3 days).** `P1`, `P3`, `P6`, `P7`, `P10`, `P11`, `P17`, `S1`, `S3`, `S8`.
- **Batch 3 — needs design agreement.** `P4` (drops `System.Linq.Dynamic.Core` from the typed path), `P2` (changes idempotency reservation ordering), `P13`/`P14` (generator pipeline rewrite), `S10` (streaming blob API — a breaking interface change), `P22` (`AddNewEntitiesFromNavigations` may be entirely redundant with EF Core's own graph tracking; needs a spike to confirm).

## Measurement note

No benchmarks exist in the repo. Before `P4`, `P13`/`P14`, and `P22` — the three items where the fix costs real work and the payoff is asserted rather than obvious — a BenchmarkDotNet project (or, for the generators, `dotnet build -bl` plus the Roslyn generator timing report) would be worth the hour it takes to set up. The rest are round-trip counts and allocation counts that can be read off the code.
