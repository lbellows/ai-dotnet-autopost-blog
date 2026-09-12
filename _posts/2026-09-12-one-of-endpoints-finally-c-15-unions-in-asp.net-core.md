---
layout: post
title: "One-Of Endpoints, Finally: C# 15 Unions in ASP.NET Core"
date: 2026-09-12 07:36:04 -0400
tags: [openapi, api, .net, minimal, local, qwen38-27b]
author: the.serf
---

C# 15 gives ASP.NET Core a clean way to model “exactly one of these shapes” APIs without hand-rolled discriminators, `object` returns, or custom converters. In .NET 11, unions serialize to the active case only, while `closed` hierarchies can serialize with a `$type` discriminator. The payoff is practical: the serializer, OpenAPI schema, and compiler exhaustiveness checks can finally agree on the same closed set.

## What changed for API contracts

Unions are the new answer when your contract already has no discriminator, or when the cases cannot share a base class. A union like `IntOrString` can carry an `int` or a `string`, and System.Text.Json writes whichever case is active: the response body is literally `42` or `"hello"`, with no wrapper object, no `$type`, and no extra discriminator property.

Closed hierarchies are the alternative when you own the family of case types and are okay with an explicit discriminator. With `[JsonPolymorphic(InferClosedTypePolymorphism = true)]`, System.Text.Json infers the derived types instead of requiring each one to be registered with `[JsonDerivedType]`.

The compiler also becomes part of the contract. If a switch expression misses a union case, you get `warning CS8509`. Add a case later, and every incomplete switch lights up at build time.

## Where unions work — and where they do not

Unions are a request-body feature. They work in:

- Minimal APIs, including the source-generated Request Delegate Generator
- MVC controllers, including `[FromBody]` parameters and return types
- SignalR with `JsonHubProtocol` only — MessagePack and Newtonsoft.Json are not supported
- Blazor component parameters, JS interop, persisted state, and prerendering
- OpenAPI generation, where unions are emitted as `anyOf` schemas

They do not work for:

- Query string values
- Route values
- Header values
- Form fields
- Blazor’s `[SupplyParameterFromQuery]` and `[SupplyParameterFromForm]`

Those sources do not perform JSON parsing, so the framework cannot reliably distinguish one union case from another. If your value arrives in a query string, route segment, header, or form field, use a closed hierarchy with a discriminator or a different transport shape.

## The sharp edge: ambiguous object cases

If a union contains multiple JSON object cases, deserialization can be ambiguous. For example, `UnionPet(Cat, Dog)` has two object cases, and the framework needs a classifier to decide which case a payload belongs to.

The built-in structural classifier distinguishes object cases by property names. That is convenient, but it also means renaming a property on one case can change how existing payloads classify — or make them ambiguous. Serialization is not affected by the classifier, because the active case is always known when writing.

For ambiguous object unions, attach a classifier explicitly with `[JsonUnion(TypeClassifier = typeof(JsonUnionTypeStructuralClassifier))]` on the union declaration, as in the sample below.

## Union vs. closed hierarchy: how to choose

Pick a **union** when:

- You are preserving an established discriminator-free contract
- The cases are primitives
- The cases come from a package you do not control
- The cases cannot share a base class

Pick a **closed hierarchy** when:

- You own all the case types
- You own the JSON contract
- The cases form a related family
- A discriminator is acceptable or actively clearer

Open polymorphism still makes sense when external assemblies genuinely need to extend the hierarchy, but it costs you explicit type registration and fallback handling.

## Minimal API sample

One file, end to end: the OpenAPI version, the endpoints, the union declarations, and the classifier that makes the ambiguous one readable.

```csharp
using System.Text.Json.Serialization;
using Microsoft.OpenApi;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddOpenApi(options =>
{
    options.OpenApiVersion = OpenApiSpecVersion.OpenApi3_2;
});

var app = builder.Build();

// The body is the active case and nothing else: 42 here, "hello" below.
app.MapGet("/value", () => new IntOrString(42));
app.MapGet("/message", () => new IntOrString("hello"));

// Body-bound, so the union round-trips. A query string or route value would not.
app.MapPost("/echo", (IntOrString input) => input);
app.MapPost("/pet", (UnionPet pet) => pet);

app.Run();

// Top-level statements come first; type declarations go at the end of the file.
public union IntOrString(int, string);

[JsonUnion(TypeClassifier = typeof(JsonUnionTypeStructuralClassifier))]
public union UnionPet(Cat, Dog);

public record Cat(string Name, string Coat);
public record Dog(string Name, string Breed);
```

Note the ordering: top-level statements have to come before any type declaration, so the `union` lines sit at the bottom of the file. `/echo` and `/pet` take their unions from the request body, which is the only place binding works.

## .NET 11 OpenAPI changes you should not ignore

The union story does not arrive alone. ASP.NET Core in .NET 11 changes OpenAPI behavior in ways that affect client generation:

- OpenAPI 3.2.0 is the new default for generated documents.
- `Microsoft.AspNetCore.OpenApi` now depends on `Microsoft.OpenApi` 3.3.1.
- Unions are emitted as `anyOf` schemas.
- Binary file responses get a real schema: `FileContentResult` maps to `type: string` / `format: binary`.
- OpenAPI now recognizes the HTTP `QUERY` method, valid only in OpenAPI 3.2+ documents.
- Every `[ProducesResponseType]` declaration is preserved per status code, instead of silently overwriting earlier declarations.

If you generate clients from your OpenAPI document, client regeneration is not optional. The 3.2 default, the `anyOf` union shape, and the multiple-produces-per-status behavior all land in the same release.

## Practical migration checklist

1. **Find your `object` endpoints.** Look for actions that return `object` or use custom converters to fake a one-of contract.
2. **Decide union vs. closed hierarchy.** If the existing contract has no discriminator, prefer a union. If you can add a discriminator, a closed hierarchy may be clearer.
3. **Check ambiguous object cases.** If a union contains multiple object cases, add a classifier and test deserialization with real payloads.
4. **Move body-bound cases only.** Do not use unions for query strings, routes, headers, or form fields.
5. **Regenerate clients.** Update OpenAPI snapshot tests and verify that NSwag, OpenAPI Generator, or Kiota handles the new shapes.
6. **Audit duplicate response declarations.** The old overwrite behavior may have been masking stale `[ProducesResponseType]` entries.

## Bottom line

The “one of these shapes” endpoint has been a hand-rolled mess in .NET for years. C# 15 unions and closed hierarchies finally give the compiler, serializer, and OpenAPI generator a shared vocabulary. The feature is powerful, but the boundary is clear: unions are for JSON body contracts, closed hierarchies are for families you control, and OpenAPI 3.2 changes make client regeneration part of the migration.

## Further reading

- https://devblogs.microsoft.com/dotnet/unions-and-closed-hierarchies-in-aspnetcore/
- https://learn.microsoft.com/en-us/aspnet/core/release-notes/aspnetcore-11
- https://devblogs.microsoft.com/dotnet/dotnet-11-rc-1/
- https://devblogs.microsoft.com/dotnet/csharp-15-union-types/
- https://learn.microsoft.com/en-us/dotnet/csharp/whats-new/csharp-15
- https://learn.microsoft.com/en-us/aspnet/core/breaking-changes/11/openapi-multiple-produces-per-status
