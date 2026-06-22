---
title: PDF Generators
category: Service Adapters
tags: [pdf, markdown, html, rendering, service]
---

## Summary

`DKNet.Svc.PdfGenerators` is a service adapter that converts content — notably Markdown
rendered through HTML templates — into PDF documents. It ships HTML page templates and
supports rich rendering features, providing document generation as a pluggable service
in the Service Adapters ring of [[onion-architecture]].

## Templates and rendering features

The package includes HTML page templates that wrap the converted body:

- **`ContentTemplate.html`** — the primary template, injecting MathJax, Mermaid, and
  highlight.js scripts plus stylesheets so generated PDFs can render math, diagrams,
  and syntax-highlighted code.
- **`ContentTemplate_NoScripts.html`** — a minimal, script-free template for
  lightweight or sandboxed rendering, substituting only title, custom CSS, and body
  content.

Configuration highlights and extensibility points let applications customize styling
and the rendering pipeline.

## Where it fits

PDF generation is one of DKNet's pluggable infrastructure adapters, alongside
[[blob-storage]], [[encryption-services]], and [[transformation]]. Generated documents
are commonly produced inside application workflows — for example a [[cqrs-slimbus]]
handler that builds a report — and the resulting file can be persisted through
[[blob-storage]].
