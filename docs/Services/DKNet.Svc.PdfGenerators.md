# DKNet.Svc.PdfGenerators

A comprehensive PDF generation toolkit that combines HTML, Markdown, and Razor-friendly templates with
PuppeteerSharp-driven rendering. The package targets .NET 9.0 and focuses on documentation, reporting, and
knowledge-base publishing scenarios.

## 🧰 Feature Set

- **Multi-source rendering** – Convert Markdown, HTML fragments, or full Razor views into polished PDFs.
- **Themeable output** – Custom templates, typography, syntax highlighting, and table styling via embedded resources.
- **Rich front matter** – YAML metadata feeds document headers, cover pages, and footers automatically.
- **Table of contents** – Generate navigable TOCs with configurable depth, numbering, and bookmarks.
- **Code-friendly** – Built-in highlight.js integration for developer documentation.
- **Composable services** – Overridable services manage templates, assets, metadata, and PDF layout events.

## 🗂️ Project Structure

| Folder/File | Description |
|-------------|-------------|
| `PdfGenerator.cs` | High-level façade that orchestrates parsing, templating, and rendering |
| `Options/` | Strongly-typed configuration objects for layout, TOC, headers/footers, and theming |
| `Models/` | Data models for module descriptions, contributors, sections, and asset manifests |
| `Services/` | Internal services (resource loader, Markdown engine, template renderer, metadata composer) |
| `templates/` | Default HTML/CSS/JS assets embedded into the assembly |

## 🚀 Quick Start

```csharp
var options = new PdfGeneratorOptions
{
    Title = "DKNet Platform Overview",
    Theme = PdfTheme.Dark,
    TableOfContents = new TableOfContentsOptions { Enabled = true, MaxDepth = 3 }
};

var generator = new PdfGenerator(options, logger: logger);
await generator.GenerateFromMarkdownAsync(
    markdownPath: "architecture.md",
    outputPdfPath: "architecture.pdf",
    cancellationToken: cancellationToken);
```

## ⚙️ Configuration Highlights

### Content Sources

- `GenerateFromMarkdownAsync` – Parses Markdown via Markdig with extension pipelines for tables, emojis, and syntax code blocks.
- `GenerateFromHtmlAsync` – Accepts raw HTML strings or files for direct rendering.
- `GenerateFromTemplateAsync` – Feed strongly typed models into Razor-compatible templates before rendering.

### Layout & Theming

- `PdfThemeOptions` – Configure fonts, accent colours, syntax highlighting theme, and page background.
- `HeaderFooterOptions` – Custom HTML fragments rendered on every page; supports metadata tokens (page numbers, module info).
- `TableOfContentsOptions` – Toggle TOC visibility, numbering styles, and heading depth.
- `ModuleInformation` – Provide version numbers, changelog data, contributor lists, and release channels.

### Rendering Pipeline

1. Content is normalised (Markdown → HTML) and merged with YAML front matter metadata.
2. Templates from `templates/` are hydrated with module information and user options.
3. PuppeteerSharp spins up a headless Chromium instance to render the HTML to PDF with the specified paper size.
4. Post-processors update metadata, embed outlines, and wire up TOC links.

## 🧩 Extensibility

- **Custom Templates** – Replace embedded templates by supplying an `IResourceProvider` implementation or overriding paths via options.
- **Conversion Events** – Implement `IConversionEvents` to hook into lifecycle events (before/after render, asset injection).
- **Markdown Pipeline** – Register additional Markdig extensions (diagrams, math) through the options builder.
- **Asset Bundles** – Provide extra CSS/JS bundles via `AdditionalResources` on the options model.

## 🧱 Architectural Role

`DKNet.Svc.PdfGenerators` lives in the **Application Layer** of the Onion Architecture, orchestrating presentation-ready
assets for publication while keeping the **Domain Layer** agnostic of rendering concerns. Storage abstractions from the
Blob Storage packages can persist generated PDFs, and background jobs (see `DKNet.AspCore.Tasks`) can schedule batch exports.

## ✅ Best Practices

- Reuse a single `PdfGenerator` instance when producing multiple documents with identical configuration to reduce startup cost.
- Cache heavy assets (fonts, highlight themes) on disk or via CDN to minimise render time.
- Prefer Markdown sources with YAML front matter to drive metadata automatically.
- Run PDF generation inside dedicated background jobs or worker services to avoid blocking HTTP request threads.
- When deploying in containers, ensure Chromium dependencies required by PuppeteerSharp are included in the image.

## 🧪 Testing Guidance

Unit tests exercise Markdown parsing, option validation, and template selection. Integration tests leverage PuppeteerSharp
with mocked Chromium binaries to verify TOC output, metadata propagation, and code highlighting.
