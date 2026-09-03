# Package Documentation Template

The canonical structure for every `docs/<Area>/<Package>.md` page. Copy the skeleton, keep the
section order and emoji headers exactly — readers navigate the whole doc set by them — and follow
the per-section rules. Every page is written **from the source, not from memory**: read the
package's public surface first and document only what the code actually does.

## Ground rules (apply to every section)

1. **Verify against source.** Every API name, default value, exception type, and behavioural claim
   must be checked against the package's current code. Never document a parameter or option that
   does not exist.
2. **Full public surface.** Every public type and extension method a consumer can touch appears
   somewhere on the page. Internal machinery is mentioned only when knowing it explains observable
   behaviour ("why didn't my hook fire").
3. **`nameof`-safe examples.** Code samples must compile as written: declare every member an
   example references, include the `using` lines, and prefer `nameof(...)` over string literals.
4. **One concept per `###` block.** A feature section explains one thing, shows one runnable
   example, then states its edge cases — not a tour.
5. **Tables for enumerable facts** (options, enum members, diagnostics, DI registrations);
   prose for behaviour and reasoning. Never bury a default value in a paragraph.
6. **Link siblings relatively** (`./Other-Package.md`, `../Services/...`) so links work on
   GitHub and GitHub Pages alike.
7. **Diagram every feature that has a shape.** If a feature can be drawn — a flow, a state
   machine, a sequence, a decision — draw it. See **Diagrams** below for the type picker and
   authoring pipeline.

---

## Skeleton

```markdown
# DKNet.<Area>.<PackageName>

One or two sentences: what the package does and the mechanism it uses — concrete, not marketing.
("A pluggable before/after-SaveChanges interceptor pipeline for EF Core — one shared interceptor
per DbContext type plus a small pair of interfaces you implement.")

> [!IMPORTANT]
> Optional. Only for something that changes whether/how the reader should use the package at all
> (retirement, hard prerequisite, security caveat). At most one.

## ✨ Why use it?

- **Bold claim, then justification** — 4–6 bullets. Each names the problem you'd otherwise solve
  by hand and how this package removes it. First bullet = the strongest reason.
- **State what it is NOT** as the final line when the name invites misuse
  ("It is not a test-data generator: no seeding, no repeatable sequence.").

## 🚀 Quick Start

```bash
dotnet add package DKNet.<Area>.<PackageName>
```

```csharp
// The smallest end-to-end working snippet: DI registration + one real use.
// A reader should be able to paste this and see the package do its job.
```

## 🧩 Features

### <Feature or type name — e.g. "`IBeforeSaveHookAsync` — mutate before the write">

What it does, when it runs, what guarantees it gives. Then one focused example:

```csharp
// runnable, minimal, compiles as written
```

If the feature has a shape — a lifecycle, a pipeline, a branch — add its diagram right after
the example (see **Diagrams** below). End with the edge cases of THIS feature (what happens on
failure, what the default is, what it deliberately does not do).

### <Next feature>

Repeat. Order sections by the reader's journey: the type they write first, then the
supporting pieces, then "how it runs" internals (only as far as they explain behaviour).

When one feature has multiple declaration forms or modes, open with a **form-picker table**
(Form | Looks like | Use when) before diving into each form as a `####` subsection.

## ⚙️ Configuration reference

Every knob in one place, as a table. If there is no options object, say so explicitly and
table the DI registrations instead:

| Option / Registration | Type / Lifetime | Default | Effect |
|---|---|---|---|
| ... | ... | ... | ... |

## 🧱 Where it fits

Where the package sits in the DDD/Onion layering, which packages sit above/below it, and the
one diagram if the flow deserves it. Name its dependencies (or state "zero dependencies").

## ⚠️ Gotchas & limits

- **Bold one-line trap statement.** Then the consequence and the workaround. These come from
  the source and the test suite — real footguns (thread-safety scope, silent no-ops, ordering
  requirements, things that throw), never restatements of the feature list.
- 4–8 bullets. If you can't find any, you haven't read the source closely enough.

## 🔗 Related packages

- [DKNet.<Sibling>](./DKNet.<Sibling>.md) – what it adds, phrased as a routing rule:
  "Reach for it when/instead of ...". Cover every package this one composes with.
```

---

## Diagrams

Add a diagram whenever a feature has a drawable shape — don't reserve them for the
architecture overview. Pick the type from what the feature *is*:

| Feature shape | Diagram type | Example |
|---|---|---|
| Multi-step processing, "what happens on X" | **workflow / flow** | what one `SaveChangesAsync` runs, in order |
| Two+ components exchanging calls over time | **sequence** | interceptor → hook → publisher during a save |
| A value or entity moving through transforms | **dataflow** | token text → extractor → resolver → formatted output |
| Distinct states + transitions | **lifecycle / state** | entity `Added → Modified → Deleted`; disabled/enabled hook scopes |
| Static structure, layers, wiring | **architecture** | which package owns which interface; DI graph |
| Branching decision ("which form/overload do I use?") | **flow (decision)** | type-naming vs labelled vs label-less `[RaisesEvent]` |

**Tool: [archify](https://github.com/tt-a1i/archify)** — the required diagram tool for this repo
(full conventions in `docs/diagrams/README.md`). Diagrams are authored as archify's typed JSON
intermediate representation, not drawn by hand. That IR is what makes every diagram
**enhanceable later**: to add a node, rename an edge, or restyle the whole set, edit the JSON
and re-render — no diagram ever has to be redrawn from scratch, and a reviewer can diff the
change like code.

1. Author the typed JSON IR: `docs/diagrams/<name>.<type>.json`
   (`<type>` ∈ `architecture | workflow | sequence | dataflow | lifecycle` — the type selects
   the schema and renderer). The JSON is the source of truth — edit it, never the SVG.
2. Validate, then render (both must pass at `showcase` quality):

   ```bash
   archify validate <type> docs/diagrams/<name>.<type>.json --quality showcase
   archify render   <type> docs/diagrams/<name>.<type>.json /tmp/<name>.html --quality showcase
   ```

   The committed `.svg` is the `<svg>` element extracted from that HTML with the theme
   `<style>` inlined — standalone, renders on GitHub and GitHub Pages without external CSS.
3. Commit **both** files — `<name>.<type>.json` (source) and `<name>.svg` (rendered) — and set
   `meta.quality_profile: "showcase"` plus a populated `meta.legend.entries` (generic swatch
   labels are a review reject).
4. Embed with a **long descriptive alt text** that works as the text-only fallback — a full
   sentence narrating the diagram, not a caption:
   `![Sequence diagram of one SaveChangesAsync call: the interceptor runs BeforeSaveAsync to capture declared events before the write, then AfterSaveAsync to map and publish them only once the write has succeeded.](../diagrams/<name>.svg)`

**ASCII fallback** — for a trivial linear flow (≤ 5 nodes, no branching), an ASCII diagram in a
fenced code block is acceptable inline and needs no `docs/diagrams/` asset:

```text
request ──▶ [key extracted] ──▶ store.TryAdd ──▶ hit? ──▶ cached response
                                             └─ miss ──▶ endpoint runs ──▶ response cached
```

Rules either way:

- The diagram shows the **real mechanism** (actual type/method names from the source), never
  generic boxes ("Service → Database").
- One diagram per shape — don't draw the same flow twice from different angles.
- Do **not** use ```mermaid fences: the GitHub Pages primer theme does not render them, so the
  page would ship broken on the published site.
- If a change alters a drawn relationship, update the JSON + re-render the SVG in the same PR —
  never patch the SVG directly, or the next re-render silently reverts the fix.

---

## Section-by-section rules

| Section | Must contain | Must NOT contain |
|---|---|---|
| Title + lede | Mechanism in one breath | Feature list, superlatives |
| ✨ Why use it? | Problems removed, strongest first; a "not for" line when needed | API details, code |
| 🚀 Quick Start | Install + smallest working end-to-end snippet | Every option, edge cases |
| 🧩 Features | One `###` per concern; runnable example each; a diagram when the feature has a shape; diagnostics codes (`DK*`) inline where the mistake happens | Marketing prose, untested snippets, mermaid fences |
| ⚙️ Configuration | Exhaustive table of every knob with defaults | Behaviour explanations (link back to the feature section) |
| 🧱 Where it fits | Layer, dependencies, composition with sibling packages, optional diagram | Restating features |
| ⚠️ Gotchas & limits | Source-verified footguns with workarounds | Generic advice ("remember to test") |
| 🔗 Related packages | Every sibling it composes with + routing rule | Bare links without the "reach for it when" |

## Checklist before committing a page

- [ ] Every public type/extension method of the package is mentioned at least once.
- [ ] Every code sample compiles: members declared, `using` lines present, current API names.
- [ ] Every default value and diagnostic code cross-checked against source.
- [ ] Options/enums/diagnostics rendered as tables, each row verified.
- [ ] Gotchas traced to actual source behaviour (cite the mechanism, e.g. "keyed by `Type.FullName`").
- [ ] Every feature with a drawable shape has its diagram (archify JSON IR + rendered SVG both
      committed, or inline ASCII for trivial linear flows); `archify validate` passes at
      `showcase`; alt text narrates the diagram; no mermaid fences; no hand-edited SVGs.
- [ ] Linked from the area `README.md` index.
- [ ] Related-packages section links resolve and carry a routing rule each.
