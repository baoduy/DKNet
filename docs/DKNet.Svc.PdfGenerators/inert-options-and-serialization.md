# Inert options and serialization

Part of [DKNet.Svc.PdfGenerators](index.md). `PdfGeneratorOptions` carries a theme, a table-of-contents
configuration, a module-loading strategy, and several metadata fields — all settable, all serializable through
`SerializableOptions`, and **none of them read by the current `PdfGenerator` pipeline**. `PdfGenerator` renders
Markdown to HTML and hands it straight to Chromium; it never builds the theme, module, table-of-contents, or
metadata pipeline that would consume these values.

```csharp
using System.Text.Json;
using DKNet.Svc.PdfGenerators;
using DKNet.Svc.PdfGenerators.Options;
using Microsoft.Extensions.DependencyInjection;

const string configJson = """
{
  "Format": "Letter",
  "IsLandscape": true,
  "Theme": "Latex",
  "ModuleOptions": "None"
}
""";

var options = JsonSerializer
    .Deserialize<SerializableOptions>(configJson)!
    .ToPdfGeneratorOptions();

var services = new ServiceCollection();
services.AddPdfGenerator(options);
using var provider = services.BuildServiceProvider();

var generator = provider.GetRequiredService<IPdfGenerator>();
var path = await generator.ConvertMarkdownFileAsync("notes.md", "reports/monthly.pdf");
Console.WriteLine(path);
```

In this example, `Format` and `IsLandscape` reach the render (see
[Page layout, header and footer](page-layout-header-footer.md)); `Theme` and `ModuleOptions` are accepted, stored
on the resulting `PdfGeneratorOptions`, and then have no effect.

`SerializableOptions` mirrors `PdfGeneratorOptions` with flat, mostly string-typed properties (handy for JSON
config or a CLI); `ToPdfGeneratorOptions()` only overwrites a target property when its source is non-`null`.
Named values are resolved by reflection against the target type's static properties: an unknown `Theme` string
becomes `Theme.Custom(value)`, an unknown `ModuleOptions` string becomes `ModuleOptions.FromLocalPath(value)`, and
an unknown `Format` or `CodeHighlightTheme` string is **ignored** rather than throwing.
`InlineOptionsParser.ParseYamlFrontMatter(markdownFilePath)` reads the same shape out of a leading `---` or `<!--`
front-matter block using hyphenated YAML keys (`chrome-path`, `document-title`, …), and throws
`InvalidDataException` when the file has no closing delimiter — `PdfGenerator` never calls it; it is a helper you
invoke yourself before constructing options.

![JSON or front-matter deserializes into SerializableOptions; ToPdfGeneratorOptions() copies every non-null field onto PdfGeneratorOptions, but only the applied fields (Format, Scale, Margins, Headers) are read by PdfGenerator -- the inert fields (Theme, TableOfContents, ModuleOptions, metadata) have no caller into ThemeService, TableOfContentsCreator or ModuleService.](diagrams/inert-options-and-serialization.svg)

## ⚙️ Configuration reference

| Option | Type | Default | Effect | Applied |
|---|---|---|---|---|
| `Theme` | `Theme` | `Theme.Github` | `Theme.Github` / `.Latex` / `.None` / `.Custom(cssPath)`. | ❌ Not wired — no effect |
| `CodeHighlightTheme` | `CodeHighlightTheme` | `CodeHighlightTheme.Github` (`github.css`) | One of the 75 highlight.js stylesheets exposed as static properties. | ❌ Not wired — no effect |
| `ModuleOptions` | `ModuleOptions` | `ModuleOptions.Remote` | `Remote` / `None` / `FromLocalPath(path)` for MathJax, Mermaid, highlight.js assets. | ❌ Not wired — no effect |
| `TableOfContents` | `TableOfContentsOptions?` | `null` | `MinDepthLevel`/`MaxDepthLevel` (1–6, else `ArgumentOutOfRangeException`), `ListStyle`, `HasColoredLinks`, `PageNumberOptions`. | ❌ Not wired — no effect |
| `DocumentTitle` | `string?` | `null` | Document title. | ❌ Not wired — no effect |
| `MetadataTitle` | `string?` | `null` | PDF metadata title. | ❌ Not wired — no effect |
| `CustomHeadContent` | `string?` | `null` | Extra markup for the generated `<head>`. | ❌ Not wired — no effect |
| `KeepHtml` | `bool` | `false` | Keep the intermediate HTML for debugging. | ❌ Not wired — no effect |
| `EnableAutoLanguageDetection` | `bool` | `false` | Auto-detect code-block languages. | ❌ Not wired — no effect |

### `TableOfContentsOptions`

| Option | Type | Default | Effect |
|---|---|---|---|
| `MinDepthLevel` | `int` | `1` | Shallowest heading level included. Assigning outside `1..6` throws `ArgumentOutOfRangeException`. |
| `MaxDepthLevel` | `int` | `6` | Deepest heading level included. Same `1..6` guard. |
| `ListStyle` | `ListStyle` | `ListStyle.OrderedDefault` | Marker style for TOC entries: `None`, `OrderedDefault`, `Unordered`, `Decimals`. |
| `HasColoredLinks` | `bool` | `false` | `true` leaves TOC links in the theme's link colour instead of styling them as plain text. |
| `PageNumberOptions` | `PageNumberOptions?` | `null` | Non-`null` asks for page numbers; `TabLeader` (`Leader`, default `Dots`; values `None`, `Dots`, `Underline`, `Dashes`). |

### `ModuleOptions` — a closed set, not a constructor

`ModuleOptions`' constructor is `protected internal`, so a consumer cannot subclass it: get an instance from
`ModuleOptions.None`, `ModuleOptions.Remote` (default), or `ModuleOptions.FromLocalPath(path)` (returns
`NodeModuleOptions(path)`). `Theme` is a `public abstract record` with no public constructor: use `Theme.Github`
(default), `Theme.Latex`, `Theme.None`, or `Theme.Custom(cssPath)` — the internal `PredefinedTheme` means a
predefined theme can be selected but not constructed or extended.

## 🧱 Where it fits

None of the fields on this page reach [Page layout, header and footer](page-layout-header-footer.md)'s Chromium
render, [Convert Markdown to a PDF](convert-markdown-to-pdf.md)'s Markdig pipeline, or
[Convert HTML to a PDF](convert-html-to-pdf.md). They exist as a fully testable, round-trippable surface with no
consumer in `PdfGenerator`.

## ⚠️ Gotchas & limits

- **Most option surface here is inert.** Earlier revisions of this documentation described `[TOC]` markers and theme switching as working features; they are not wired in source — do not design a document around them.
- **`IConversionEvents` (`HtmlConverting`, `TemplateModelCreating`, `TempPdfCreated`) never fires from `PdfGenerator`.** Only the unwired helper services (`ThemeService`, `TableOfContentsCreator`, `ModuleService`, `MetadataService`) subscribe to it.
- **`ListStyle.Decimals` is plural**, even though the type's own XML example spells it `Decimal` — use `Decimals`.
- Do not edit `THIRD-PARTY-NOTICES.md` or the test fixture `Sample.md` — the legal attribution and packaging tests depend on their exact contents.
