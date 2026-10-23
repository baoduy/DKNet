# Convert Markdown to a PDF

Part of [DKNet.Svc.PdfGenerators](index.md). `ConvertMarkdownFileAsync` renders one Markdown file, and
`ConvertMultipleMarkdownFilesAsync` concatenates several into a single document — the shape a changelog or a
multi-chapter export needs.

```csharp
using DKNet.Svc.PdfGenerators;
using Microsoft.Extensions.DependencyInjection;

var services = new ServiceCollection();
services.AddPdfGenerator();
using var provider = services.BuildServiceProvider();

var generator = provider.GetRequiredService<IPdfGenerator>();

// one markdown file -> PDF next to it (extension swapped)
var singlePath = await generator.ConvertMarkdownFileAsync("notes.md");
Console.WriteLine(singlePath);

// several markdown files concatenated with Environment.NewLine, then rendered as one document
var combinedPath = await generator.ConvertMultipleMarkdownFilesAsync(
    ["chapter1.md", "chapter2.md"], "book.pdf");
Console.WriteLine(combinedPath);
```

Markdown is rendered by Markdig with three extensions enabled, built once per `PdfGenerator` instance and reused
for every call:

- **Advanced extensions** (`UseAdvancedExtensions`) — GitHub-flavored tables, task lists, auto-links, footnotes, definition lists, and the rest of the Markdig "advanced" bundle.
- **YAML front matter** (`UseYamlFrontMatter`) — a leading `---` block is parsed as front matter instead of being rendered as content.
- **Emoji and smileys** (`UseEmojiAndSmiley`) — `:smile:` style shortcodes are converted.

The pipeline is fixed; there is no hook to add or remove Markdig extensions. `ConvertMultipleMarkdownFilesAsync`
joins the raw Markdown sources with `Environment.NewLine` **before** rendering, so a construct may span the
boundary between two files. Both overloads resolve paths to full paths and create the output directory if it is
missing; `ConvertMarkdownFileAsync(FileInfo)` wraps the string overload and returns a `FileInfo`.

![ConvertMarkdownFileAsync reads one file, or ConvertMultipleMarkdownFilesAsync joins several first; either way the text goes through Markdown.ToHtml with the fixed Markdig pipeline, then GeneratePdfFromHtmlAsync renders it with headless Chromium and writes the PDF.](diagrams/convert-markdown-to-pdf.svg)

The diagram does not show the HTML-to-PDF rendering step in detail — see
[Page layout, header and footer](page-layout-header-footer.md) for how `PdfGeneratorOptions` shapes that render.

## ⚙️ Configuration reference

| Call | Output when the path argument is omitted |
|---|---|
| `ConvertMarkdownFileAsync(string, string?)` | the input path with its extension changed to `.pdf` |
| `ConvertMarkdownFileAsync(FileInfo)` | same, returned as a `FileInfo` |
| `ConvertMultipleMarkdownFilesAsync` | *(output path is required)* |

## 🧱 Where it fits

[Convert HTML to a PDF](convert-html-to-pdf.md) skips the Markdig step entirely for callers that already have HTML.
Both paths converge on the same Chromium render described in
[Page layout, header and footer](page-layout-header-footer.md).

## ⚠️ Gotchas & limits

- **No Razor/template-model API.** There is no `GenerateFromTemplateAsync` or model binding — render the Markdown string yourself (fill placeholders, apply a template) before calling this package.
- **Multi-file joins can split a construct.** A fenced code block or table that straddles two files in `ConvertMultipleMarkdownFilesAsync` is joined by `Environment.NewLine` before parsing, so Markdig may not render it as intended.
- **The Markdig pipeline is fixed.** You cannot add, remove, or reorder extensions; `Theme`, `TableOfContents`, and `ModuleOptions` do not influence this pipeline either — see [Inert options and serialization](inert-options-and-serialization.md).
