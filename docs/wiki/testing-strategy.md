---
title: Testing Strategy
category: Quality & Testing
tags: [testing, xunit, shouldly, testcontainers, coverage]
---

## Summary

DKNet's test suite uses **xUnit** with **Shouldly** assertions and
**TestContainers.MsSql** for integration tests, so persistence is exercised against a
real SQL Server rather than an in-memory provider. Every package has a sibling
`*.Tests` project, and coverage gates are enforced in CI to keep the framework reliable.

## Conventions

- **Naming**: `MethodName_Scenario_ExpectedBehavior` (for example
  `DynamicAnd_WithMultipleConditions_CombinesCorrectly`).
- **Stack**: xUnit + Shouldly + TestContainers.MsSql; avoid mocking the database.
- **Isolation**: prefer `IAsyncLifetime` fixtures over shared `IClassFixture` state
  when isolation matters.
- **SQL verification**: assert on `query.ToQueryString()` output alongside the
  materialized rows — a recurring pattern in the specifications tests that confirms
  filtering is translated to SQL, not run in memory.

## Why real databases

Integration tests run against TestContainers.MsSql (Docker required) rather than EF
Core InMemory, because the in-memory provider masks SQL-specific behavior. This is what
validates features such as [[specifications]], the [[dynamic-predicate-builder]]
(including the mandatory `.AsExpandable()` expansion), and [[data-authorization]]'s
query-filter translation.

## Coverage targets

| Area | Target |
|---|---|
| Core libraries | 99% line |
| EfCore libraries | 95% line |
| Service libraries | 90% line |
| CI gate (overall) | 80% line |

The primary CI pipeline restores, builds in Release, runs tests with OpenCover
coverage, runs SonarCloud analysis, enforces the 80% threshold, and comments coverage
on PRs. Tests should encode *why* behavior matters, exercising the domain rules in
[[domain-driven-design]] and the persistence behaviors built on [[repositories]].
