# DKNet.Svc.PdfGenerators

Converts HTML or Markdown into a PDF file using headless Chromium (PuppeteerSharp) and Markdig, with page layout,
header/footer, and margin control.

> This package is a fork of [Markdown2Pdf](https://github.com/Flayms/Markdown2Pdf) 2.x — see
> [THIRD-PARTY-NOTICES.md](https://github.com/baoduy/DKNet/blob/main/src/Services/DKNet.Svc.PdfGenerators/THIRD-PARTY-NOTICES.md)
> for the upstream attribution.

## ✨ Why use it?

- **One call from Markdown to a PDF on disk.** `ConvertMarkdownFileAsync("notes.md")` writes `notes.pdf` next to it — no template engine, no browser lifecycle, no temp-file bookkeeping in your code.
- **Chromium fetches itself.** With no `ChromePath` configured, the first conversion downloads a matching build through PuppeteerSharp, so a container needs no browser baked in.
- **Real print layout.** Paper format, orientation, scale, margins, and repeated HTML header/footer templates come from Chromium's own print pipeline rather than CSS guesswork.
- **Caller content is fenced from your network by default.** Rendered HTML runs with scripting off, and a request guard blocks loopback and private-network addresses. DNS rebinding is not closed — see [browser lifecycle and request safety](https://github.com/baoduy/DKNet/blob/main/docs/DKNet.Svc.PdfGenerators/browser-lifecycle-and-request-safety.md).

## 🚀 Quick Start

```bash
dotnet add package DKNet.Svc.PdfGenerators
echo "# Hello PDF" > notes.md
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
await using var provider = services.BuildServiceProvider();

var generator = provider.GetRequiredService<IPdfGenerator>();
var path = await generator.ConvertMarkdownFileAsync("notes.md", "reports/monthly.pdf");
Console.WriteLine(path);
```

Run with `dotnet run`.

## Documentation

| Guide | Read when |
|---|---|
| [Package overview](https://github.com/baoduy/DKNet/blob/main/docs/DKNet.Svc.PdfGenerators/index.md) | Start with architecture and the end-to-end flow. |
| [Convert Markdown to a PDF](https://github.com/baoduy/DKNet/blob/main/docs/DKNet.Svc.PdfGenerators/convert-markdown-to-pdf.md) | Render one or several Markdown files into a PDF. |
| [Convert HTML to a PDF](https://github.com/baoduy/DKNet/blob/main/docs/DKNet.Svc.PdfGenerators/convert-html-to-pdf.md) | Render a raw HTML string or file directly, with no Markdown step. |
| [Page layout, header and footer](https://github.com/baoduy/DKNet/blob/main/docs/DKNet.Svc.PdfGenerators/page-layout-header-footer.md) | Control paper size, orientation, scale, margins, and repeated header/footer templates. |
| [Browser lifecycle and request safety](https://github.com/baoduy/DKNet/blob/main/docs/DKNet.Svc.PdfGenerators/browser-lifecycle-and-request-safety.md) | How Chromium is downloaded, launched and reused, and how outgoing requests are guarded. |
| [Inert options and serialization](https://github.com/baoduy/DKNet/blob/main/docs/DKNet.Svc.PdfGenerators/inert-options-and-serialization.md) | The theme, table-of-contents, module and metadata options that have no effect on the rendered output. |
