# DKNet.Svc.PdfGenerators reference

| Field | Value |
|---|---|
| Area | Services |
| NuGet | `dotnet add package DKNet.Svc.PdfGenerators` |
| Docs | https://github.com/baoduy/DKNet/blob/dev/docs/Services/DKNet.Svc.PdfGenerators.md |
| Source | https://github.com/baoduy/DKNet/tree/dev/src/Services/DKNet.Svc.PdfGenerators |
| Depends on (DKNet) | none — no `ProjectReference` entries in the csproj |
| Depends on (3rd party) | `Markdig`, `PdfPig`, `PuppeteerSharp`, `YamlDotNet` |
| Target framework | `net10.0` |

## Purpose

Converts an HTML string/file or one-or-more Markdown files into a PDF, using Markdig to render Markdown to HTML
and a headless Chromium instance (via PuppeteerSharp) to print that HTML to PDF. Each conversion call launches
(or reuses) one Chromium browser owned by the `PdfGenerator` instance, sets the HTML as page content, and calls
Chromium's native print-to-PDF with the paper format/margins/scale/header-footer from `PdfGeneratorOptions`.

It is NOT a template engine — there is no Razor/model-binding API, so you render your own HTML/Markdown string
first, then convert. It is also NOT a place to look for theming, table-of-contents, code-highlighting, or
metadata behavior: those option types exist, serialize, and unit-test as standalone objects, but the shipped
`PdfGenerator` pipeline never reads them (see **Gotchas**).

## Entry points

| Call | Exact signature | Called on | Notes (lifetime, ordering, prerequisites) |
|---|---|---|---|
| `AddPdfGenerator` | `public static IServiceCollection AddPdfGenerator(this IServiceCollection services, PdfGeneratorOptions? options = null)` (namespace `Microsoft.Extensions.DependencyInjection` — ambient) | `IServiceCollection` | `TryAddSingleton<IPdfGenerator>` — idempotent; a second call with different options is silently ignored and the **first** call's options win for the process lifetime. Registered via a factory lambda so the container disposes the singleton (and its Chromium browser) on shutdown. |
| `new PdfGenerator(options)` | `public class PdfGenerator(PdfGeneratorOptions? options = null) : IPdfGenerator, IAsyncDisposable` | direct construction | Bypasses DI entirely — the only way to get per-call/per-request option variation, since the DI singleton freezes its options at registration. Implements `IAsyncDisposable`; dispose to close the browser it launched. |
| `IPdfGenerator.ConvertHtmlAsync` | `Task<string> ConvertHtmlAsync(string htmlContent, string? outputPath = null)` | instance method | `outputPath` defaults to `{CurrentDirectory}/output_from_html.pdf`. |
| `IPdfGenerator.ConvertHtmlFileAsync` | `Task<string> ConvertHtmlFileAsync(string htmlFilePath, string? outputPath = null)` | instance method | Throws `FileNotFoundException` if `htmlFilePath` does not exist; otherwise reads the file and delegates to `ConvertHtmlAsync`. |
| `IPdfGenerator.ConvertMarkdownFileAsync` (string) | `Task<string> ConvertMarkdownFileAsync(string markdownFilePath, string? outputFilePath = null)` | instance method | Resolves both paths to full paths; `outputFilePath` defaults to the input path with extension changed to `.pdf`; creates the output directory if missing. |
| `IPdfGenerator.ConvertMarkdownFileAsync` (FileInfo) | `Task<FileInfo> ConvertMarkdownFileAsync(FileInfo markdownFile)` | instance method | Thin wrapper: calls the string overload with `markdownFile.FullName` and wraps the result in a new `FileInfo`. |
| `IPdfGenerator.ConvertMultipleMarkdownFilesAsync` | `Task<string> ConvertMultipleMarkdownFilesAsync(string[] markdownFilePaths, string outputFilePath)` | instance method | `outputFilePath` is required (no default). Joins raw Markdown sources with `Environment.NewLine` **before** rendering — a Markdown construct can span two files' boundary. |
| `InlineOptionsParser.ParseYamlFrontMatter` | `public static async Task<PdfGeneratorOptions> ParseYamlFrontMatter(string markdownFilePath)` | static helper (`DKNet.Svc.PdfGenerators.Services`) | Not called by `PdfGenerator`  — you invoke it yourself, before constructing `PdfGeneratorOptions`, to read a leading `---`/`<!--` front-matter block. Throws `InvalidDataException` if the file has no matching closing delimiter; propagates a `DirectoryNotFoundException` (or similar) if the file itself doesn't exist, and any YAML deserialization exception on malformed YAML. |
| `SerializableOptions.ToPdfGeneratorOptions` | `public PdfGeneratorOptions ToPdfGeneratorOptions()` | instance method on `SerializableOptions` | Only overwrites a target property when the source property is non-null — a partial `SerializableOptions` leaves the rest at `PdfGeneratorOptions` defaults. |

## Public surface

### `DKNet.Svc.PdfGenerators`

| Type | Kind | Purpose | Key members |
|---|---|---|---|
| `IPdfGenerator` | interface | The conversion contract consumers depend on. | See **Entry points** table for all five methods. |
| `PdfGenerator` | class, `IAsyncDisposable` | The only implementation; owns one shared Chromium `IBrowser` per instance. | `PdfGenerator(PdfGeneratorOptions? options = null)`; the five `Convert*` methods; `ValueTask DisposeAsync()` — closes and disposes the shared browser. |
| `IConversionEvents` | interface | Declares three conversion-lifecycle events (`HtmlConverting`, `TemplateModelCreating`, `TempPdfCreated`) and `string? OutputFileName`. | **Never implemented or raised by `PdfGenerator`** — see Gotchas. Only the unwired internal helper services subscribe to `TemplateModelCreating`, and nothing constructs those services or an `IConversionEvents` implementation from `PdfGenerator`. |
| `AsyncConversionEventHandler<TEventArgs>` | delegate | `Task AsyncConversionEventHandler<TEventArgs>(object? sender, TEventArgs e) where TEventArgs : EventArgs` | Backs `IConversionEvents.TemplateModelCreating`. |
| `MarkdownEventArgs` | class : `EventArgs` | Carries mutable Markdown content for `HtmlConverting`. | `MarkdownEventArgs(string markdownContent)`; `string MarkdownContent { get; set; }`. |
| `TemplateModelEventArgs` | class : `EventArgs` | Carries the template-model dictionary for `TemplateModelCreating`. | `TemplateModelEventArgs(Dictionary<string,string> templateModel)`; `IDictionary<string,string> TemplateModel { get; }` (get-only reference, but the dictionary itself is mutable). |
| `PdfEventArgs` | class : `EventArgs` | Carries the temp PDF path for `TempPdfCreated`. | `PdfEventArgs(string pdfPath)`; `string PdfPath { get; }` (get-only). |

### `Microsoft.Extensions.DependencyInjection` (ambient namespace)

| Type | Kind | Purpose | Key members |
|---|---|---|---|
| `PdfGeneratorSetup` | static class | DI registration entry point. | `IServiceCollection AddPdfGenerator(this IServiceCollection services, PdfGeneratorOptions? options = null)`. |

### `DKNet.Svc.PdfGenerators.Options`

| Type | Kind | Purpose | Key members |
|---|---|---|---|
| `PdfGeneratorOptions` | class | Root options object passed to `PdfGenerator`/`AddPdfGenerator`. | See **Options & defaults** below for every property. |
| `MarginOptions` | class | This package's own margin type (distinct from `PuppeteerSharp.Media.MarginOptions`). | `string? Top/Bottom/Left/Right { get; set; }`, all default `null`. |
| `Theme` | `public abstract record`, no public constructor | Closed set of document themes. | Static `Theme Github/Latex/None`; `static Theme Custom(string cssPath)`. |
| `ThemeType` | public enum | Discriminates the internal theme record. | `None`, `Github`, `Latex`. |
| `ModuleOptions` | `public record`, `protected internal` constructor | Closed set describing where MathJax/Mermaid/highlight.js assets load from. | Static `ModuleOptions None/Remote`; `static ModuleOptions FromLocalPath(string modulePath)`; `ModuleLocation ModuleLocation { get; }`. |
| `ModuleLocation` | public enum | `None = 0`, `Remote`, `Custom`. | — |
| `CodeHighlightTheme` | `public sealed record` | 75 static factory properties total: 74 real highlight.js stylesheets (e.g. `Github`, `Monokai`, `Dark`, `Nord`, …), plus `None`. | `override string ToString()` returns the CSS filename (e.g. `"github.css"`); `None.ToString()` returns `""`. |
| `TableOfContentsOptions` | class | TOC generation options (unused by `PdfGenerator`). | `int MinDepthLevel { get; set; }` (default 1), `int MaxDepthLevel { get; set; }` (default 6) — both throw `ArgumentOutOfRangeException` outside `1..6`; `ListStyle ListStyle { get; set; } = ListStyle.OrderedDefault`; `bool HasColoredLinks { get; set; }`; `PageNumberOptions? PageNumberOptions { get; set; }`. |
| `ListStyle` | public enum | `None`, `OrderedDefault`, `Unordered`, `Decimals` (plural). | — |
| `PageNumberOptions` | class | `Leader TabLeader { get; set; } = Leader.Dots`. | — |
| `Leader` | public enum | `None`, `Dots`, `Underline`, `Dashes`. | — |
| `SerializableOptions` | class | Flat, nullable, mostly-string mirror of `PdfGeneratorOptions` for JSON/CLI round-tripping. | All properties nullable (see **Options & defaults**); `PdfGeneratorOptions ToPdfGeneratorOptions()`. |

### `DKNet.Svc.PdfGenerators.Services`

| Type | Kind | Purpose | Key members |
|---|---|---|---|
| `InlineOptionsParser` | static class | Reads `SerializableOptions`-shaped YAML out of a Markdown file's front matter. | `static Task<PdfGeneratorOptions> ParseYamlFrontMatter(string markdownFilePath)`. |
| `PropertyService` | static class | Reflection lookup of a named static property/field on a type, used to resolve string option values (e.g. `"Github"` → `CodeHighlightTheme.Github`). | `static bool TryGetPropertyValue<TContainer>(string propertyName, out object propertyValue)`; a generic-result overload throws `InvalidCastException` if the found member's type doesn't match. |
| `TemplateFiller` | static class | Simple `@(token)` template substitution. Not used by `PdfGenerator`'s current pipeline. | `static string FillTemplate(string template, Dictionary<string,string> model)`. |

Internal helper types (`ModuleService`, `ThemeService`, `MetadataService`, `TableOfContentsCreator`,
`EmbeddedResourceService`, `ModuleInformation`) exist only to explain the observable "options exist but do
nothing" behavior described in **Purpose**/**Gotchas** — none of them is part of the consumer-facing surface.

## Options & defaults

| Option | Type | Default | Effect | Applied by `PdfGenerator`? |
|---|---|---|---|---|
| `Format` | `PuppeteerSharp.Media.PaperFormat` | `PaperFormat.A4` | Paper size passed to Chromium's `PdfOptions.Format`. | Yes |
| `IsLandscape` | `bool` | `false` | `PdfOptions.Landscape`. | Yes |
| `Scale` | `decimal` | `1` | `PdfOptions.Scale`. | Yes |
| `MarginOptions` | `Options.MarginOptions?` | `null` | Copied field-by-field onto a fresh `PuppeteerSharp.Media.MarginOptions`; when `null`, an *empty* `PuppeteerSharp.Media.MarginOptions()` is passed (Chromium's own defaults apply). | Yes |
| `HeaderHtml` | `string?` | `null` | `PdfOptions.HeaderTemplate`; setting either this or `FooterHtml` sets `DisplayHeaderFooter = true`. | Yes |
| `FooterHtml` | `string?` | `null` | `PdfOptions.FooterTemplate`. | Yes |
| `ChromePath` | `string?` | `null` | `null` → downloads Chromium via `BrowserFetcher`; non-null → used directly as `LaunchOptions.ExecutablePath`, and the download step is skipped entirely. | Yes |
| `Theme` | `Theme` | `Theme.Github` | Selects a CSS theme. | No |
| `CodeHighlightTheme` | `CodeHighlightTheme` | `CodeHighlightTheme.Github` | Selects a highlight.js stylesheet. | No |
| `ModuleOptions` | `ModuleOptions` | `ModuleOptions.Remote` | Selects CDN vs local vs no extra JS/CSS modules. | No |
| `TableOfContents` | `TableOfContentsOptions?` | `null` | Configures TOC generation. | No |
| `DocumentTitle` | `string?` | `null` | Document title. | No |
| `MetadataTitle` | `string?` | `null` | PDF metadata title. | No |
| `CustomHeadContent` | `string?` | `null` | Extra `<head>` markup. | No |
| `KeepHtml` | `bool` | `false` | Keep intermediate HTML for debugging. | No |
| `EnableAutoLanguageDetection` | `bool` | `false` | Auto-detect code-block languages. | No |

`PdfGenerator`'s internal HTML-to-PDF step is the entire read-list for `Options`: `Format`, `IsLandscape`,
`MarginOptions`, `Scale`, `HeaderHtml`, `FooterHtml`, `ChromePath`. Nothing else on `PdfGeneratorOptions` is ever
dereferenced by the shipped pipeline. `PrintBackground` is hardcoded `true` (not an option), and the page's media
type is hardcoded to `MediaType.Screen`, so `@media print` CSS never applies.

## Usage patterns

### Register a process-wide singleton via DI

**When**: standard ASP.NET Core / generic-host app where every consumer uses the same layout.

```csharp
using DKNet.Svc.PdfGenerators;
using DKNet.Svc.PdfGenerators.Options;
using Microsoft.Extensions.DependencyInjection;

var services = new ServiceCollection();
services.AddPdfGenerator(new PdfGeneratorOptions
{
    IsLandscape = false,
    MarginOptions = new MarginOptions { Top = "2cm", Bottom = "2cm", Left = "1.5cm", Right = "1.5cm" }
});

var provider = services.BuildServiceProvider();
var exporter = new ReportExporter(provider.GetRequiredService<IPdfGenerator>());
var exported = await exporter.ExportAsync("notes.md");
```

`ReportExporter` wraps the resolved generator:

```csharp
using DKNet.Svc.PdfGenerators;

public sealed class ReportExporter(IPdfGenerator pdfGenerator)
{
    public Task<string> ExportAsync(string markdownPath) =>
        pdfGenerator.ConvertMarkdownFileAsync(markdownPath, "reports/monthly.pdf");
}
```

**Notes**: `AddPdfGenerator` is idempotent (`TryAddSingleton`) — a later call with different options is silently
dropped, so register it exactly once, early, with the options you actually want.

### Convert an HTML string directly

**When**: you already rendered HTML yourself and just need a PDF.

```csharp
using DKNet.Svc.PdfGenerators;

var generator = new PdfGenerator();
var html = "<h1>Hello HTML</h1><p>Test paragraph.</p>";
var pdfPath = await generator.ConvertHtmlAsync(html, "output/hello.pdf");
// pdfPath == "output/hello.pdf"
await generator.DisposeAsync();
```

**Notes**: omitting `outputPath` writes to `{CurrentDirectory}/output_from_html.pdf`. `ConvertHtmlFileAsync` throws
`FileNotFoundException` if the input file is missing.

### Convert a single Markdown file, letting the output path default

**When**: turning one Markdown doc into a same-named PDF next to it.

```csharp
using DKNet.Svc.PdfGenerators;

var generator = new PdfGenerator();
var markdownPath = "notes.md";
var pdfPath = await generator.ConvertMarkdownFileAsync(markdownPath); // writes notes.pdf
```

**Notes**: both input and (if supplied) output paths are resolved to full paths, and the output directory is
created if it doesn't exist. The `FileInfo` overload (`ConvertMarkdownFileAsync(FileInfo markdownFile)`) does the
same thing and hands back a `FileInfo` instead of a `string`.

### Concatenate several Markdown files into one PDF

**When**: a multi-chapter export or a changelog spanning several source files.

```csharp
using DKNet.Svc.PdfGenerators;

var generator = new PdfGenerator();
var result = await generator.ConvertMultipleMarkdownFilesAsync(
    ["chapters/intro.md", "chapters/setup.md"],
    "book.pdf");
```

**Notes**: sources are joined with `Environment.NewLine` **before** Markdown rendering — a fenced code block or
YAML front-matter block that isn't closed at the end of one file will bleed into the next. `outputFilePath` is
required here (no default).

### Full print-layout options with the PuppeteerSharp `PaperFormat` alias

**When**: you need a specific paper size, orientation, scale, and a running header/footer with page numbers.

```csharp
using DKNet.Svc.PdfGenerators;
using DKNet.Svc.PdfGenerators.Options;
// Alias avoids CS0104 with this package's own MarginOptions.
using PaperFormat = PuppeteerSharp.Media.PaperFormat;

var options = new PdfGeneratorOptions
{
    Format = PaperFormat.Letter,
    IsLandscape = true,
    Scale = 0.9m,
    MarginOptions = new MarginOptions { Top = "2cm", Bottom = "2cm", Left = "1.5cm", Right = "1.5cm" },
    HeaderHtml = "<div style='font-size:10px;width:100%;text-align:center'>Monthly report</div>",
    FooterHtml = "<div style='font-size:10px;width:100%;text-align:center'>" +
                 "<span class='pageNumber'></span> / <span class='totalPages'></span></div>"
};

var generator = new PdfGenerator(options);
var pdfPath = await generator.ConvertHtmlAsync("<h1>Report</h1>", "report.pdf");
```

**Notes**: `Options.MarginOptions` and `PuppeteerSharp.Media.MarginOptions` share a bare name — importing both
namespaces without an alias is `CS0104` ambiguous. `HeaderHtml`/`FooterHtml` being non-null is what turns
`DisplayHeaderFooter` on; there is no separate boolean switch.

### Load options from a YAML front-matter block or JSON, then convert

**When**: layout should come from data (a config file, or the Markdown document itself) rather than hardcoded C#.

```csharp
using System.Text.Json;
using DKNet.Svc.PdfGenerators;
using DKNet.Svc.PdfGenerators.Options;
using DKNet.Svc.PdfGenerators.Services;

// From the Markdown file's own front matter:
var optionsFromFrontMatter = await InlineOptionsParser.ParseYamlFrontMatter("report.md");

// Or from a JSON config:
var configJson = "{}";
var optionsFromJson = JsonSerializer
    .Deserialize<SerializableOptions>(configJson)!
    .ToPdfGeneratorOptions();

var generator = new PdfGenerator(optionsFromFrontMatter);
await generator.ConvertMarkdownFileAsync("report.md", "report.pdf");
```

**Notes**: `ParseYamlFrontMatter` throws `InvalidDataException` when the file's leading `---`/`<!--` block has no
matching closing delimiter (or none is present at all). `ToPdfGeneratorOptions()` leaves any property whose
`SerializableOptions` source is `null` at the `PdfGeneratorOptions` default — it never nulls out a default.

## Runtime behaviour

For every `Convert*` call, in order:

1. **Markdown path only**: the file is read and rendered to HTML by a `MarkdownPipeline` built once per
   `PdfGenerator` instance with `UseAdvancedExtensions()`, `UseYamlFrontMatter()`, and `UseEmojiAndSmiley()`.
   Multiple files are string-concatenated with `Environment.NewLine` first, then rendered as one document.
2. The HTML (or the caller's raw HTML for `ConvertHtmlAsync`) is handed to the internal PDF-generation step.
3. The instance's shared `IBrowser` is returned, launching one if none exists or the previous one is
   disconnected: Chromium is ensured first (a no-op if `ChromePath` is set; otherwise it takes a process-wide
   static semaphore and downloads via `BrowserFetcher`), then Puppeteer launches it headless and, when
   `CI`/`GITHUB_ACTIONS` env vars equal `"true"`, with `Args = ["--no-sandbox", "--disable-setuid-sandbox"]`. An
   instance-level semaphore serializes concurrent callers of the same `PdfGenerator` so only one browser is ever
   launched per instance.
4. A new page is opened, its content set with `SetContentAsync(htmlContent)` (not a base-URL load — see
   Gotchas), its media type forced to `MediaType.Screen`, and the PDF is written to `outputFilePath` with
   `PrintBackground` hardcoded `true`.
5. The page is disposed; the browser itself stays open on the instance for reuse by the next call.
6. `DisposeAsync()` (call it, or let the DI container do it on shutdown) closes and disposes the shared browser
   and the instance's semaphore.

## Diagnostics & exceptions

| Exception type | Severity | When | Fix |
|---|---|---|---|
| `FileNotFoundException` | error | `ConvertHtmlFileAsync` called with a path that doesn't exist. | Check `File.Exists` first, or catch it. |
| `InvalidDataException` | error | `InlineOptionsParser.ParseYamlFrontMatter` called on a file with no leading `---`/`<!--` block, or one with no matching closing delimiter. | Ensure the front-matter block is present and closed before calling. |
| `ArgumentOutOfRangeException` | error | Setting `TableOfContentsOptions.MinDepthLevel` or `MaxDepthLevel` outside `1..6`. | Keep both within `1..6`. (Note: this option is currently inert in `PdfGenerator` regardless.) |
| `InvalidCastException` | error | `PropertyService`'s generic-result lookup finds a static member by name whose declared type doesn't match the requested type. | Only reachable through `SerializableOptions.ToPdfGeneratorOptions()` with mismatched reflection targets; not user-facing in normal use. |
| (propagated) `PuppeteerSharp.ProcessException` | error | Chromium fails to launch — most commonly an x86_64 Chromium build on a non-Apple ARM64 host. | Run on x64 or Apple Silicon (Rosetta), or set `ChromePath` to an ARM-native Chrome install. |
| (propagated) I/O exceptions from `BrowserFetcher` | error | Chrome download races the package's own download lock, or network failure. | Set `ChromePath` to skip the download entirely in restricted environments. |

There are no analyzer `DiagnosticDescriptor`s in this package — it ships no Roslyn analyzer/generator.

## Gotchas (source-verified)

- **Most of `PdfGeneratorOptions` is inert.** `Theme`, `CodeHighlightTheme`, `ModuleOptions`, `TableOfContents`,
  `DocumentTitle`, `MetadataTitle`, `CustomHeadContent`, `KeepHtml`, `EnableAutoLanguageDetection` are settable and
  serializable but never read. Evidence: `PdfGenerator`'s HTML-to-PDF pipeline only touches `Format`,
  `IsLandscape`, `MarginOptions`, `Scale`, `HeaderHtml`, `FooterHtml`, `ChromePath`.
- **`IConversionEvents` never fires.** `PdfGenerator` does not implement it, hold a reference to an implementation
  of it, or construct any of the internal helper services that are the only types subscribing to
  `TemplateModelCreating`. A consumer who wires up `HtmlConverting`/`TemplateModelCreating`/`TempPdfCreated`
  handlers on some custom `IConversionEvents` implementation will see them never invoked by `PdfGenerator`.
- **Two `MarginOptions` types exist.** `DKNet.Svc.PdfGenerators.Options.MarginOptions` (this package, CSS-string
  properties) and `PuppeteerSharp.Media.MarginOptions` (numeric-string properties consumed by PuppeteerSharp
  itself). Importing both namespaces makes the bare name `MarginOptions` ambiguous (`CS0104`) — the package's own
  source aliases the PuppeteerSharp type to cope with this.
- **Options freeze at DI registration.** `AddPdfGenerator` builds one `PdfGenerator` and registers it as a
  singleton via `TryAddSingleton`; a later `AddPdfGenerator(otherOptions)` call is a no-op. Per-request layout
  changes require `new PdfGenerator(options)` directly instead of resolving `IPdfGenerator`.
- **A browser per `PdfGenerator` instance, reused across its calls — but a fresh instance means a fresh browser
  launch.** The shared browser is only reused while still connected; there is no cross-instance pool, so
  constructing many short-lived `PdfGenerator`s (instead of resolving the DI singleton) launches many Chromium
  processes. Multi-conversion services should hold one long-lived `PdfGenerator` (or the DI singleton), not `new`
  one per call.
- **Relative asset URLs in HTML never resolve.** Content is set with `SetContentAsync(htmlContent)`, which sets
  content directly with no base URL. Relative `<img src="./logo.png">` or `<link href="./style.css">` will not
  load; use absolute URLs or inline (base64) data.
- **Chromium download races if you bypass `PdfGenerator`'s own serialization.** The private Chrome-ensure step
  takes a process-wide static semaphore before calling into `BrowserFetcher` — a concurrency test in the
  package's own suite exists specifically because, without that lock, concurrent first-time conversions raced
  each other on the same downloaded zip file and threw `IOException`. Don't reach for
  `new BrowserFetcher().DownloadAsync()` yourself in parallel outside this lock.
- **`ModuleOptions` and `Theme` are closed hierarchies you cannot subclass.** `ModuleOptions`'s constructor is
  `protected internal` and `Theme` has no public constructor at all — only its own static factories
  (`Github`/`Latex`/`None`/`Custom`) produce instances. A consumer cannot add a fourth theme type or module
  location by inheritance.
- **`ListStyle.Decimals` is plural; `TableOfContentsOptions`'s own XML-doc example spells it `Decimal`.** Copying
  the doc-comment example verbatim fails to compile — the real enum member is `ListStyle.Decimals`.
- **On non-Apple ARM64, Chromium *launch* fails, not download.** PuppeteerSharp fetches an x64 Chromium build
  regardless of host architecture; the download succeeds, but launching it fails with
  `PuppeteerSharp.ProcessException` (`x86_64-binfmt-P: Could not open '/lib64/ld-linux-x86-64.so.2'`). Verify real
  PDF-rendering changes on x64 or Apple Silicon (Rosetta runs the x64 binary fine there), or set `ChromePath` to
  an ARM-native Chrome/Chromium install.

## Anti-patterns & hallucination traps

- **`GenerateFromTemplateAsync`, or any Razor/model-binding conversion method — does not exist.** There is no
  templating API; render the HTML/Markdown string yourself first (or use `DKNet.Svc.Transformation`).
- **Calling `EnsureChromeAsync` or `GetBrowserAsync` directly — both are `private`.** They are not reachable
  outside `PdfGenerator`, even via reflection-friendly test patterns; use the public `Convert*` methods.
- **Subscribing to `IConversionEvents.HtmlConverting`/`TemplateModelCreating`/`TempPdfCreated` expecting them to
  fire during a `PdfGenerator` conversion.** They never do (see Gotchas) — don't build a feature that depends on
  intercepting Markdown-to-HTML or template-model creation through this interface.
- **Expecting `[TOC]`, `[[_TOC_]]`, or `<!-- toc -->` markers in Markdown to produce a table of contents.** The
  regex and rendering logic exist in an internal helper type, but nothing in `PdfGenerator` constructs or invokes
  it.
- **Expecting `Theme`/`CodeHighlightTheme`/`ModuleOptions`/`TableOfContents` to change the rendered PDF.** They do
  not — see **Options & defaults**' Applied column.
- **`new PuppeteerSharp.Media.MarginOptions()` where `DKNet.Svc.PdfGenerators.Options.MarginOptions` was intended**
  (or vice versa) — wrong namespace, and if both are imported unaliased it's `CS0104`, not a silent wrong-type bug.
- **Constructing the internal predefined-theme type directly** — it is `internal`; use `Theme.Github`/`.Latex`/
  `.None`.
- **Subclassing `ModuleOptions`** — its constructor is `protected internal`; use the static factories.
- **Hand-writing a Chromium browser-pool/queue inside this package's abstractions** — `PdfGenerator` deliberately
  launches one browser per instance with no pool; that's a consumer-side concern (see Gotchas), not something to
  patch into this package's types.

## Composes with

| Package | Rule |
|---|---|
| `DKNet.Svc.BlobStorage.Abstractions` | Reach for it right after conversion — `Convert*` returns a path or `FileInfo`; pass that to `IBlobService.SaveAsync(new BlobDetails.BlobData(fileName, BinaryData.FromStream(File.OpenRead(path))))` instead of leaving the PDF on local disk. The real signature is `Task<string> SaveAsync(BlobDetails.BlobData blob, CancellationToken cancellationToken = default)` — it takes a `BlobDetails.BlobData` record, not a bare `BinaryData`. See `dknet-blob-storage`. |
| `DKNet.AspCore.Tasks` | Reach for it to run the conversion as a background job — PDF generation is slow and CPU-bound (a real browser launch + print), so don't do it inline inside a request handler for large documents. See `dknet-aspcore-api`. |
| `DKNet.Svc.Transformation` | Reach for it to fill placeholders in a Markdown/HTML template *before* handing the result to this package for conversion — this package has no templating of its own. |

This package has no `ProjectReference` on any other DKNet package and no EF Core or SlimBus/messaging coupling —
it depends only on PuppeteerSharp, Markdig, PdfPig, and YamlDotNet.

## Testing notes

Stack: xUnit + Shouldly (plain xUnit `Assert.*` also appears in a few older test files — both styles coexist).
No general TestContainers/Docker dependency; a generated PDF is verified by existence + non-zero length, not by
parsing its content.

- **A shared Chrome-warmup fixture runs one warmup conversion before the tests in its collection** so Chrome is
  downloaded once and reused — but not every PDF-rendering test file joins that collection. Don't assume an
  unfamiliar PDF-rendering test is already sharing the warmup; check for the collection attribute before adding
  a new one.
- **Concurrency/race tests deliberately run outside that shared collection**, so their concurrently-constructed
  `PdfGenerator` instances genuinely race the process-wide Chrome download lock, asserting no `IOException`.
- **DI tests** build a bare `ServiceCollection`, call `AddPdfGenerator()`, and assert singleton identity and a
  single registration count even after calling `AddPdfGenerator()` twice — the idempotency behavior described in
  **Gotchas**.
- **`ChromePath` tests** locate an already-installed Chrome via `BrowserFetcher().GetInstalledBrowsers()` rather
  than hardcoding a path — copy this pattern if you need a real Chrome path in a test.
- **YAML front-matter tests** cover both `---`/`---` and `<!-- ... -->` delimiter styles, an incomplete block
  (`InvalidDataException`), invalid YAML syntax, and a nonexistent file — mirror these cases for any new parsing
  logic in this area.
