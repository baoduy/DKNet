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
