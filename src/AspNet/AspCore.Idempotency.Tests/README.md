# AspCore.Idempotency.Tests

Unit tests for `DKNet.AspCore.Idempotency` — options binding and DI setup. No database, no
containers; these run in milliseconds.

| File | Covers |
|---|---|
| `IdempotencyOptionsTests.cs` | `IdempotencyOptions` binding, defaults and validation (14 cases) |
| `IdempotencySetupTests.cs` | DI registration surface and setup extensions (9 cases) |

```bash
dotnet test AspNet/AspCore.Idempotency.Tests      # from src/
```

Store-backed and end-to-end coverage lives in the sibling projects:
`AspCore.Idempotency.MsSqlStore.Tests`, `.NpgsqlStore.Tests`, `.RedisStore.Tests`
(TestContainers — Docker required) and `AspCore.Idempotency.ApiTests`.

Package reference: `docs/AspNetCore/DKNet.AspCore.Idempotency.md`.
Repo-wide testing conventions: `src/AGENTS.md` and the `dknet-testing` skill.
