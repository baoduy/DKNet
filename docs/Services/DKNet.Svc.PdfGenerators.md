# DKNet.Svc.PdfGenerators

Documentation-grade PDF generation: converts HTML or Markdown into PDF via headless Chromium (PuppeteerSharp), with
GitHub-flavored rendering (Markdig), a table of contents, syntax highlighting, and pluggable themes. It's a fork of
[Markdown2Pdf](https://github.com/) 2.x, extended with additional PDF generation options — see
`src/Services/DKNet.Svc.PdfGenerators/THIRD-PARTY-NOTICES.md` for the upstream attribution.

## When to reach for it

Reach for this package when you need to turn Markdown documentation, generated reports, or arbitrary HTML into a
downloadable PDF — README-to-PDF exports, invoices rendered from an HTML template, changelogs with a table of
contents.

## Install and minimal wiring

```bash
dotnet add package DKNet.Svc.PdfGenerators
```

```csharp
using DKNet.Svc.PdfGenerators;

builder.Services.AddPdfGenerator(); // IPdfGenerator, singleton
```

```csharp
public sealed class ReportExporter(IPdfGenerator pdfGenerator)
{
    public Task<string> ExportAsync(string markdownPath) =>
        pdfGenerator.ConvertMarkdownFileAsync(markdownPath, "report.pdf");
}
```

The first conversion auto-downloads a matching Chromium build via PuppeteerSharp's `BrowserFetcher` unless
`PdfGeneratorOptions.ChromePath` points at an existing install — expect a one-time download delay in a fresh
container or CI runner.

## Features

### Every conversion entry point (`IPdfGenerator`)

```csharp
Task<string> ConvertHtmlAsync(string htmlContent, string? outputPath = null);
Task<string> ConvertHtmlFileAsync(string htmlFilePath, string? outputPath = null);
Task<string> ConvertMarkdownFileAsync(string markdownFilePath, string? outputFilePath = null);
Task<FileInfo> ConvertMarkdownFileAsync(FileInfo markdownFile);
Task<string> ConvertMultipleMarkdownFilesAsync(string[] markdownFilePaths, string outputFilePath);
```

Four input shapes are supported: a raw HTML string, an HTML file, a single Markdown file, or several Markdown files
concatenated into one PDF. There is no Razor-template or model-binding API — feed it HTML/Markdown text, not a view.

### Markdown rendering extras

Markdig-based rendering supports GitHub-flavored Markdown plus:
- **Table of contents** — auto-inserted at a `[TOC]`, `[[_TOC_]]`, or `<!-- toc -->` marker; opt a heading out with
  `<!-- omit from toc -->` immediately above it. Configure via `PdfGeneratorOptions.TableOfContents`.
- **Syntax highlighting** — `CodeHighlightTheme` ships ~60 highlight.js themes as static members (`.Github` default,
  `.GithubDark`, `.Monokai`, `.Nord`, `.None`, …).
- **MathJax / Mermaid diagrams** — loaded via `ModuleOptions` (remote CDN by default, or a local npm install).

### Themes

`Theme` is the page's visual theme (distinct from `CodeHighlightTheme`, which only affects code blocks):
`Theme.Github` (default), `Theme.Latex`, `Theme.None`, or `Theme.Custom(cssPath)` for your own stylesheet.

### Table of contents depth control

```csharp
new TableOfContentsOptions
{
    MinDepthLevel = 1,   // 1-6, throws ArgumentOutOfRangeException outside that range
    MaxDepthLevel = 6,   // 1-6, same
    ListStyle = ListStyle.OrderedDefault, // None, OrderedDefault, Unordered, Decimals
    HasColoredLinks = false,
    PageNumberOptions = new PageNumberOptions { TabLeader = Leader.Dots }, // None, Dots, Underline, Dashes
};
```

### Remote vs. local module assets

```csharp
options.ModuleOptions = ModuleOptions.Remote; // default — loads MathJax/Mermaid/highlight.js from a CDN, needs internet
options.ModuleOptions = ModuleOptions.FromLocalPath("./node_modules"); // offline — expects mathjax@3, mermaid@10,
                                                                        // font-awesome, @highlightjs/cdn-assets@11,
                                                                        // github-markdown-css, latex.css installed there
```

Use `FromLocalPath` for air-gapped/offline PDF generation.

### Page layout options (`PdfGeneratorOptions`)

| Property | Default |
|---|---|
| `PaperFormat Format` | `PaperFormat.A4` |
| `bool IsLandscape` | `false` |
| `decimal Scale` | `1` |
| `MarginOptions? MarginOptions` | `null` (Puppeteer defaults) |
| `string? HeaderHtml` / `string? FooterHtml` | `null` |
| `string? DocumentTitle` / `string? MetadataTitle` | `null` |
| `string? CustomHeadContent` | `null` — inject extra `<head>` markup |
| `bool KeepHtml` | `false` — keep the intermediate HTML file for debugging |
| `bool EnableAutoLanguageDetection` | `false` |
| `string? ChromePath` | `null` — auto-download if unset |
| `CodeHighlightTheme CodeHighlightTheme` | `.Github` |
| `Theme Theme` | `Theme.Github` |
| `ModuleOptions ModuleOptions` | `ModuleOptions.Remote` |
| `TableOfContentsOptions? TableOfContents` | `null` |

`MarginOptions` (`Top`/`Bottom`/`Left`/`Right`, all `string?`, default `null`) accepts CSS-style values (`"1cm"`).

### Serializable options round-trip

`SerializableOptions` mirrors `PdfGeneratorOptions` with flat, string-typed properties (handy for JSON config or a
CLI) and converts back via `ToPdfGeneratorOptions()` — an invalid theme/format name falls back to the default rather
than throwing.

## Composing with other DKNet packages

Save the generated PDF with [`DKNet.Svc.BlobStorage.*`](./DKNet.Svc.BlobStorage.Abstractions.md) once it's on disk —
`ConvertMarkdownFileAsync` returns a file path/`FileInfo`, ready to hand to `IBlobService.SaveAsync`.

## Gotchas and limits

- **Chromium download.** The first call in a fresh environment downloads a Chromium build unless `ChromePath` is
  set; the download is serialized so concurrent first calls don't race each other, but budget for the delay in cold
  containers/CI. On ARM sandboxes this package's own test suite downloads an x86_64 Chromium build that can't run —
  verify PDF-generation changes on an x64 runner.
- **No Razor/template-model API.** Despite what earlier revisions of this page claimed, there is no
  `GenerateFromTemplateAsync` or model-binding step — render your Markdown/HTML string yourself, then convert it.
- **`IConversionEvents` (`HtmlConverting`, `TemplateModelCreating`, `TempPdfCreated`) is defined but not wired into
  `PdfGenerator`** in the current source — don't build on these events expecting them to fire.
- Do not edit `THIRD-PARTY-NOTICES.md` or the test fixture `Sample.md` — the legal attribution and packaging tests
  depend on their exact contents.
