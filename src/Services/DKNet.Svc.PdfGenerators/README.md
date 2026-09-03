# DKNet.Svc.PdfGenerators

[![NuGet](https://img.shields.io/nuget/v/DKNet.Svc.PdfGenerators)](https://www.nuget.org/packages/DKNet.Svc.PdfGenerators/)
[![NuGet Downloads](https://img.shields.io/nuget/dt/DKNet.Svc.PdfGenerators)](https://www.nuget.org/packages/DKNet.Svc.PdfGenerators/)
[![.NET](https://img.shields.io/badge/.NET-10.0-blue)](https://dotnet.microsoft.com/)
[![License](https://img.shields.io/badge/License-MIT-green.svg)](https://github.com/baoduy/DKNet/blob/main/LICENSE)

PDF generation from HTML or Markdown, via headless Chromium (PuppeteerSharp) and Markdig. Fork of
Markdown2Pdf 2.x — see `THIRD-PARTY-NOTICES.md` in the package for upstream attribution.

## Features

- `IPdfGenerator` — convert an HTML string, an HTML file, a Markdown file, or several Markdown files into one PDF
- Chromium print layout: paper format, orientation, scale, margins, and repeated HTML header/footer templates
- Chromium is downloaded on demand when no `ChromePath` is configured, so a container needs no browser baked in
- `--no-sandbox` is applied automatically when `CI` or `GITHUB_ACTIONS` is `"true"`

## Installation

```bash
dotnet add package DKNet.Svc.PdfGenerators
```

## Quick Start

```csharp
using DKNet.Svc.PdfGenerators;
using DKNet.Svc.PdfGenerators.Options;
using Microsoft.Extensions.DependencyInjection;

builder.Services.AddPdfGenerator(new PdfGeneratorOptions
{
    MarginOptions = new MarginOptions { Top = "2cm", Bottom = "2cm", Left = "1.5cm", Right = "1.5cm" }
});

public sealed class ReportExporter(IPdfGenerator pdfGenerator)
{
    // writes ./reports/monthly.pdf and returns its full path
    public Task<string> ExportAsync(string markdownPath) =>
        pdfGenerator.ConvertMarkdownFileAsync(markdownPath, "reports/monthly.pdf");
}
```

`AddPdfGenerator(PdfGeneratorOptions? options = null)` builds one `PdfGenerator` immediately and registers it
as a singleton `IPdfGenerator`. The call is idempotent, so the **first** call's options are the ones the
instance keeps for the process lifetime.

## Configuration — `PdfGeneratorOptions`

The **Applied** column says whether the current `PdfGenerator` pipeline reads the value. Several option types
carried over from upstream are settable and serializable but never read — do not design a document around them.

| Option | Type | Default | Effect | Applied |
|---|---|---|---|---|
| `Format` | `PaperFormat` (PuppeteerSharp) | `PaperFormat.A4` | Paper size passed to Chromium. | ✅ |
| `IsLandscape` | `bool` | `false` | Landscape orientation. | ✅ |
| `Scale` | `decimal` | `1` | Render scale. | ✅ |
| `MarginOptions` | `MarginOptions?` | `null` | `Top`/`Bottom`/`Left`/`Right` as CSS strings (`"1cm"`); `null` leaves Chromium's defaults. | ✅ |
| `HeaderHtml` | `string?` | `null` | Chromium header template; setting it (or `FooterHtml`) turns the header/footer on. | ✅ |
| `FooterHtml` | `string?` | `null` | Chromium footer template. | ✅ |
| `ChromePath` | `string?` | `null` | Path to an existing Chromium/Chrome; `null` downloads a build on demand. | ✅ |
| `Theme` | `Theme` | `Theme.Github` | `Github` / `Latex` / `None` / `Custom(cssPath)`. | ❌ |
| `CodeHighlightTheme` | `CodeHighlightTheme` | `CodeHighlightTheme.Github` | One of 75 highlight.js stylesheets exposed as static properties. | ❌ |
| `ModuleOptions` | `ModuleOptions` | `ModuleOptions.Remote` | `Remote` / `None` / `FromLocalPath(path)` for MathJax, Mermaid and highlight.js assets. | ❌ |
| `TableOfContents` | `TableOfContentsOptions?` | `null` | See the nested table below. | ❌ |
| `DocumentTitle` | `string?` | `null` | Document title. | ❌ |
| `MetadataTitle` | `string?` | `null` | PDF metadata title. | ❌ |
| `CustomHeadContent` | `string?` | `null` | Extra markup for the generated `<head>`. | ❌ |
| `KeepHtml` | `bool` | `false` | Keep the intermediate HTML for debugging. | ❌ |
| `EnableAutoLanguageDetection` | `bool` | `false` | Auto-detect code-block languages. | ❌ |

### Nested option types

`MarginOptions` — `Top`, `Bottom`, `Left`, `Right`, all `string?`, all `null` by default.

`TableOfContentsOptions`:

| Option | Type | Default | Effect |
|---|---|---|---|
| `MinDepthLevel` | `int` | `1` | Shallowest heading level; outside `1..6` throws `ArgumentOutOfRangeException`. |
| `MaxDepthLevel` | `int` | `6` | Deepest heading level; same guard. |
| `ListStyle` | `ListStyle` | `OrderedDefault` | `None`, `OrderedDefault`, `Unordered`, `Decimals`. |
| `HasColoredLinks` | `bool` | `false` | Leave TOC links in the theme's link colour. |
| `PageNumberOptions` | `PageNumberOptions?` | `null` | Non-`null` asks for page numbers. |

`PageNumberOptions` — `TabLeader` (`Leader`, default `Leader.Dots`; values `None`, `Dots`, `Underline`, `Dashes`).

`ModuleOptions` has a `protected internal` constructor; get an instance from `ModuleOptions.None`,
`ModuleOptions.Remote`, or `ModuleOptions.FromLocalPath(path)`. `Theme` has no public constructor; use
`Theme.Github`, `Theme.Latex`, `Theme.None`, or `Theme.Custom(cssPath)` — `PredefinedTheme` is `internal`, so a
predefined theme can be selected but not constructed or extended.

`SerializableOptions` mirrors `PdfGeneratorOptions` with nullable, mostly string-typed properties and converts
via `ToPdfGeneratorOptions()`; an unset property leaves the target at its default. Named values resolve by
reflection against the target type's static properties: an unknown `Theme` string becomes `Theme.Custom(value)`,
an unknown `ModuleOptions` string becomes `ModuleOptions.FromLocalPath(value)`, and an unknown `Format` or
`CodeHighlightTheme` string is ignored. `InlineOptionsParser.ParseYamlFrontMatter(path)` reads the same shape
out of a Markdown file's front matter using hyphenated keys.

## Documentation

Full feature reference, diagrams, and gotchas:
https://github.com/baoduy/DKNet/blob/main/docs/Services/DKNet.Svc.PdfGenerators.md
