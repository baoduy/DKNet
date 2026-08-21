# DKNet.Svc.PdfGenerators

[![NuGet](https://img.shields.io/nuget/v/DKNet.Svc.PdfGenerators)](https://www.nuget.org/packages/DKNet.Svc.PdfGenerators/)
[![NuGet Downloads](https://img.shields.io/nuget/dt/DKNet.Svc.PdfGenerators)](https://www.nuget.org/packages/DKNet.Svc.PdfGenerators/)
[![.NET](https://img.shields.io/badge/.NET-10.0-blue)](https://dotnet.microsoft.com/)
[![License](https://img.shields.io/badge/License-MIT-green.svg)](../../../../LICENSE)

Documentation-grade PDF generation from HTML or Markdown, via headless Chromium (PuppeteerSharp) and Markdig. Fork
of Markdown2Pdf 2.x with additional options — see `THIRD-PARTY-NOTICES.md` for upstream attribution.

## Features

- `IPdfGenerator` — convert an HTML string, an HTML file, a Markdown file, or several Markdown files into one PDF
- Table of contents with configurable depth, list style, and page-number leaders
- ~60 built-in syntax-highlighting themes plus a Github/Latex/custom page theme
- Remote (CDN) or local (offline npm) MathJax/Mermaid/highlight.js module loading

## Installation

```bash
dotnet add package DKNet.Svc.PdfGenerators
```

## Quick Start

```csharp
using DKNet.Svc.PdfGenerators;

builder.Services.AddPdfGenerator();

public sealed class ReportExporter(IPdfGenerator pdfGenerator)
{
    public Task<string> ExportAsync(string markdownPath) =>
        pdfGenerator.ConvertMarkdownFileAsync(markdownPath, "report.pdf");
}
```

## Documentation

Full feature reference, configuration, and gotchas:
https://github.com/baoduy/DKNet/blob/dev/docs/Services/DKNet.Svc.PdfGenerators.md
