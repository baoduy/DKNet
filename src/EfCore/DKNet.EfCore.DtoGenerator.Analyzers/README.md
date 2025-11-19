# DKNet.EfCore.DtoGenerator.Analyzers

This project contains Roslyn analyzers and code-fixes for the DKNet EF Core DTO generator.

Current rule(s)

- DKN001: Types annotated with `[GenerateDto]` must be declared `partial`.
    - Diagnostic id: `DKN001`
    - Severity: Warning (configured in the analyzer)
    - Rationale: The code generator produces additional members in a generated partial type; to allow that code to
      compile the source type must be `partial`.

What is in this project

- `GenerateDtoPartialAnalyzer` — Analyzer that reports `DKN001` when a `class` or `record` has a `[GenerateDto]`
  attribute but is not declared `partial`.
- `GenerateDtoPartialCodeFixProvider` — Code fix that inserts the `partial` modifier on the type while preserving
  modifier ordering.
- Release tracking files required by Roslyn analyzer tooling: `AnalyzerReleases.Shipped.md`,
  `AnalyzerReleases.Unshipped.md`.

How to use locally

1. Build the analyzer project (it will also restore required packages):

```bash
cd src/EfCore/DKNet.EfCore.DtoGenerator.Analyzers
dotnet restore
dotnet build
```

2. To enable the analyzer in the `DKNet.EfCore.DtoGenerator` project (development scenario) the solution is already
   wired to reference this analyzer project as an analyzer (see the `ProjectReference` in
   `DKNet.EfCore.DtoGenerator.csproj`). Opening the solution in your IDE (Visual Studio / Rider / VS Code with C#
   extension) will load the analyzer and show diagnostics in-source.

3. Quick manual validation:

- Open `DKNet.EfCore.DtoGenerator` (or any consuming project that includes the analyzer) and add a test type:

```csharp
[GenerateDto(typeof(SomeType))]
public class Product { }
```

You should see diagnostic `DKN001` on `Product` and a quick-fix lightbulb offering "Make type partial" which changes the
declaration to `public partial class Product { }`.

Running a full solution build

```bash
cd src
dotnet restore
dotnet build DKNet.FW.sln
```

Testing and unit tests

- This project does not currently include Roslyn analyzer unit tests. I recommend adding an xUnit test project that uses
  the Microsoft.CodeAnalysis.Testing packages (for example the `Microsoft.CodeAnalysis.CSharp.CodeFix.Testing.XUnit` and
  `Microsoft.CodeAnalysis.CSharp.Analyzer.Testing.XUnit` packages) to verify diagnostics and code fixes.

Packaging & Distribution

- To distribute this analyzer as a NuGet analyzer package, pack the analyzer project as a normal NuGet package and
  include the analyzer binaries under the `analyzers/dotnet/cs` folder in the package.

Notes for maintainers

- The analyzer project is configured as a Roslyn component and uses release-tracking files (`AnalyzerReleases.*`) to
  satisfy Roslyn analyzer rules.
- If you add additional analyzers, update `AnalyzerReleases.Unshipped.md` with their metadata.
- Keep the `EnforceExtendedAnalyzerRules` property in the analyzer project file so the analyzer project compiles cleanly
  with workspace references.

Contributing

- Follow the repository coding standards. Add tests for new diagnostics and code fixes.
- When adding new analyzers, increment the release-tracking files and include a short rationale.

License

- The analyzer follows the repository license (see the root `LICENSE` file).

