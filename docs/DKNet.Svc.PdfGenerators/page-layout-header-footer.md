# Page layout, header and footer

Part of [DKNet.Svc.PdfGenerators](index.md). `PdfGeneratorOptions` carries the fields that become Chromium's print
options: paper format, orientation, scale, margins, and a repeated HTML header/footer template.

```csharp
using DKNet.Svc.PdfGenerators;
using DKNet.Svc.PdfGenerators.Options;
using Microsoft.Extensions.DependencyInjection;
// Alias rather than `using PuppeteerSharp.Media;` -- that namespace also declares a MarginOptions,
// so importing both it and DKNet.Svc.PdfGenerators.Options makes the name ambiguous (CS0104).
using PaperFormat = PuppeteerSharp.Media.PaperFormat;

var services = new ServiceCollection();
services.AddPdfGenerator(new PdfGeneratorOptions
{
    Format = PaperFormat.Letter,
    IsLandscape = true,
    Scale = 0.9m,
    MarginOptions = new MarginOptions { Top = "2cm", Bottom = "2cm", Left = "1.5cm", Right = "1.5cm" },
    HeaderHtml = "<div style='font-size:10px;width:100%;text-align:center'>Monthly report</div>",
    FooterHtml = "<div style='font-size:10px;width:100%;text-align:center'>" +
                 "<span class='pageNumber'></span> / <span class='totalPages'></span></div>"
});
await using var provider = services.BuildServiceProvider();

var generator = provider.GetRequiredService<IPdfGenerator>();
var path = await generator.ConvertMarkdownFileAsync("notes.md", "reports/monthly.pdf");
Console.WriteLine(path);
```

`HeaderHtml`/`FooterHtml` are Chromium header/footer templates and are only displayed when at least one of them is
set. Background graphics are always printed (`PrintBackground = true`) and the page is rendered with the `screen`
media type, so `@media print` rules in your CSS do not apply — neither of those is configurable.

![PdfGeneratorOptions' layout fields (Format, IsLandscape, Scale, MarginOptions, HeaderHtml, FooterHtml) are read inside GeneratePdfFromHtmlAsync, copied onto Chromium's PdfOptions, and either toggle DisplayHeaderFooter or flow straight through; PrintBackground is always true regardless of any option.](diagrams/page-layout-header-footer.svg)

## ⚙️ Configuration reference

| Option | Type | Default | Effect | Applied |
|---|---|---|---|---|
| `Format` | `PaperFormat` (PuppeteerSharp) | `PaperFormat.A4` | Paper size passed to Chromium. | ✅ |
| `IsLandscape` | `bool` | `false` | Landscape orientation. | ✅ |
| `Scale` | `decimal` | `1` | Render scale. | ✅ |
| `MarginOptions` | `MarginOptions?` (this package's, not PuppeteerSharp's) | `null` (Chromium defaults) | `Top`/`Bottom`/`Left`/`Right` as CSS strings, e.g. `"1cm"`. | ✅ |
| `HeaderHtml` | `string?` | `null` | Chromium header template; setting it (or `FooterHtml`) turns the header/footer on. | ✅ |
| `FooterHtml` | `string?` | `null` | Chromium footer template. | ✅ |

### `MarginOptions` (this package's own type, not PuppeteerSharp's)

| Option | Type | Default | Effect |
|---|---|---|---|
| `Top` | `string?` | `null` | CSS length for the top margin, e.g. `"2cm"`; `null` leaves Chromium's default. |
| `Bottom` | `string?` | `null` | As above, bottom edge. |
| `Left` | `string?` | `null` | As above, left edge. |
| `Right` | `string?` | `null` | As above, right edge. |

Each value is copied onto `PuppeteerSharp.Media.MarginOptions`; when `PdfGeneratorOptions.MarginOptions` is `null`,
an empty `MarginOptions` is passed instead, so Chromium's own defaults apply.

## 🧱 Where it fits

This options surface applies to both [Convert Markdown to a PDF](convert-markdown-to-pdf.md) and
[Convert HTML to a PDF](convert-html-to-pdf.md) — both end at the same `GeneratePdfFromHtmlAsync` call. The
`Theme`, `TableOfContents`, `CodeHighlightTheme` and `ModuleOptions` fields on the same `PdfGeneratorOptions` do
**not** reach this render — see [Inert options and serialization](inert-options-and-serialization.md).

## ⚠️ Gotchas & limits

- **Two types are named `MarginOptions`.** `PdfGeneratorOptions.MarginOptions` is this package's `DKNet.Svc.PdfGenerators.Options.MarginOptions`; PuppeteerSharp declares its own in `PuppeteerSharp.Media`. Importing both namespaces makes the bare name ambiguous (`CS0104`) — alias the PuppeteerSharp types you need instead of importing the whole namespace.
- **`PrintBackground` and the media type are fixed.** `PrintBackground` is always `true`, and the page always renders as `MediaType.Screen` — there is no option to switch either one off.
- **Options are frozen at registration.** The singleton is built on first resolve from the options passed to `AddPdfGenerator`, so per-request layout changes mean constructing `new PdfGenerator(options)` yourself instead of resolving `IPdfGenerator`.
