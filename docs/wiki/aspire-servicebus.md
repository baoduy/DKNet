---
title: Aspire Service Bus Hosting
category: Infrastructure & Messaging
tags: [aspire, service-bus, orchestration, hosting, emulator]
---

## Summary

`DKNet.Aspire.Hosting.ServiceBus` is a .NET Aspire hosting integration that orchestrates
an Azure Service Bus resource — including the Service Bus emulator — as part of an
Aspire app host. It lets distributed DKNet applications declare and run their messaging
infrastructure locally and in deployment without bespoke setup.

## What it provides

The package adds Service Bus emulator hosting to an Aspire AppHost, so a Service Bus
resource can be wired into the distributed application model alongside other
orchestrated resources. This makes local development and integration testing of
message-driven workflows reproducible.

## Relationship to messaging

Aspire orchestration provisions the transport; the application's CQRS and event flow
runs through [[cqrs-slimbus]], where the `SlimBusEventPublisher` forwards
[[domain-events]] to SlimMessageBus. Service Bus can serve as the backing transport for
that bus in a distributed deployment, connecting the in-process domain model to
out-of-process consumers.

## Where it fits

This package is DKNet's Infrastructure & Orchestration layer in [[onion-architecture]]
terms — it sits outside the domain entirely, provisioning resources rather than
implementing business logic. It complements the persistence and web rings without the
domain ever depending on it.
