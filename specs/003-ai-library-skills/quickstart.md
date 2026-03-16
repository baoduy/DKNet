# Quickstart: Creating and Maintaining DKNet AI Skill Files

## What Is a Skill File?

A skill file is a structured markdown document in `memory-bank/libraries/` that tells AI agents (GitHub Copilot, Cursor, Claude Code) exactly how to use one DKNet library. AI agents load these files as context when generating code.

---

## Adding a New Skill File

1. **Copy** `specs/003-ai-library-skills/contracts/skill-file-template.md`
2. **Name it** `NN-package-name.md` where `NN` is the next sequence number
3. **Fill every section** — do not leave placeholder text
4. **Add it** to `memory-bank/libraries/README.md` master index
5. **Verify** all code examples compile with zero warnings

---

## Updating an Existing Skill File

Skill files MUST be updated in the **same PR** as the library change when:

- A public API method signature changes
- A DI registration method is renamed or split
- A new anti-pattern is discovered
- The minimum version requirement increases

Update the `## Version` table at the bottom of the file.

---

## Quality Checklist (per file)

Before merging a skill file:

- [ ] All sections of the template are filled
- [ ] Code examples use `.NET 10+` syntax
- [ ] All code examples compile with `TreatWarningsAsErrors=true`
- [ ] Nullable reference types used everywhere
- [ ] Test example uses xUnit + Shouldly + TestContainers (no `UseInMemoryDatabase`)
- [ ] Anti-Patterns section has at least 2 entries
- [ ] "Composes With" table is accurate
- [ ] Security Notes section present for security-relevant libraries
- [ ] Entry added to `memory-bank/libraries/README.md`

---

## File Naming Convention

```
NN-short-name.md

Examples:
  01-slimbus-extensions.md
  07-efcore-specifications.md
  composition-patterns.md   (multi-library recipes, no number prefix)
  README.md                 (master index, always this name)
```

---

## Agent Loading

The `memory-bank/libraries/README.md` master index is referenced from:
- `AGENTS.md` → "Library Skills" section
- `memory-bank/README.md` → "Libraries" navigation entry
- `.github/copilot-instructions.md` → loading priority list

AI agents are instructed to load `README.md` first (for scenario routing) then the specific skill file(s) for the task at hand.

