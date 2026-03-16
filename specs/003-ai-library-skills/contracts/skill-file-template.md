# Skill File Template

> Copy this template when adding a new library to `memory-bank/libraries/`.  
> Fill every section. Do not leave placeholder text.

---

# [Library Name] — AI Skill File

> **Package**: `[NuGet package name]`  
> **Minimum Version**: `[x.y.z]`  
> **Minimum .NET**: `net10.0`  
> **Source**: `[relative path in repo]`

---

## Purpose

_One sentence: what problem does this library solve?_

---

## When To Use

- ✅ [Scenario 1]
- ✅ [Scenario 2]

## When NOT To Use

- ❌ [Anti-scenario 1 — suggest the correct alternative]
- ❌ [Anti-scenario 2]

---

## Installation

```bash
dotnet add package [PackageName]
```

---

## Setup / DI Registration

```csharp
// Program.cs / ServiceCollectionExtensions
[paste the exact service registration snippet]
```

---

## Key API Surface

| Type / Method | Role |
|---|---|
| `TypeName` | What it does |

---

## Usage Pattern

```csharp
// ✅ Correct idiomatic usage
[complete, compilable example with XML doc headers]
```

---

## Anti-Patterns

```csharp
// ❌ WRONG — [reason]
[bad code]

// ✅ CORRECT
[good code]
```

---

## Composes With

| Library | How |
|---|---|
| `DKNet.XYZ` | [relationship] |

---

## Test Example

```csharp
// xUnit + Shouldly + TestContainers — NO UseInMemoryDatabase
[test method following MethodName_Scenario_ExpectedBehavior naming]
```

---

## Security Notes _(required for libraries handling sensitive data)_

- [Note 1]

---

## Version

| Version | Notes |
|---|---|
| `x.y.z` | Initial documentation |

