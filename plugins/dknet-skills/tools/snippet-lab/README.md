# Snippet lab — compile-check C# examples in a skill

Compiles every ```csharp fence of a skill (SKILL.md + references/*.md) against the DKNet packages **as
published on nuget.org** — the surface a reader who runs `dotnet add package` actually gets, not `src/**/bin`,
which can be ahead of the last release. The source generators come in through those same packages, wired the
way a consumer gets them (analyzer + `GenerateDtoAttribute.cs` as contentFiles), with generated files emitted
under obj/Generated.

No local build needed: `dotnet build` restores everything. Versions live in the lab's own
`Directory.Packages.props` — bump `$(DKNetVersion)` (currently `13.1.2`) when a new DKNet release ships, and
re-run the lab to catch any example the new version invalidated.

One exception: `Aspire.Hosting.ServiceBus` is `IsPackable=false` and is not on nuget.org, so it is referenced
from `src/Aspire/.../bin/Debug/net10.0/` if that DLL exists. Without a local solution build the other twelve
skills still check clean and only `dknet-slimbus-cqrs`' `AddServiceBus` fences fail; `check.sh` prints a note.

    tools/snippet-lab/check.sh <skill-dir> [run-label]      # e.g. tools/snippet-lab/check.sh skills/dknet-efcore-specifications

    # every skill
    for d in skills/*/; do tools/snippet-lab/check.sh "$d"; done

Each run works in its own copy under `tools/lab-runs/<label>/` (git-ignored) so several checks can run in
parallel. Output: compiler errors (file:line) — the header comment of each Snippets/*.cs names the source fence.

The lab's assembly name is pinned to `Generated` in `SnippetLab.csproj`. `DKNet.SlimBus.Generators` emits CRUD
types into `$(AssemblyName).Crud` (falling back to the literal `Generated.Crud` only when the compilation has
no assembly name at all), and the skills' `[CrudAction]` examples import `Generated.Crud`. Renaming the
assembly — e.g. to label a run — puts the generated types in a namespace no published example can name, and
the fences fail with CS0246 on request types that were in fact generated correctly.

How fences are compiled (see extract.mjs):
- `using` lines are hoisted; everything else is placed in `namespace SnippetLab.<markdown-file>` — so fences
  in the SAME markdown file share one namespace and may reference each other's types (define `Product` once
  per file, reuse it), while different files never collide.
- A fence with a top-level type declaration (class/record/interface/struct/enum) is emitted as-is inside that namespace.
- A fence that is only statements (Program.cs style, `var builder = WebApplication.CreateBuilder(args);` ...)
  is wrapped in `static async Task RunAsync(string[] args)`. It must declare every variable it uses.
- A fence whose first line contains `no-compile` is skipped (use it for deliberately-wrong "don't do this" examples).
- The project mirrors an ASP.NET Core app's implicit usings (Microsoft.AspNetCore.Builder/Http/Routing,
  Microsoft.Extensions.DependencyInjection/Hosting/Logging/Configuration). Anything else needs an explicit `using`.
- Roslyn skips method-body binding while declaration errors exist, so fix declaration errors first and re-run.
