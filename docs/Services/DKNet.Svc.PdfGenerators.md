# DKNet.Svc.PdfGenerators

Converts HTML or Markdown into a PDF file using headless Chromium (PuppeteerSharp) and Markdig, with page layout,
header/footer, and margin control.

> [!NOTE]
> This package is a fork of Markdown2Pdf 2.x — see `src/Services/DKNet.Svc.PdfGenerators/THIRD-PARTY-NOTICES.md` for the
> upstream attribution. Several option types carried over from upstream are **not wired into the current
> `PdfGenerator`** pipeline; the **Applied** column in the configuration reference below marks exactly which
> options affect the output today.

## ✨ Why use it?

- **One call from Markdown to a PDF on disk.** `ConvertMarkdownFileAsync("notes.md")` writes `notes.pdf` next to it —
  no template engine, no browser lifecycle, no temp-file bookkeeping in your code.
- **Chromium fetches itself.** With no `ChromePath` configured, the first conversion downloads a matching build through
  PuppeteerSharp, so a container needs no browser baked in.
- **Real print layout.** Paper format, orientation, scale, margins, and repeated HTML header/footer templates come from
  Chromium's own print pipeline rather than CSS guesswork.
- **Several Markdown files, one document.** `ConvertMultipleMarkdownFilesAsync` concatenates sources in order — the
  shape a changelog or a multi-chapter export needs.

Reach for it to turn Markdown documentation, generated reports, or arbitrary HTML into a downloadable PDF.

## 🚀 Quick Start

```bash
dotnet add package DKNet.Svc.PdfGenerators
```

```csharp
using Microsoft.Extensions.DependencyInjection;
using DKNet.Svc.PdfGenerators;
using DKNet.Svc.PdfGenerators.Options;

builder.Services.AddPdfGenerator(new PdfGeneratorOptions
{
    IsLandscape = false,
    MarginOptions = new MarginOptions { Top = "2cm", Bottom = "2cm", Left = "1.5cm", Right = "1.5cm" }
});
```

```csharp
public sealed class ReportExporter(IPdfGenerator pdfGenerator)
{
    // writes ./reports/monthly.pdf and returns its full path
    public Task<string> ExportAsync(string markdownPath) =>
        pdfGenerator.ConvertMarkdownFileAsync(markdownPath, "reports/monthly.pdf");
}
```

`AddPdfGenerator(options)` builds one `PdfGenerator` immediately and registers it as a singleton `IPdfGenerator`; the
call is idempotent, and the options passed to the **first** call are the ones the instance keeps for the process
lifetime. Pass no argument for defaults.

The first conversion downloads a Chromium build via PuppeteerSharp's `BrowserFetcher` unless `ChromePath` points at an
existing install — expect a one-time delay in a fresh container or CI runner.

## 🧩 Features

### Conversion entry points (`IPdfGenerator`)

```csharp
Task<string> ConvertHtmlAsync(string htmlContent, string? outputPath = null);
Task<string> ConvertHtmlFileAsync(string htmlFilePath, string? outputPath = null);
Task<string> ConvertMarkdownFileAsync(string markdownFilePath, string? outputFilePath = null);
Task<FileInfo> ConvertMarkdownFileAsync(FileInfo markdownFile);
Task<string> ConvertMultipleMarkdownFilesAsync(string[] markdownFilePaths, string outputFilePath);
```

Four input shapes: a raw HTML string, an HTML file, a single Markdown file, or several Markdown files concatenated into
one PDF. There is no Razor-template or model-binding API — render your own HTML/Markdown text first, then convert it.

Output paths follow simple rules:

| Call | Output when the path argument is omitted |
|---|---|
| `ConvertHtmlAsync` / `ConvertHtmlFileAsync` | `{CurrentDirectory}/output_from_html.pdf` |
| `ConvertMarkdownFileAsync(string, …)` | the input path with its extension changed to `.pdf` |
| `ConvertMarkdownFileAsync(FileInfo)` | same, returned as a `FileInfo` |
| `ConvertMultipleMarkdownFilesAsync` | *(output path is required)* |

Markdown conversion resolves both paths to full paths and creates the output directory if it is missing.
`ConvertHtmlFileAsync` throws `FileNotFoundException` when the input HTML file does not exist.

### Markdown rendering

Markdown is rendered by Markdig with three extensions enabled:

- **Advanced extensions** (`UseAdvancedExtensions`) — GitHub-flavored tables, task lists, auto-links, footnotes,
  definition lists, and the rest of the Markdig "advanced" bundle.
- **YAML front matter** (`UseYamlFrontMatter`) — a leading `---` block is parsed as front matter instead of being
  rendered as content.
- **Emoji and smileys** (`UseEmojiAndSmiley`) — `:smile:` style shortcodes are converted.

The pipeline is fixed; there is no hook to add or remove Markdig extensions. Multi-file conversion joins the raw
Markdown sources with `Environment.NewLine` **before** rendering, so a construct may span the boundary between two
files.

### Page layout, header, and footer

Layout comes from `PdfGeneratorOptions` and is passed to Chromium's print options: `Format`, `IsLandscape`, `Scale`, and
`MarginOptions` (CSS-style strings such as `"1cm"`). `HeaderHtml`/`FooterHtml` are Chromium header/footer templates and
are only displayed when at least one of them is set:

```csharp
// Alias rather than `using PuppeteerSharp.Media;` — that namespace also declares a MarginOptions,
// so importing both it and DKNet.Svc.PdfGenerators.Options makes the name ambiguous (CS0104).
using PaperFormat = PuppeteerSharp.Media.PaperFormat;

new PdfGeneratorOptions
{
    Format = PaperFormat.Letter,
    IsLandscape = true,
    Scale = 0.9m,
    HeaderHtml = "<div style='font-size:10px;width:100%;text-align:center'>Monthly report</div>",
    FooterHtml = "<div style='font-size:10px;width:100%;text-align:center'>" +
                 "<span class='pageNumber'></span> / <span class='totalPages'></span></div>"
};
```

Background graphics are always printed (`PrintBackground = true`) and the page is rendered with the `screen` media type,
so `@media print` rules in your CSS do not apply.

### Container and CI behaviour

When the `CI` or `GITHUB_ACTIONS` environment variable equals `"true"`, Chromium is launched with `--no-sandbox
--disable-setuid-sandbox` — the flags a containerized runner normally needs. Chromium downloads are serialized behind a
process-wide semaphore, so concurrent first calls do not race each other.

### Serializable options round-trip

`SerializableOptions` mirrors `PdfGeneratorOptions` with flat, mostly string-typed properties (handy for JSON config or
a CLI) and converts via `ToPdfGeneratorOptions()`. Named values are resolved by reflection against the target type's
static properties, with a sensible fallback: an unknown `Theme` string becomes `Theme.Custom(value)`, an unknown
`ModuleOptions` string becomes `ModuleOptions.FromLocalPath(value)`, and an unknown `CodeHighlightTheme` string is
ignored rather than throwing.

```csharp
var options = JsonSerializer
    .Deserialize<SerializableOptions>(configJson)!
    .ToPdfGeneratorOptions();

builder.Services.AddPdfGenerator(options);
```

## ⚙️ Configuration reference

`PdfGeneratorOptions` — the **Applied** column says whether the current `PdfGenerator` pipeline reads the value:

| Option | Type | Default | Effect | Applied |
|---|---|---|---|---|
| `Format` | `PaperFormat` (PuppeteerSharp) | `PaperFormat.A4` | Paper size passed to Chromium. | ✅ |
| `IsLandscape` | `bool` | `false` | Landscape orientation. | ✅ |
| `Scale` | `decimal` | `1` | Render scale. | ✅ |
| `MarginOptions` | `MarginOptions?` (this package's, not PuppeteerSharp's) | `null` (Chromium defaults) | `Top`/`Bottom`/`Left`/`Right` as CSS strings, e.g. `"1cm"`. | ✅ |
| `HeaderHtml` | `string?` | `null` | Chromium header template; setting it (or `FooterHtml`) turns the header/footer on. | ✅ |
| `FooterHtml` | `string?` | `null` | Chromium footer template. | ✅ |
| `ChromePath` | `string?` | `null` | Path to an existing Chromium/Chrome; when `null`, a build is downloaded on demand. | ✅ |
| `Theme` | `Theme` | `Theme.Github` | `Theme.Github` / `.Latex` / `.None` / `.Custom(cssPath)`. | ❌ |
| `CodeHighlightTheme` | `CodeHighlightTheme` | `CodeHighlightTheme.Github` (`github.css`) | One of the 75 highlight.js stylesheets exposed as static properties. | ❌ |
| `ModuleOptions` | `ModuleOptions` | `ModuleOptions.Remote` | `Remote` / `None` / `FromLocalPath(path)` for MathJax, Mermaid, highlight.js assets. | ❌ |
| `TableOfContents` | `TableOfContentsOptions?` | `null` | `MinDepthLevel`/`MaxDepthLevel` (1–6, else `ArgumentOutOfRangeException`), `ListStyle`, `HasColoredLinks`, `PageNumberOptions`. | ❌ |
| `DocumentTitle` | `string?` | `null` | Document title. | ❌ |
| `MetadataTitle` | `string?` | `null` | PDF metadata title. | ❌ |
| `CustomHeadContent` | `string?` | `null` | Extra markup for the generated `<head>`. | ❌ |
| `KeepHtml` | `bool` | `false` | Keep the intermediate HTML for debugging. | ❌ |
| `EnableAutoLanguageDetection` | `bool` | `false` | Auto-detect code-block languages. | ❌ |

The ❌ rows are settable, serializable, and unit-tested as standalone types, but `PdfGenerator` renders Markdown to HTML
and hands it straight to Chromium — it never builds the theme, module, table-of-contents, or metadata pipeline that would
consume them. Treat them as inert until they are wired up; do not design a document around them.

### How the option types relate

`PdfGeneratorOptions` is the root; four of its properties are objects with their own options, and one type
mirrors the whole thing for serialization:

```text
PdfGeneratorOptions
├── MarginOptions?            applied — becomes Chromium's page margins
├── ModuleOptions             inert   — None | Remote | FromLocalPath(path) -> NodeModuleOptions
├── Theme                     inert   — Github | Latex | None | Custom(cssPath) -> CustomTheme
├── CodeHighlightTheme        inert   — 75 static stylesheet values
└── TableOfContentsOptions?   inert
    ├── ListStyle             (enum)
    ├── MinDepthLevel / MaxDepthLevel
    └── PageNumberOptions?
        └── Leader            (enum)

SerializableOptions --ToPdfGeneratorOptions()--> PdfGeneratorOptions
```

Read the tree as ownership, not as a pipeline: only `MarginOptions` reaches the output today. The rest are
reachable, settable, and round-trip through `SerializableOptions`, but no code reads them — see the
**Applied** column above.

#### `MarginOptions` (this package's own type, not PuppeteerSharp's)

| Option | Type | Default | Effect |
|---|---|---|---|
| `Top` | `string?` | `null` | CSS length for the top margin, e.g. `"2cm"`; `null` leaves Chromium's default. |
| `Bottom` | `string?` | `null` | As above, bottom edge. |
| `Left` | `string?` | `null` | As above, left edge. |
| `Right` | `string?` | `null` | As above, right edge. |

Each value is copied onto `PuppeteerSharp.Media.MarginOptions`; when `PdfGeneratorOptions.MarginOptions` is
`null`, an empty `MarginOptions` is passed instead, so Chromium's own defaults apply.

#### `TableOfContentsOptions`

| Option | Type | Default | Effect |
|---|---|---|---|
| `MinDepthLevel` | `int` | `1` | Shallowest heading level included. Assigning outside `1..6` throws `ArgumentOutOfRangeException`. |
| `MaxDepthLevel` | `int` | `6` | Deepest heading level included. Same `1..6` guard. |
| `ListStyle` | `ListStyle` | `ListStyle.OrderedDefault` | Marker style for TOC entries. |
| `HasColoredLinks` | `bool` | `false` | `true` leaves TOC links in the theme's link colour instead of styling them as plain text. |
| `PageNumberOptions` | `PageNumberOptions?` | `null` | Non-`null` asks for page numbers, which the type documents as a second render pass. |

`ListStyle` values are `None`, `OrderedDefault`, `Unordered`, and `Decimals` — note the plural on
`Decimals`, which the type's own XML example spells `Decimal`.

#### `PageNumberOptions`

| Option | Type | Default | Effect |
|---|---|---|---|
| `TabLeader` | `Leader` | `Leader.Dots` | Fill character between the entry and its page number. |

`Leader` values are `None`, `Dots`, `Underline`, and `Dashes`.

#### `ModuleOptions` — a closed set, not a constructor

`ModuleOptions`' constructor is `protected internal`, so a consumer cannot subclass it. The three ways to get
an instance are all on the type itself:

| Factory | `ModuleLocation` | Meaning |
|---|---|---|
| `ModuleOptions.None` | `None` | Load no extra modules — basic Markdown only. |
| `ModuleOptions.Remote` *(default)* | `Remote` | Load MathJax, Mermaid, highlight.js and the CSS themes from a CDN. |
| `ModuleOptions.FromLocalPath(path)` | `Custom` | Returns `NodeModuleOptions(path)`; the type documents `mathjax@3`, `mermaid@10`, `font-awesome`, `@highlightjs/cdn-assets@11`, `github-markdown-css` and `latex.css` as the packages that must be installed under `path`. |

`NodeModuleOptions` is public and exposes `ModulePath`; `ModuleLocation` is a public enum.

#### `Theme` — also a closed set

`Theme` is a `public abstract record` with no public constructor:

| Factory | Returns | Meaning |
|---|---|---|
| `Theme.Github` *(default)* | internal `PredefinedTheme(ThemeType.Github)` | GitHub Markdown styling. |
| `Theme.Latex` | internal `PredefinedTheme(ThemeType.Latex)` | LaTeX-like document styling. |
| `Theme.None` | internal `PredefinedTheme(ThemeType.None)` | No document theme. |
| `Theme.Custom(cssPath)` | public `CustomTheme(CssPath)` | Your own stylesheet. |

`PredefinedTheme` is `internal` — you can select a predefined theme but you cannot construct, pattern-match,
or extend one. `CustomTheme` and the `ThemeType` enum are public.

#### `SerializableOptions` — the string-typed mirror

Every property is nullable, and `ToPdfGeneratorOptions()` only overwrites a target property when its source is
non-`null`, so a partial document leaves the rest at their defaults:

| Option | Type | Resolved by |
|---|---|---|
| `ChromePath`, `CustomHeadContent`, `DocumentTitle`, `FooterHtml`, `HeaderHtml`, `MetadataTitle` | `string?` | Copied verbatim. |
| `EnableAutoLanguageDetection`, `IsLandscape`, `KeepHtml` | `bool?` | Copied when set. |
| `Scale` | `decimal?` | Copied when set. |
| `MarginOptions` | `MarginOptions?` | Copied by reference. |
| `TableOfContents` | `TableOfContentsOptions?` | Copied by reference. |
| `Format` | `string?` | Named static property on `PaperFormat`; an unknown name is **ignored**. |
| `Theme` | `string?` | Named static property on `Theme`, else `Theme.Custom(value)`. |
| `ModuleOptions` | `string?` | Named static property on `ModuleOptions`, else `ModuleOptions.FromLocalPath(value)`. |
| `CodeHighlightTheme` | `string?` | Named static property on `CodeHighlightTheme`; an unknown name is **ignored**. |

`InlineOptionsParser.ParseYamlFrontMatter(markdownFilePath)` reads the same shape out of a leading `---` or
`<!--` front-matter block using hyphenated YAML keys (`chrome-path`, `document-title`, …), and throws
`InvalidDataException` when the file has no closing delimiter. `PdfGenerator` never calls it — it is a helper
you invoke yourself before constructing options.

## 🧱 Where it fits

The conversion is short, and the diagram is mostly useful for what it does *not* contain — there is no theme,
module, table-of-contents, or metadata stage between Markdig and Chromium:

![Data-flow diagram: a Markdown file goes through Markdig's fixed pipeline into HTML text, or an HTML string enters at the same point; the HTML is set on a headless Chromium page with the screen media type, PdfOptions supplies format, margins and scale, and PdfAsync writes the PDF whose path is returned. Theme, table-of-contents and module options sit beside the HTML stage with no code path into it.](../diagrams/svc-pdfgenerators-conversion-pipeline.svg)

- **[DKNet.Svc.BlobStorage.Abstractions](./DKNet.Svc.BlobStorage.Abstractions.md)** — conversion returns a path or a
  `FileInfo`, so the natural next step is `IBlobService.SaveAsync` with `BinaryData.FromStream(File.OpenRead(path))`.
- **`DKNet.AspCore.Tasks`** — generating a PDF is slow and CPU-bound; run it in a background job rather than inside a
  request when the document is large.
- **Everything else in DKNet** is independent of this package: it depends on PuppeteerSharp and Markdig only, with no EF
  Core or messaging coupling.

## ⚠️ Gotchas & limits

- **Most option surface is currently inert.** Theme, code highlighting, module assets, table of contents, metadata,
  `KeepHtml`, and `EnableAutoLanguageDetection` are not read by `PdfGenerator` — see the Applied column above. Earlier
  revisions of this page documented `[TOC]` markers and theme switching as working features; they are not wired in
  source.
- **Two types are named `MarginOptions`.** `PdfGeneratorOptions.MarginOptions` is this package's
  `DKNet.Svc.PdfGenerators.Options.MarginOptions`; PuppeteerSharp declares its own in
  `PuppeteerSharp.Media`. Importing both namespaces makes the bare name ambiguous (`CS0104`) — alias the
  PuppeteerSharp types you need (`using PaperFormat = PuppeteerSharp.Media.PaperFormat;`) instead of importing
  the whole namespace.
- **No Razor/template-model API.** There is no `GenerateFromTemplateAsync` or model binding — render the string
  yourself, then convert.
- **`IConversionEvents` (`HtmlConverting`, `TemplateModelCreating`, `TempPdfCreated`) never fires from
  `PdfGenerator`.** Only the unwired helper services subscribe to it.
- **Chromium download.** The first call in a fresh environment downloads a Chromium build unless `ChromePath` is set.
  On ARM sandboxes this package's own test suite downloads an x86_64 build that cannot run — verify PDF changes on an
  x64 runner.
- **A browser per conversion.** Every call launches and disposes its own Chromium instance; there is no page or browser
  pool, so high-volume conversion needs your own queueing.
- **Options are frozen at registration.** The singleton is constructed by `AddPdfGenerator`, so per-request layout
  changes mean constructing `new PdfGenerator(options)` yourself instead of resolving `IPdfGenerator`.
- **Relative asset URLs in HTML do not resolve.** Content is set with `SetContentAsync`, not loaded from a base URL, so
  images and stylesheets need absolute URLs or inline data.
- Do not edit `THIRD-PARTY-NOTICES.md` or the test fixture `Sample.md` — the legal attribution and packaging tests
  depend on their exact contents.

## 🔗 Related packages

- [DKNet.Svc.BlobStorage.Abstractions](./DKNet.Svc.BlobStorage.Abstractions.md) – reach for it to store or serve the
  generated PDF instead of leaving it on local disk.
- [DKNet.Svc.Transformation](./DKNet.Svc.Transformation.md) – reach for it to fill placeholders in a Markdown or HTML
  template before conversion.
