---
title: Idempotency
category: ASP.NET Core
tags: [aspnetcore, idempotency, middleware, mssql, reliability]
---

## Summary

`DKNet.AspCore.Idempotency` is endpoint middleware that makes HTTP operations
idempotent: repeated requests carrying the same idempotency key return the original
cached response instead of executing the operation again. It is backed by a pluggable
store, with `DKNet.AspCore.Idempotency.MsSqlStore` providing a SQL Server-backed
implementation.

## What the middleware does

The middleware intercepts requests bearing an idempotency key, caches the HTTP
response (including the status code), and replays it on retries. It performs composite
key validation and wraps the cache interactions with exception handling, supporting
fail-open and fail-closed error behaviors so applications can choose whether a cache
outage blocks or permits the underlying operation.

## The MS SQL store

`MsSqlStore` persists idempotency keys using EF Core 10 with its own `DbContext` and
schema. Concurrency is handled via a unique index combined with an insert-or-query
pattern, so two simultaneous requests with the same key resolve deterministically. The
store is registered in `Program.cs` alongside the middleware, and the store interface
is pluggable — other backends can be supplied.

## Where it fits

Idempotency depends on the core utilities in [[fw-extensions]] and uses the
FluentResults pattern. It pairs with the other ASP.NET Core utilities —
[[background-tasks]] and [[aspcore-extensions]] — to harden web applications built on
DKNet's [[onion-architecture]].
