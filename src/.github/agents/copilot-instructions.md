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

- **`docs/<Area>/<Package>.md`** — the per-package reference (API, DI setup, options).
  Areas: `Core/`, `EfCore/`, `AspNetCore/`, `Services/`, `Messaging/`, `Aspire/`.
- **`.claude/skills/dknet-packages/`** — scenario-based routing from a need to the
  right package and its doc page. Companion skills: `dknet-codegen` (source
  generators), `dknet-testing` (TestContainers, coverage).

## Usage
Before API code generation tasks:
1. Read `docs/<Area>/README.md` for the area you are working in
2. Read `docs/<Area>/<Package>.md` for each package you will call
3. Apply the conventions in `src/AGENTS.md` and the repo-root `CLAUDE.md`
