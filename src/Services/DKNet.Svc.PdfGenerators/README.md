# DKNet.Svc.PdfGenerators

## Overview

DKNet.Svc.PdfGenerators is a .NET 9.0 library for generating PDFs from HTML, Markdown, and other content sources. It
provides flexible options for document styling, table of contents, code highlighting, and more, leveraging modern PDF
and HTML processing libraries.

## Features

- PDF generation from HTML/Markdown
- Customizable templates and themes
- Table of contents support
- Code highlighting
- Header/footer styling
- Embedded resource management

## Key Dependencies

- Markdig (Markdown processing)
- PdfPig (PDF manipulation)
- PuppeteerSharp (HTML to PDF rendering)
- YamlDotNet (YAML configuration)

## Project Structure

- `PdfGenerator.cs`: Main PDF generation logic
- `Models/`: Data models (e.g., `ModuleInformation`)
- `Options/`: Configuration options for themes, margins, TOC, etc.
- `Services/`: Internal services for resource management, metadata, templates, and more
- `templates/`: HTML/CSS templates for content and styling

## Usage

1. Reference the DKNet.Svc.PdfGenerators NuGet package in your project.
2. Configure your PDF generation options using the provided models.
3. Use `PdfGenerator` to generate PDFs from your content.

## Example

```csharp
var options = new PdfGeneratorOptions { /* ... */ };
var generator = new PdfGenerator(options);
generator.GeneratePdf("input.md", "output.pdf");
```

## Contributing

Contributions are welcome! Please submit issues or pull requests for bug fixes and new features.

## License

[Specify your license here]

