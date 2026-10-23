# Convert HTML to a PDF

Part of [DKNet.Svc.PdfGenerators](index.md). `ConvertHtmlAsync` renders a raw HTML string, and
`ConvertHtmlFileAsync` reads an HTML file first — both skip the Markdig step entirely and hand the content straight
to Chromium.

```csharp
using DKNet.Svc.PdfGenerators;
using Microsoft.Extensions.DependencyInjection;

var services = new ServiceCollection();
services.AddPdfGenerator();
await using var provider = services.BuildServiceProvider();

var generator = provider.GetRequiredService<IPdfGenerator>();

// raw HTML string -> {CurrentDirectory}/output_from_html.pdf
var fromString = await generator.ConvertHtmlAsync("<h1>Report</h1><p>Generated directly from HTML.</p>");
Console.WriteLine(fromString);

// an HTML file on disk; throws FileNotFoundException if it does not exist
var fromFile = await generator.ConvertHtmlFileAsync("page.html", "page.pdf");
Console.WriteLine(fromFile);
```

Run with `dotnet run`.

![ConvertHtmlFileAsync reads the HTML file and throws FileNotFoundException if it is missing, then calls ConvertHtmlAsync; ConvertHtmlAsync hands the HTML straight to GeneratePdfFromHtmlAsync, which defaults the output path to output_from_html.pdf when none is given.](diagrams/convert-html-to-pdf.svg)

## ⚙️ Configuration reference

| Call | Output when the path argument is omitted |
|---|---|
| `ConvertHtmlAsync` | `{CurrentDirectory}/output_from_html.pdf` |
| `ConvertHtmlFileAsync` | same default; throws `FileNotFoundException` first if the input file is missing |

## 🧱 Where it fits

[Convert Markdown to a PDF](convert-markdown-to-pdf.md) renders Markdig output through this same Chromium pipeline;
[Page layout, header and footer](page-layout-header-footer.md) covers the `PdfGeneratorOptions` fields both paths
share. [Browser lifecycle and request safety](browser-lifecycle-and-request-safety.md) covers what happens to
`<img>`, `<script>`, and other requests the rendered HTML makes.

## ⚠️ Gotchas & limits

- **Relative asset URLs in HTML do not resolve.** Content is set with `SetContentAsync`, not loaded from a base URL, so images and stylesheets need absolute URLs or inline data (`data:` URIs).
- **No Razor/template-model API.** Render your own HTML string first — there is no model-binding entry point.
