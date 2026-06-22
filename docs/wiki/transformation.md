---
title: Transformation
category: Service Adapters
tags: [transformation, tokens, formatting, templating]
---

## Summary

`DKNet.Svc.Transformation` provides token and data transformation capabilities: it
resolves named tokens against supplied data sources and formats the resolved values
into strings. It is useful for templating scenarios — substituting placeholders in
text from objects, dictionaries, or collections — and is one of DKNet's pluggable
service adapters.

## Token resolution

`ITokenResolver` (implemented by `TokenResolver`) exposes synchronous and asynchronous
resolve operations that look up a token's value from the supplied data: collections,
dictionaries, or object properties. It returns the first matching value, so multiple
data sources can be layered. String helpers in the package add value-type detection and
string transformation utilities.

## Value formatting

`IValueFormatter` defines the contract for converting a resolved token value into its
string representation. Separating resolution from formatting lets applications control
how values render independently of how they are looked up.

## Where it fits

Transformation sits in the Service Adapters ring of [[onion-architecture]] alongside
[[blob-storage]], [[pdf-generators]], and [[encryption-services]]. Its string helpers
complement the broader core utilities in [[fw-extensions]]; resolved/templated text can
feed downstream services such as document generation in [[pdf-generators]].
