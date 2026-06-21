# DKNet Wiki — Content Catalog

An LLM-maintained knowledge wiki for the **DKNet Framework** — a .NET 10 NuGet
library suite for enterprise applications built on Domain-Driven Design (DDD) and
Onion Architecture. Browse articles by category below; see [[testing-strategy]] for
quality gates and `CLAUDE.md` for wiki conventions.

## Architecture & Concepts

- [[onion-architecture]] — How DKNet enforces inward-flowing dependencies across package boundaries.
- [[domain-driven-design]] — DDD building blocks (aggregates, value objects, events) as expressed in DKNet.
- [[aggregate-root]] — The `AggregateRoot` base type that marks consistency boundaries and carries events.
- [[domain-events]] — Event records raised by aggregates and dispatched during SaveChanges.
- [[cqrs-slimbus]] — CQRS command/query handling via `DKNet.SlimBus.Extensions`.

## Core Utilities

- [[fw-extensions]] — `DKNet.Fw.Extensions`, the foundational String/DateTime/Enum/Type helper library.
- [[random-creator]] — `DKNet.RandomCreator` secure random string and character generation.

## EF Core Persistence

- [[efcore-abstractions]] — DDD contracts: `AggregateRoot`, entities, and domain-event base types.
- [[specifications]] — Composable query objects (Criteria/Includes/OrderBy) built with LinqKit.
- [[dynamic-predicate-builder]] — Runtime EF Core predicates from `(property, op, value)` triples.
- [[repositories]] — Read/write repository abstractions and generic implementations.
- [[savechanges-hooks]] — Before/after SaveChanges interceptor pipeline.
- [[audit-logs]] — Automatic created/updated audit tracking via `[AuditLog]`.
- [[column-encryption]] — Column-level encryption for `[Encrypted]` string properties.
- [[data-authorization]] — Ownership-based row-level filtering via EF Core query filters.
- [[dto-generator]] — Roslyn incremental source generator driven by `[GenerateDto]`.

## ASP.NET Core

- [[idempotency]] — Endpoint idempotency middleware with a pluggable store.
- [[background-tasks]] — `IBackgroundTask` jobs run by a hosted `BackgroundService`.
- [[aspcore-extensions]] — ASP.NET Core hosting and web utility extensions.

## Service Adapters

- [[blob-storage]] — Provider-agnostic `IBlobService` with Azure, AWS S3, and Local adapters.
- [[pdf-generators]] — Markdown/HTML-to-PDF generation service.
- [[encryption-services]] — Standalone AES/AES-GCM/RSA/SHA/HMAC cryptographic services.
- [[transformation]] — Token resolution and value transformation utilities.

## Infrastructure & Messaging

- [[aspire-servicebus]] — `DKNet.Aspire.Hosting.ServiceBus` orchestration integration.

## Quality & Testing

- [[testing-strategy]] — xUnit + Shouldly + TestContainers.MsSql conventions and coverage targets.
