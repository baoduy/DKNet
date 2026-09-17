// Copyright (c) https://drunkcoding.net. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// Author: DRUNK Coding Team
// File: RetiredErrorResponseHelpersTests.cs
// Description: DRK-1484 §5 @unit scenario — "The helpers that skipped the setting are gone". Reflects over the
// published public surface of DKNet.AspCore.Extensions.Responses rather than compiling a call to the retired
// members, so the test still compiles once Build actually deletes them (a compile-time reference would not).

using System.Net;
using System.Reflection;
using DKNet.AspCore.Extensions.Responses;
using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace AspCore.Extensions.Tests.Responses;

public class RetiredErrorResponseHelpersTests
{
    #region Methods

    [Fact]
    public void ToProblemDetails_ResultWithHttpStatusCodeOverload_IsRetired()
    {
        var stillExists = typeof(ProblemDetailsExtensions)
            .GetMethods(BindingFlags.Public | BindingFlags.Static)
            .Where(m => m.Name == nameof(ProblemDetailsExtensions.ToProblemDetails))
            .Any(m => m.GetParameters().Select(p => p.ParameterType)
                .SequenceEqual([typeof(FluentResults.IResultBase), typeof(HttpStatusCode)]));

        stillExists.ShouldBeFalse("ToProblemDetails(IResultBase, HttpStatusCode) answers without reading the setting and must be removed");
    }

    [Fact]
    public void ToProblemDetails_ModelStateDictionaryOverload_IsRetired()
    {
        var stillExists = typeof(ProblemDetailsExtensions)
            .GetMethods(BindingFlags.Public | BindingFlags.Static)
            .Where(m => m.Name == nameof(ProblemDetailsExtensions.ToProblemDetails))
            .Any(m => m.GetParameters().Select(p => p.ParameterType).SequenceEqual([typeof(ModelStateDictionary)]));

        stillExists.ShouldBeFalse("ToProblemDetails(ModelStateDictionary) answers without reading the setting and must be removed");
    }

    [Fact]
    public void ToProblemDetails_NoPublicOverloadAnswersWithoutReadingTheRegisteredSetting()
    {
        // REWORK round 1 (DRK-1490, important finding): a public `ToProblemDetails(IResultBase,
        // ErrorResponseOptions? = null)` let `result.ToProblemDetails()` compile and answer a failure while
        // skipping the host's registration. The only public path onto the standard body is now
        // Response()/Response<T>(), which resolve ErrorResponseOptions from the container themselves.
        var publicOverloads = typeof(ProblemDetailsExtensions)
            .GetMethods(BindingFlags.Public | BindingFlags.Static)
            .Where(m => m.Name == nameof(ProblemDetailsExtensions.ToProblemDetails))
            .ToList();

        publicOverloads.ShouldBeEmpty(
            "a public ToProblemDetails(...) lets a caller answer a failure without reading the registered " +
            "setting; use Response()/Response<T>() instead, which resolve it from the container on their own");
    }

    [Fact]
    public void Response_NoOverloadTakesErrorResponseOptionsDirectly()
    {
        var overloadsTakingOptions = typeof(ResultResponseExtensions)
            .GetMethods(BindingFlags.Public | BindingFlags.Static)
            .Where(m => m.Name == nameof(ResultResponseExtensions.Response))
            .Where(m => m.GetParameters().Any(p => p.ParameterType == typeof(ErrorResponseOptions)))
            .ToList();

        overloadsTakingOptions.ShouldBeEmpty(
            "Response(..., ErrorResponseOptions?, ...) lets an endpoint skip the registered setting and must be removed; " +
            "the helpers that remain read it on their own");
    }

    #endregion
}
