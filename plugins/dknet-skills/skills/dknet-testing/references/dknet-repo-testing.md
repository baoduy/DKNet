# DKNet repo testing reference

Full depth behind `SKILL.md`'s Part B: running and validating tests when contributing to the DKNet framework itself — not application code that merely depends on it (see `references/consumer-testing.md` for that). Everything here runs from `src/`, where `DKNet.FW.sln` lives.

## Commands

```bash
dotnet restore DKNet.FW.sln
dotnet build   DKNet.FW.sln -c Debug             # must produce zero warnings
dotnet test    DKNet.FW.sln --settings coverage.runsettings --collect:"XPlat Code Coverage"
dotnet test    EfCore/EfCore.Specifications.Tests       # single project
dotnet test    --filter "FullyQualifiedName~DynamicAnd_WithMultipleConditions"
dotnet format                                    # before committing
./verify_nuget_package.sh                        # pack solution to ./nupkgs (Release), then verify
```

`Directory.Build.props` sets `TreatWarningsAsErrors=true`, `Nullable=enable`, `LangVersion=latest`, and `GenerateDocumentationFile=true` solution-wide — any new warning, missing XML doc, or nullable mismatch breaks the build, test code included.

## Coverage targets

| Area | Target |
|---|---|
| Core libraries | 99% line |
| EfCore libraries | 95% line |
| Service libraries | 90% line |
| CI gate (overall) | 80% line |

The 80% CI gate is what actually blocks a PR; the per-area numbers are aspirational, not separately measured or enforced. `.github/workflows/build-test-coverage.yml` restores, builds in Release, runs tests with coverage collection, runs SonarCloud analysis, enforces the gate, and comments coverage on PRs. Its `Test` step is a real pass/fail signal — it does not carry `continue-on-error` (the SonarCloud and coverage-publishing steps around it do) — so a red run there means the build failed, the tests failed, or the coverage gate failed.

## The MsSql / ARM64 rule

SQL Server only runs on x64 and Apple Silicon (via Rosetta). `mcr.microsoft.com/mssql/server` ships x64-only images, with no ARM64 build. `AspCore.Idempotency.MsSqlStore.Tests`' fixture (`Fixtures/ApiFixture.cs`) picks its image off `RuntimeInformation.ProcessArchitecture` and falls back to `mcr.microsoft.com/azure-sql-edge:latest` on ARM64 — that fallback works on Apple Silicon but **not** on other ARM64 hosts (Linux/ARM boxes included), where the container fails to launch outright. Keep the arch switch in place when touching that fixture; don't "simplify" it to a single hard-coded image.

On a non-Apple ARM machine, exclude the MsSql tests from local runs and validate them through GitHub Actions instead:

```bash
dotnet test DKNet.FW.sln --filter "FullyQualifiedName!~MsSqlStore"     # whole solution, minus MsSql
dotnet test AspNet/AspCore.Idempotency.NpgsqlStore.Tests               # Postgres and Redis
dotnet test AspNet/AspCore.Idempotency.RedisStore.Tests                # both run fine on ARM64
```

Postgres and Redis containers run natively on ARM64, and `NpgsqlStore` exercises the shared `DKNet.AspCore.Idempotency.Relational` base — so a change to the shared reservation path is still covered locally even when MsSql itself is excluded. Only MsSql-specific SQL goes unverified on that machine.

**Never delete, `[Skip]`, or otherwise disable the MsSql test project to make a local run go green** — it is that store's only coverage and it passes on CI. Exclude it at the command line, say so in the PR, and re-validate on the x64 runner below.

## The Svc.PdfGenerators / Chromium rule

`Svc.PdfGenerators.Tests` has the same x64 story as SQL Server, for a different reason: PuppeteerSharp fetches an **x64 Chromium** unconditionally. On a non-Apple ARM64 host the download succeeds and the *launch* fails:

```
PuppeteerSharp.ProcessException: Failed to launch browser!
x86_64-binfmt-P: Could not open '/lib64/ld-linux-x86-64.so.2'
```

That takes out roughly 14 of the 185 tests in that project — every one that renders a real PDF. The rest of the project still runs, so this is not a reason to skip the whole thing. Exclude nothing in code; run it locally, expect those launch failures on ARM64, and re-validate on the remote runner:

```bash
gh workflow run remote-tests.yml --ref <branch> -f project=Services/Svc.PdfGenerators.Tests
```

On Apple Silicon the x64 Chromium runs under Rosetta, so the whole suite passes locally there.

## Remote x64 verification (`remote-tests.yml`)

Run tests locally first: on x64 and Apple Silicon that covers the whole solution; on other ARM hosts it covers everything except the MsSql-backed tests excluded above, which **must** be re-validated here before merging a change to the idempotency stores. Dispatch the workflow whenever something genuinely can't run locally (Docker down, an image that won't pull, a restricted sandbox) or when a true x64 second opinion is wanted:

```bash
gh workflow run remote-tests.yml --ref <branch>                              # whole solution
gh workflow run remote-tests.yml --ref <branch> -f project=EfCore/EfCore.Extensions.Tests
gh workflow run remote-tests.yml --ref <branch> -f filter="FullyQualifiedName~DynamicAnd"
gh run watch <run-id> --exit-status        # or: gh run list --workflow remote-tests.yml
gh run view <run-id> --log-failed          # inline failed-step logs
gh run download <run-id> -n test-results   # pull trx + logs locally to fix code
```

It gives a clean pass/fail on tests only (no coverage/Sonar gate) and uploads a `test-results` artifact (`*.trx` + `build.log` + `test.log`) plus a failed-test step summary for AI debugging. The workflow must exist on the branch you dispatch (`--ref`) — it lives on the default branch `dev`, so a feature branch needs it merged/rebased in first — and it runs against the *pushed* branch, so commit before dispatching, not after.

## Pre-PR checklist (test-relevant excerpt)

- [ ] `dotnet build DKNet.FW.sln -c Debug` — zero warnings
- [ ] `dotnet test DKNet.FW.sln` — all green, or explicitly excluded and re-validated remotely per the MsSql rule above
- [ ] `dotnet format` clean
- [ ] Coverage held or improved; breaking changes called out in the PR body
- [ ] No generated artefacts committed (`nupkgs/`, `TestResults/`, `coverage-report*/`)

PRs state the problem, the change, and the validation (test results, coverage, SQL strings where relevant), plus any breaking change. Base branch is `dev`.
