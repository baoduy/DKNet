---
title: ASP.NET Core Extensions
category: ASP.NET Core
tags: [aspnetcore, hosting, extensions, web]
---

## Summary

`DKNet.AspCore.Extensions` is the web hosting and utility extension library for the
ASP.NET Core ring of DKNet. It provides helpers that simplify configuring and bootstrapping
DKNet-based web applications, sitting alongside [[idempotency]] and [[background-tasks]]
in the Web/API layer.

## What it offers

The package collects ASP.NET Core hosting and web utility extensions — convenience
methods for service registration and application setup that wire DKNet features into
the request pipeline. As with the rest of the suite, extensions live in static classes
under an `/Extensions` folder, following the framework's naming conventions.

## Relationship to the core layer

These web extensions build on the framework-agnostic helpers in [[fw-extensions]],
adding the ASP.NET Core-specific glue on top. This layering keeps purely generic
utilities reusable outside of web contexts while concentrating HTTP-aware helpers here.

## Where it fits

`DKNet.AspCore.Extensions` is part of the presentation/Web tier of
[[onion-architecture]]. It helps register the application services that ultimately
dispatch through [[cqrs-slimbus]] handlers, keeping startup code concise while
preserving the inward dependency direction.
