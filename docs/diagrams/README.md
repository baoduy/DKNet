# Diagram sources

This folder holds only the four diagrams shared by the cross-cutting guides (listed below). Each package's own
diagrams live in its folder, `docs/<Package>/diagrams/`, next to the pages that embed them. Wherever it lives, every
diagram is committed twice:

- **`<name>.<type>.json`** — the typed [archify](https://github.com/tt-a1i/archify) intermediate representation.
  This is the source of truth; edit it, never the SVG.
- **`<name>.svg`** — the rendered asset the Markdown pages embed. Standalone: the theme stylesheet is inlined, so
  it renders correctly on GitHub and on GitHub Pages without any external CSS.

`<type>` is the archify diagram type and decides which schema and renderer apply: `architecture`, `workflow`,
`sequence`, `dataflow`, or `lifecycle`.

## Regenerating an SVG

```bash
# 1. Validate the IR at showcase quality — this must pass before rendering.
#    For a package diagram, use docs/<Package>/diagrams/ instead of docs/diagrams/.
archify validate <type> docs/diagrams/<name>.<type>.json --quality showcase

# 2. Render to HTML.
archify render <type> docs/diagrams/<name>.<type>.json /tmp/<name>.html --quality showcase

# 3. Open /tmp/<name>.html and choose Export → "SVG (light)". Commit the downloaded file as <name>.svg.
```

The committed `.svg` is the output of the viewer's **Export → SVG (light)** menu item (`data-format="svg-light"`):
the diagram `<svg>` element with `data-theme="light"` on its root, the light palette resolved into its inlined
`<style>` block and a light background, and explicit `width`/`height` attributes taken from the `viewBox`. Never
commit the plain *SVG* export: it follows the reader's colour scheme and turns dark for a reader whose OS or browser
is in dark mode, and the *SVG (dark)* export is always dark. The GitHub Pages theme (`jekyll-theme-primer`) is light,
so every diagram is pinned light.

Check before committing — every root `<svg>` must print `data-theme="light"`:

```bash
for f in $(find docs -name '*.svg'); do grep -o '<svg[^>]*>' "$f" | head -1 | grep -o 'data-theme="[a-z]*"' || echo none; done | sort | uniq -c
```

**archify 3.x:** `render`/`finalize` require `meta.output`, which the committed sources omit — add it to a scratch
copy, not the committed JSON. Its *Export SVG* output starts with an XML declaration and embeds the JetBrains Mono
subset, so a 3.x export is larger than the older assets. `dknet-layers`, `dknet-onion-packages` and
`dknet-request-lifecycle` were last exported with 3.0.1 after passing `finalize --quality showcase`.
Every SVG under `docs/` was re-exported with 3.0.1 *SVG (light)* for DRK-2597, except three that 3.0.1 now lays out
differently from their committed SVG: `dknet-domain-event-path`, and `efcore-encryption-column-path` and
`encrypt-columns` in `docs/DKNet.EfCore.Encryption/diagrams/`. Those three keep their earlier colour-scheme-following
export with `data-theme="light"` added to the root `<svg>`, which selects the light palette their `<style>` already
holds. `dknet-domain-event-path.svg` painted no background, so it also gained one full-size `#f4f5f7`
background `<rect>` before its grid; without it the diagram showed through to a dark page. Their next re-export,
after any JSON edit, picks up the current layout.

## Conventions

- **`meta.quality_profile` is `"showcase"`** on every diagram here.
- **`meta.legend.entries` is always populated.** Without it archify falls back to generic swatch labels
  ("Frontend", "Backend", "Emphasis") that say nothing about the particular diagram. Every entry key that the
  diagram actually renders must carry a label naming what it means *in that diagram*.
- **Every node, edge, and label states something checkable** against `src/`, a test, or a config file. A diagram
  is documentation, so the same accuracy bar applies.
- **One idea per diagram.** Prefer a second diagram over a denser one.
- **Static by default** — `meta.animation` is left unset.
- **Light theme, pinned.** Every committed `.svg` is exported with *Export → SVG (light)*, so its root `<svg>`
  carries `data-theme="light"` and it renders light whatever colour scheme the reader uses. The JSON has no theme
  field: the theme is chosen at export time only.

## Shared diagrams

These four describe the suite rather than a single package, and are embedded from the repo-root README, the package
index, the Getting Started guide and the Architecture guide:

| Diagram | Answers |
|---|---|
| `dknet-layers.architecture.json` | which onion ring owns which package |
| `dknet-onion-packages.architecture.json` | what depends on what, as real project references |
| `dknet-request-lifecycle.sequence.json` | what one HTTP write request touches, in order |
| `dknet-domain-event-path.dataflow.json` | how a domain event travels from an entity method to a bus consumer |

Every other diagram belongs to one package and lives in `docs/<Package>/diagrams/`.
