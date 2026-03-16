# Data Model: Skill File Schema

## Entity: SkillFile

| Field | Type | Required | Notes |
|---|---|---|---|
| `package` | string | ✅ | NuGet package name |
| `minimumVersion` | semver | ✅ | Earliest version the documented API applies to |
| `minimumDotNet` | string | ✅ | Always `net10.0` |
| `source` | path | ✅ | Relative path in repo |
| `purpose` | string | ✅ | One sentence |
| `whenToUse` | string[] | ✅ | ≥1 bullet |
| `whenNotToUse` | string[] | ✅ | ≥1 bullet with redirect |
| `installation` | code | ✅ | `dotnet add package` snippet |
| `diRegistration` | code | ✅ | `Program.cs` service setup |
| `keyApiSurface` | table | ✅ | Type/Method → Role |
| `usagePattern` | code | ✅ | Complete compilable example |
| `antiPatterns` | code[] | ✅ | ≥2 wrong→correct pairs |
| `composesWith` | table | ✅ | Library → relationship |
| `testExample` | code | ✅ | xUnit + Shouldly + TestContainers |
| `securityNotes` | string[] | conditional | Required for security-relevant libraries |
| `versionTable` | table | ✅ | version → notes |

## Entity: MasterIndex (README.md)

| Field | Type | Notes |
|---|---|---|
| `scenarioTable` | table | Scenario → library list → skill file link(s) |
| `libraryList` | table | Package name → file → purpose |
| `loadingOrder` | ordered list | Which files to load for common tasks |

## Entity: CompositionPattern

| Field | Type | Notes |
|---|---|---|
| `name` | string | e.g., "CRUD Endpoint", "Idempotent Mutation" |
| `libraries` | string[] | All packages involved |
| `registrationSnippet` | code | Combined `Program.cs` setup |
| `handlerSnippet` | code | Handler + entity + DTO |
| `endpointSnippet` | code | Endpoint mapping |

## Relationships

```
MasterIndex ──(1:N)──> SkillFile
SkillFile ──(M:N)──> SkillFile  (via "Composes With")
CompositionPattern ──(1:N)──> SkillFile
```

## Validation Rules

- `package` must match an entry in `Directory.Packages.props` or NuGet registry
- `minimumDotNet` must be `net10.0` (constitution requirement)
- `testExample` must NOT contain `UseInMemoryDatabase` (constitution requirement)
- `diRegistration` must NOT contain connection strings or secrets (constitution requirement)

