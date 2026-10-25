# Package Documentation Template

The canonical structure for every package's documentation. Each package gets **one folder**,
`docs/<Package>/`, named exactly as its NuGet `PackageId`, plus a package `README.md` next to its
project. Copy the skeletons, keep the section order and emoji headers exactly — readers navigate
the whole doc set by them — and follow the per-section rules. Every page is written **from the
source, not from memory**: read the package's public surface first and document only what the
code actually does.

## The folder

```text
docs/<Package>/
├── index.md              the package: what, why, Quick Start, architecture, how it works, feature list
├── <feature>.md          one page per capability, kebab-case of what it does (send-commands.md)
└── diagrams/             every diagram's archify JSON IR + rendered .svg

src/<Area>/<Package>/README.md   the package README, packed into the NuGet package
```

- One capability per feature page. A package with a single capability still gets `index.md`
  plus one feature page.
- Every `docs/<Package>/index.md` is listed in the package index, [`docs/README.md`](README.md).
- `docs/diagrams/` holds only the four diagrams shared by the cross-cutting guides; a package's
  own diagrams live in `docs/<Package>/diagrams/`.

## Ground rules (apply to every page)

1. **Verify against source.** Every API name, default value, exception type, and behavioural claim
   must be checked against the package's current code. Never document a parameter or option that
   does not exist.
2. **Full public surface.** Every public type and extension method a consumer can touch appears
   on some page in the folder. Internal machinery is mentioned only when knowing it explains
   observable behaviour ("why didn't my hook fire").
3. **`nameof`-safe examples.** Code samples must compile as written: declare every member an
   example references, include the `using` lines, and prefer `nameof(...)` over string literals.
   A Quick Start is run, or copied verbatim from a passing test.
4. **Point at code by name, never by line.** Name the type (`IRepositorySpec`) or its folder; a
   file path only when the file itself is the subject. Never a line number or range — every edit
   moves lines and the page goes stale.
5. **Tables for enumerable facts** (options, enum members, diagnostics, DI registrations);
   prose for behaviour and reasoning. Never bury a default value in a paragraph.
6. **Link siblings relatively** (`../DKNet.EfCore.Hooks/index.md`, `send-commands.md`) so links
   work on GitHub and GitHub Pages alike.
7. **Diagram every page.** Each `index.md` carries a component diagram and a flow diagram; each
   feature page carries its own diagram. See **Diagrams** below.

---

## `index.md` skeleton

````markdown
# DKNet.<Area>.<PackageName>

One sentence: what the package does and for whom — concrete, not marketing.

## ✨ Why use it?

- **Bold claim, then justification** — 2–5 bullets. Each names the problem you'd otherwise solve
  by hand and how this package removes it. First bullet = the strongest reason.

## 🚀 Quick Start

```bash
dotnet add package DKNet.<Area>.<PackageName>
```

```csharp
// The smallest end-to-end working snippet: DI registration + one real use.
// A reader should be able to paste this and see a visible result.
```

## 🏗️ Architecture

The component diagram: the package's real types as they run inside the host app — what the
reader registers or calls, the stores, handlers and background parts behind them, and every
external dependency. Then 2–3 sentences on what it cannot show (lifetimes, what is shared, what
runs where).

## 🔄 How it works

The flow diagram of the Quick Start call, from that call to the result the reader sees: every
step in order, the branch they hit most and the failure path. Then 2–4 sentences on defaults,
ordering and timing the diagram cannot show.

## 🧩 Features

- [Capability, phrased as what it does](<feature>.md) — one line: what it does for the reader

## 🔗 Related packages

- [DKNet.<Sibling>](../DKNet.<Sibling>/index.md) — what it adds, phrased as a routing rule:
  "Reach for it when/instead of ...". Cover every package this one composes with.
````

## Feature page skeleton

```markdown
# Capability, phrased as what it does

Part of [DKNet.<Area>.<PackageName>](index.md). What it is, the smallest code showing it, then the
behaviour worth knowing (defaults, order, what happens on the unhappy path). Then this
capability's diagram, right after its example, with 1–2 sentences on what it cannot show.

## ⚙️ Configuration reference

| Option / Registration | Type / Lifetime | Default | Effect |
|---|---|---|---|
| ... | ... | ... | ... |

## 🧱 Where it fits

How this capability sits against the other packages and the runtime around it — with an
architecture diagram when it spans more than one.

## ⚠️ Gotchas & limits

- **Bold one-line trap statement.** Then the consequence and the workaround. These come from
  the source and the test suite — real footguns (thread-safety scope, silent no-ops, ordering
  requirements, things that throw), never restatements of the happy path.
```

## Package `README.md` skeleton

The README next to the project is what nuget.org shows. It carries the title and one-liner,
**✨ Why use it?** and **🚀 Quick Start** (the same as `index.md`), then a **Documentation** table
linking into the folder — no Features section, which would drift from the pages that own it:

```markdown
## Documentation

| Guide | Read when |
|---|---|
| [Package overview](https://github.com/baoduy/DKNet/blob/main/docs/<Package>/index.md) | Start with architecture and the end-to-end flow. |
| [<Capability>](https://github.com/baoduy/DKNet/blob/main/docs/<Package>/<feature>.md) | One line: when to read it. |
```

Links are absolute because the README is packed into the NuGet package, where relative links do
not resolve.

---

## Diagrams

Every diagram is authored with **[archify](https://github.com/tt-a1i/archify)** — the required
diagram tool for this repo (full conventions in [`docs/diagrams/README.md`](diagrams/README.md)).
Diagrams are typed JSON intermediate representation, not drawn by hand: to add a node, rename an
edge, or restyle the whole set, edit the JSON and re-render, and a reviewer can diff the change
like code.

| Where | Diagram | Type |
|---|---|---|
| `index.md` 🏗️ Architecture | the package's components inside the host app | **architecture** |
| `index.md` 🔄 How it works | what the Quick Start call runs, in order | **workflow** or **sequence** |
| each feature page | that capability's own shape | pick from the table below |

| Feature shape | Diagram type | Example |
|---|---|---|
| Multi-step processing, "what happens on X" | **workflow** | what one `SaveChangesAsync` runs, in order |
| Two+ components exchanging calls over time | **sequence** | interceptor → hook → publisher during a save |
| A value or entity moving through transforms | **dataflow** | token text → extractor → resolver → formatted output |
| Distinct states + transitions | **lifecycle** | entity `Added → Modified → Deleted`; disabled/enabled hook scopes |
| Static structure, layers, wiring | **architecture** | which package owns which interface; DI graph |
| Branching decision ("which form/overload do I use?") | **workflow** (decision) | type-naming vs labelled vs label-less `[RaisesEvent]` |

1. Author the typed JSON IR: `docs/<Package>/diagrams/<name>.<type>.json`
   (`<type>` ∈ `architecture | workflow | sequence | dataflow | lifecycle` — the type selects
   the schema and renderer). The JSON is the source of truth — edit it, never the SVG.
2. Validate, then render at `showcase` quality, as `docs/diagrams/README.md` describes.
3. Commit **both** files — `<name>.<type>.json` (source) and `<name>.svg` (rendered) — side by
   side in `docs/<Package>/diagrams/`, with `meta.quality_profile: "showcase"` and a populated
   `meta.legend.entries` (generic swatch labels are a review reject).
4. Embed with a **long descriptive alt text** that works as the text-only fallback — a full
   sentence narrating the diagram, not a caption:
   `![Sequence diagram of one SaveChangesAsync call: the interceptor runs BeforeSaveAsync to capture declared events before the write, then AfterSaveAsync to map and publish them only once the write has succeeded.](diagrams/<name>.svg)`

Rules:

- The diagram shows the **real mechanism** (actual type/method names from the source), never
  generic boxes ("Service → Database").
- One diagram per shape — don't draw the same flow twice from different angles.
- Do **not** use ```mermaid fences: the GitHub Pages primer theme does not render them, so the
  page would ship broken on the published site.
- If a change alters a drawn relationship, update the JSON + re-render the SVG in the same PR —
  never patch the SVG directly, or the next re-render silently reverts the fix.

---

## Section-by-section rules

| Page | Section | Must contain | Must NOT contain |
|---|---|---|---|
| `index.md` | Title + one-liner | What it does, for whom, in one sentence | Feature list, superlatives |
| `index.md` | ✨ Why use it? | Problems removed, strongest first | API details, code |
| `index.md` | 🚀 Quick Start | Install + smallest working end-to-end snippet | Every option, edge cases |
| `index.md` | 🏗️ Architecture | Component diagram of the real types, plus what it cannot show | Generic boxes |
| `index.md` | 🔄 How it works | Flow diagram of the Quick Start call, including its failure path | Invented steps |
| `index.md` | 🧩 Features | Every feature page in the folder, one line each | Method signatures restated |
| `index.md` | 🔗 Related packages | Every sibling it composes with + routing rule | Bare links without the "reach for it when" |
| feature page | Title + body | Back-link to `index.md`, one capability, a runnable example, its diagram | A second capability, mermaid fences |
| feature page | ⚙️ Configuration reference | Exhaustive table of every knob this capability reads, with defaults | Behaviour explanations |
| feature page | 🧱 Where it fits | Composition with sibling packages, a diagram when it spans several | Restating the feature |
| feature page | ⚠️ Gotchas & limits | Source-verified footguns with workarounds | Generic advice ("remember to test") |

## Checklist before committing a package's docs

- [ ] Every public type/extension method of the package is mentioned on at least one page.
- [ ] Every code sample compiles: members declared, `using` lines present, current API names.
- [ ] Every default value and diagnostic code cross-checked against source.
- [ ] Options/enums/diagnostics rendered as tables, each row verified.
- [ ] Gotchas traced to actual source behaviour (cite the mechanism, e.g. "keyed by `Type.FullName`").
- [ ] `index.md` has its 🏗️ Architecture and 🔄 How it works diagrams, every feature page has its
      own, each with archify JSON IR + rendered SVG committed in `docs/<Package>/diagrams/`;
      `archify validate` passes at `showcase`; alt text narrates the diagram; no mermaid fences;
      no hand-edited SVGs.
- [ ] Every feature page is listed in `index.md` 🧩 Features and links back to it.
- [ ] `docs/<Package>/index.md` is linked from the package index, [`docs/README.md`](README.md).
- [ ] The package `README.md` Documentation table links every page in the folder.
- [ ] Related-packages links resolve and carry a routing rule each.
