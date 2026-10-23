# Select a named job from command-line arguments

Part of [DKNet.AspCore.Extensions](index.md). `JobSelector.Select` finds the first argument that is not an option and matches it against a caller-supplied set of job names:

```csharp
using DKNet.AspCore.Extensions.Jobs;
var selection = JobSelector.Select(["--environment", "Development", "SEED"], ["seed", "warm"]);
Console.WriteLine(selection.JobName); // seed
Console.WriteLine(selection.IsRecognized); // True
```

![JobSelector skips option arguments, finds the first positional candidate and matches a canonical known name without executing it.](diagrams/job-selection.svg)

Arguments starting with `-` are skipped. An option without `=` also consumes the following argument; an inline option such as `--environment=Development` consumes only itself. Matching is case-insensitive and preserves the canonical known name.

`JobSelection` carries `RequestedJobName`, nullable canonical `JobName`, `IsRecognized`, and a copied `KnownJobNames` list. `HasJobName` means a candidate was found, even if unknown. No candidate produces null names and false recognition; an unknown candidate preserves the requested string but no canonical name.

## ⚠️ Gotchas & limits

- This helper selects only; it does not register, run or schedule a job and does not stop the host.
- Every dash-prefixed option without `=` consumes a following argument, including flags that an application might consider valueless. Choose argument conventions accordingly.
- Null argument/name collections throw. Known-name ordering decides the first case-insensitive match.
