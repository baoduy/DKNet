# Diagram sources

Every diagram referenced from the pages under `docs/` is committed twice:

- **`<name>.<type>.json`** — the typed [archify](https://github.com/tt-a1i/archify) intermediate representation.
  This is the source of truth; edit it, never the SVG.
- **`<name>.svg`** — the rendered asset the Markdown pages embed. Standalone: the theme stylesheet is inlined, so
  it renders correctly on GitHub and on GitHub Pages without any external CSS.

`<type>` is the archify diagram type and decides which schema and renderer apply: `architecture`, `workflow`,
`sequence`, `dataflow`, or `lifecycle`.

## Regenerating an SVG

```bash
# 1. Validate the IR at showcase quality — this must pass before rendering.
archify validate <type> docs/diagrams/<name>.<type>.json --quality showcase

# 2. Render to HTML.
archify render <type> docs/diagrams/<name>.<type>.json /tmp/<name>.html --quality showcase
```

The committed `.svg` is the diagram `<svg>` element extracted from that HTML, with the theme `<style>` block
inlined as its first child and explicit `width`/`height` attributes taken from the `viewBox` — the same output the
viewer's *Export SVG* button produces.

## Conventions

- **`meta.quality_profile` is `"showcase"`** on every diagram here.
- **`meta.legend.entries` is always populated.** Without it archify falls back to generic swatch labels
  ("Frontend", "Backend", "Emphasis") that say nothing about the particular diagram. Every entry key that the
  diagram actually renders must carry a label naming what it means *in that diagram*.
- **Every node, edge, and label states something checkable** against `src/`, a test, or a config file. A diagram
  is documentation, so the same accuracy bar applies.
- **One idea per diagram.** Prefer a second diagram over a denser one.
- **Static by default** — `meta.animation` is left unset.

## Cross-cutting diagrams

Four diagrams describe the suite rather than a single package, and are embedded from the hub and architecture
pages:

| Diagram | Answers |
|---|---|
| `dknet-layers.architecture.json` | which onion ring owns which package |
| `dknet-onion-packages.architecture.json` | what depends on what, as real project references |
| `dknet-request-lifecycle.sequence.json` | what one HTTP write request touches, in order |
| `dknet-domain-event-path.dataflow.json` | how a domain event travels from an entity method to a bus consumer |

The rest are named after the package they document.
