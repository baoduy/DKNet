---
title: Background Tasks
category: ASP.NET Core
tags: [aspnetcore, background-service, jobs, hosting]
---

## Summary

`DKNet.AspCore.Tasks` provides a lightweight background job model: units of work
implement the `IBackgroundTask` interface, and a hosted `BackgroundJobHost` discovers
and runs them within a service scope. It lets applications register recurring or
startup work without hand-rolling `BackgroundService` plumbing for each job.

## The `IBackgroundTask` contract

`IBackgroundTask` exposes a single `RunAsync` method representing a job to be executed.
Implementations are registered with DI, keeping each task small and focused on one
unit of work.

## The hosting service

`BackgroundJobHost` is a hosted `BackgroundService` that resolves all registered
`IBackgroundTask` implementations and executes them inside a service scope. Running in
a scope means each job gets correctly scoped dependencies (such as a `DbContext`),
avoiding the captive-dependency problems that arise when scoped services are used from
a singleton host.

## Where it fits

Background tasks complement the other ASP.NET Core utilities, [[idempotency]] and
[[aspcore-extensions]], in the Web/API ring of [[onion-architecture]]. A job can drive
domain behavior by resolving [[repositories]] and invoking aggregate methods, which in
turn emit [[domain-events]] through the persistence pipeline.
