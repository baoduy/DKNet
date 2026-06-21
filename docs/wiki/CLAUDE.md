# DKNet Wiki — Schema & Conventions

This directory is an **LLM-maintained knowledge wiki** for the DKNet Framework, a
.NET 10 NuGet library suite built around Domain-Driven Design (DDD) and Onion
Architecture. It follows the "Karpathy pattern": a flat folder of short, focused
Markdown articles that an LLM can read, link, and keep current.

## Conventions

- **Articles** are one Markdown file per slug (`<slug>.md`). Each article opens with
  YAML frontmatter (`title`, `category`, `tags`), a `## Summary`, then 2–4 content
  sections grounded in the codebase and `docs/`.
- **Wikilinks** use the `[[slug]]` form and must point only at existing article
  slugs so links always resolve. Every article links to at least three others.
- **Categories** are defined in `index.md` under `##` headings. The `category` value
  in an article's frontmatter must match one of those headings verbatim.
- **`index.md`** is the content catalog: every article is listed under its category
  with a one-line description.
- **`log.md`** is the chronological operation log. Append one entry per maintenance
  pass describing what changed and why.

## Maintaining the wiki

When code or `docs/` change, update the affected articles, fix any wikilinks, refresh
`index.md` if articles are added/removed, and append a dated entry to `log.md`.
Keep articles short and factual; cite real types, packages, and behaviors.
