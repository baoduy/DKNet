# DKNet.Svc.PdfGenerators

Converts HTML or Markdown into a PDF file using headless Chromium (PuppeteerSharp) and Markdig, with page layout, header/footer, and margin control.

> [!NOTE]
> This package is a fork of [Markdown2Pdf](https://github.com/Flayms/Markdown2Pdf) 2.x — see
> [THIRD-PARTY-NOTICES.md](../../src/Services/DKNet.Svc.PdfGenerators/THIRD-PARTY-NOTICES.md) for the upstream
> attribution. Several option types carried over from upstream are **not wired into the current `PdfGenerator`
> pipeline** — the feature pages below mark exactly which options affect the output today.

## ✨ Why use it?

- **One call from Markdown to a PDF on disk.** `ConvertMarkdownFileAsync("notes.md")` writes `notes.pdf` next to it — no template engine, no browser lifecycle, no temp-file bookkeeping in your code.
- **Chromium fetches itself.** With no `ChromePath` configured, the first conversion downloads a matching build through PuppeteerSharp, so a container needs no browser baked in.
- **Real print layout.** Paper format, orientation, scale, margins, and repeated HTML header/footer templates come from Chromium's own print pipeline rather than CSS guesswork.
- **Caller content cannot reach your network by default.** Rendered HTML runs with scripting off and a loopback/private-network request guard on, so untrusted Markdown or HTML cannot use the renderer for SSRF.

Reach for it to turn Markdown documentation, generated reports, or arbitrary HTML into a downloadable PDF.

## 🚀 Quick Start

Use a .NET 10 console project. Install the package, then replace `Program.cs` with the example.

```bash
dotnet new console -f net10.0 -n PdfDemo
cd PdfDemo
dotnet add package DKNet.Svc.PdfGenerators
```

```csharp
using DKNet.Svc.PdfGenerators;
using DKNet.Svc.PdfGenerators.Options;
using Microsoft.Extensions.DependencyInjection;

var services = new ServiceCollection();
services.AddPdfGenerator(new PdfGeneratorOptions
{
    MarginOptions = new MarginOptions { Top = "2cm", Bottom = "2cm", Left = "1.5cm", Right = "1.5cm" }
});
using var provider = services.BuildServiceProvider();

var generator = provider.GetRequiredService<IPdfGenerator>();
var path = await generator.ConvertMarkdownFileAsync("notes.md", "reports/monthly.pdf");
Console.WriteLine(path);
```

Run with `dotnet run`. `AddPdfGenerator(options)` builds one `PdfGenerator` immediately and registers it as a
singleton `IPdfGenerator`; the call is idempotent, and the options passed to the **first** call are the ones the
instance keeps for the process lifetime. The first conversion in a fresh environment downloads a Chromium build via
PuppeteerSharp's `BrowserFetcher` unless `ChromePath` points at an existing install — expect a one-time delay.

## 🏗️ Architecture

![The host application calls AddPdfGenerator to register a singleton PdfGenerator, then calls IPdfGenerator. PdfGenerator renders Markdown through a fixed Markdig pipeline, guards every outgoing request with RequestGuard, and drives a headless Chromium process that writes the PDF file.](diagrams/components.svg)

`PdfGeneratorSetup.AddPdfGenerator` registers one `PdfGenerator` as a singleton; it launches Chromium on first use
and relaunches it only if the process disconnects. Not pictured: `ThemeService`, `TableOfContentsCreator` and
`ModuleService` exist in the package but are never constructed by `PdfGenerator`, and there is no page or browser
pool — every conversion opens and closes its own Chromium page.

## 🔄 How it works

![A Markdown file goes through Markdig's fixed pipeline into HTML text, or an HTML string enters at the same point; the HTML is set on a headless Chromium page with the screen media type, PdfOptions supplies format, margins and scale, and PdfAsync writes the PDF whose path is returned. Theme, table-of-contents and module options sit beside the HTML stage with no code path into it.](diagrams/svc-pdfgenerators-conversion-pipeline.svg)

The conversion is short, and the diagram is mostly useful for what it does *not* contain — there is no theme,
module, table-of-contents, or metadata stage between Markdig and Chromium. `PrintBackground` is always `true` and
the page renders with the `screen` media type, so `@media print` CSS rules never apply.

## 🧩 Features

- [Convert Markdown to a PDF](convert-markdown-to-pdf.md) — render one or several Markdown files into a PDF through Markdig's fixed pipeline.
- [Convert HTML to a PDF](convert-html-to-pdf.md) — render a raw HTML string or an HTML file directly, with no Markdown step.
- [Page layout, header and footer](page-layout-header-footer.md) — control paper size, orientation, scale, margins, and repeated header/footer templates.
- [Browser lifecycle and request safety](browser-lifecycle-and-request-safety.md) — how Chromium is downloaded, launched and reused, and how every outgoing request is guarded.
- [Inert options and serialization](inert-options-and-serialization.md) — the theme, table-of-contents, module and metadata options that round-trip through `SerializableOptions` but never reach the rendered output.

## 🔗 Related packages

- [DKNet.Svc.BlobStorage.Abstractions](../DKNet.Svc.BlobStorage.Abstractions/index.md) — conversion returns a path or a `FileInfo`, so the natural next step is `IBlobService.SaveAsync` with `BinaryData.FromStream(File.OpenRead(path))`.
- [DKNet.Svc.Transformation](../DKNet.Svc.Transformation/index.md) — reach for it to fill placeholders in a Markdown or HTML template before conversion.
