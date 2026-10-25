# Copilot Agent Context (Spec-Kit)

This file is generated/maintained for spec-driven planning context.

## Feature Context
- Feature: `003-ai-library-skills` — **superseded**
- Plan: `specs/003-ai-library-skills/plan.md` (historical record only)
- Language: C# 13 / .NET 10+
- Project Type: Documentation / AI context library

## Status
The `memory-bank/libraries/` tree this feature produced has been removed. Its two
successors, both live:

- **`docs/<Package>/index.md`** — the per-package reference (API, DI setup, options), one
  folder per package with a page per feature; `docs/README.md` indexes them by area.
- **`plugins/dknet-skills/skills/dknet-packages/`** — scenario-based routing from a need
  to the right package and to the skill that owns it. The `dknet-skills` plugin holds one
  skill per package family plus `dknet-codegen` (source generators) and `dknet-testing`
  (TestContainers, coverage).

## Usage
Before API code generation tasks:
1. Read `docs/README.md`, the package index, to find the packages you need
2. Read `docs/<Package>/index.md` and its feature pages for each package you will call
3. Apply the conventions in `src/AGENTS.md` and the repo-root `CLAUDE.md`
