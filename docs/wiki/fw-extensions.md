---
title: DKNet.Fw.Extensions
category: Core Utilities
tags: [core, extensions, string, datetime, enum, type]
---

## Summary

`DKNet.Fw.Extensions` is DKNet's foundational core utility library — a
framework-agnostic package of helper extensions for String, DateTime, Enum, Type, and
async enumerables that the rest of the suite builds upon. It carries no domain or
infrastructure dependencies, which is why it sits in the innermost ring of
[[onion-architecture]].

## What it provides

The library groups small, focused extension methods in static classes (per the
project's `/Extensions` convention):

- **String helpers** — common formatting, parsing, and value-type detection utilities.
- **DateTime helpers** — date manipulation and comparison helpers.
- **Enum helpers** — conversion and metadata access for enum types.
- **Type helpers** — reflection-oriented checks used across the framework.
- **Async enumerable** support for streaming sequences.

Its changelog tracks these as the String, DateTime, and Enum extension areas.

## Role across the suite

Because it is dependency-light and broadly useful, `DKNet.Fw.Extensions` is referenced
by higher-level packages — for example `DKNet.AspCore.Idempotency` depends on it (see
[[idempotency]]). Treating it as the shared base keeps utility logic centralized
rather than duplicated.

## Relation to other core pieces

It is paired with [[random-creator]] as the two Core Foundation packages, and it
underpins the EF Core and ASP.NET Core layers. New cross-cutting helpers should land
here rather than being reinvented in feature packages; the same discipline applies to
the testable, well-documented public APIs described in [[testing-strategy]].
