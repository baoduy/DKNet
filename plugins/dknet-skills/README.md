# dknet-skills

Agent skills that teach coding agents the [DKNet](https://github.com/baoduy/DKNet) NuGet packages: which package
solves which problem, the exact DI and EF Core wiring, specifications and dynamic predicates, domain events and the
`SaveChanges` interceptor family, CQRS with SlimMessageBus, the source generators, idempotent endpoints, blob storage
and the service utilities.

The plugin is a [Claude Code plugin](https://code.claude.com/docs/en/plugins) **and** a set of
[Agent Skills](https://agentskills.io) (`skills/<name>/SKILL.md`), so the same files work in Claude Code, Cursor,
Codex, Copilot and every other agent the `skills` CLI supports. Every skill is written from the DKNet source and
docs, every API name is verified against `src/`, and every C# example is compiled against the DKNet assemblies
before release.

## Install

**Claude Code** (marketplace):

```text
claude plugin marketplace add baoduy/DKNet
claude plugin install dknet-skills@dknet
```

Inside a Claude Code session the same commands are `/plugin marketplace add baoduy/DKNet` and
`/plugin install dknet-skills@dknet`. Skills then load on demand as `dknet-skills:<skill-name>`.

**Any agent** (Agent Skills CLI, installs into `.claude/skills/`, `.agents/skills/`, `.cursor/skills/` … as chosen):

```bash
npx skills add baoduy/DKNet                      # pick skills and agents interactively
npx skills add baoduy/DKNet --all                # every skill, every detected agent
npx skills add baoduy/DKNet -s dknet-efcore-specifications -a claude-code -g
```

**Claude Code from npm** (no marketplace; pins the version with your project):

```bash
npm i -D @drunkcoding/dknet-skills
claude --plugin-dir node_modules/@drunkcoding/dknet-skills
```

**Working on DKNet itself** (this repository): `claude --plugin-dir plugins/dknet-skills`.

## Skills

Start with `dknet-packages`; it routes a need to the package and to the skill that owns it.

| Skill | Teaches |
|---|---|
| `dknet-packages` | Which package for which need, the wiring order for a new API, registration conventions, removed and renamed APIs. |
| `dknet-efcore-domain-model` | `DKNet.EfCore.Abstractions`, `DKNet.EfCore.Extensions`, `DKNet.EfCore.Relational.Helpers` — entity base classes, `UseAutoConfigModel`, global filters, seeding, sequences. |
| `dknet-efcore-specifications` | `DKNet.EfCore.Specifications` — `Specification<TEntity>`, `IRepositorySpec`, the dynamic predicate builder, paging. |
| `dknet-efcore-save-pipeline` | `DKNet.EfCore.Hooks`, `DKNet.EfCore.Events`, `DKNet.EfCore.AuditLogs` — before/after-save hooks, domain-event dispatch, audit trail. |
| `dknet-efcore-data-security` | `DKNet.EfCore.DataAuthorization`, `DKNet.EfCore.Encryption` — row-level ownership filter, transparent column encryption. |
| `dknet-codegen` | `DKNet.EfCore.DtoGenerator`, `DKNet.SlimBus.Generators` — `[GenerateDto]`, `[CrudCreate]`/`[CrudUpdate]`/`[CrudAction]`, diagnostics. |
| `dknet-slimbus-cqrs` | `DKNet.SlimBus.Extensions` (+ `Aspire.Hosting.ServiceBus`) — handlers, auto-save, events onto the bus. |
| `dknet-aspcore-api` | `DKNet.AspCore.Extensions`, `DKNet.AspCore.Tasks` — endpoint groups, `Result` to `ProblemDetails`, `[FromClaim]`, start-up jobs. |
| `dknet-idempotency` | `DKNet.AspCore.Idempotency` and its MsSql, Npgsql and Redis stores. |
| `dknet-blob-storage` | `DKNet.Svc.BlobStorage.*` — `IBlobService` with Azure, S3 and local adapters. |
| `dknet-services` | `DKNet.Svc.Encryption`, `DKNet.Svc.PdfGenerators`, `DKNet.Svc.Transformation`. |
| `dknet-core-utilities` | `DKNet.Fw.Extensions`, `DKNet.RandomCreator`. |
| `dknet-testing` | Testing code built on DKNet (TestContainers, SQL assertions, generated endpoints) and running the DKNet repo's own tests. |

Each skill keeps its `SKILL.md` short and puts the full per-package reference (public surface, options, runtime
behaviour, gotchas) in `references/<PackageId>.md`, loaded only when needed.

## Layout

```text
.claude-plugin/marketplace.json        # marketplace "dknet" (repo root) → ./plugins/dknet-skills
plugins/dknet-skills/                  # = the npm package @drunkcoding/dknet-skills
├── .claude-plugin/plugin.json         # plugin manifest (version stamped at release)
├── package.json                       # npm metadata (placeholder version 0.0.0)
├── scripts/sync-version.mjs           # copies package.json version into plugin.json (npm "version" hook)
├── README.md, LICENSE
└── skills/<skill-name>/
    ├── SKILL.md                       # Agent Skills frontmatter + instructions (<= 500 lines)
    └── references/<PackageId>.md      # verified per-package reference
```

## Validate

```bash
claude plugin validate . --strict                       # marketplace + plugin manifests (repo root)
claude plugin validate plugins/dknet-skills --strict    # plugin + skills
uvx --from skills-ref agentskills validate plugins/dknet-skills/skills/<skill-name>
npx skills add ./ --list                                # what the skills CLI will discover
(cd plugins/dknet-skills && npm pack --dry-run)         # what the npm package will ship
```

## Release

The plugin is versioned **with DKNet**: every NuGet release of the framework also publishes
[`@drunkcoding/dknet-skills`](https://www.npmjs.com/package/@drunkcoding/dknet-skills) to npmjs.com with the same
version number. The `publish-npm` job in `.github/workflows/dotnet-publish.yml` runs after the NuGet job on `main`,
stamps that job's version into `package.json` and `.claude-plugin/plugin.json` (`npm version <version>` →
`scripts/sync-version.mjs`), validates the skills, and publishes with provenance. A version already on npm is
skipped. There is no separate tag or GitHub release: DKNet's `v<version>` release marks the commit.

The repository therefore carries the placeholder `0.0.0` and nothing is bumped by hand. The job authenticates
with [npm Trusted Publishing](https://docs.npmjs.com/trusted-publishers), so there is no `NPM_TOKEN` secret and
nothing to rotate — it mints a short-lived OIDC credential from the workflow's `id-token: write` permission, and
npm attaches the provenance attestation automatically. The trusted publisher is configured once on npmjs.com
(package → Settings → Trusted Publisher → GitHub Actions, repository `baoduy/DKNet`, workflow filename
`dotnet-publish.yml`, no environment); npm matches the workflow filename exactly, so renaming the workflow means
updating that setting too.

To test the npm path without releasing anything, dispatch the workflow on `dev`:

```bash
gh workflow run dotnet-publish.yml --ref dev
```

On `dev` the NuGet publish and the GitHub release stay off; the npm job publishes `<version>-dev.<run>` under the
`dev` dist-tag (`npm i -D @drunkcoding/dknet-skills@dev`), which never collides with a `main` release.

## Keeping skills true

Skills are derived from `docs/<Area>/<Package>.md` and `src/`. When a package's public API or behaviour changes,
update the owning skill (`SKILL.md` and `references/<PackageId>.md`) in the same pull request as `docs/`.

## License

MIT
