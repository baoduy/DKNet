// Copyright (c) https://drunkcoding.net. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// Author: DRUNK Coding Team
// File: ListFilter.cs
// Description: A single field/operation/value filter condition accepted by the generic list endpoints, bindable from a query string.

using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using System.Text.Json.Serialization;
using DKNet.EfCore.Specifications.Dynamics;

namespace DKNet.AspCore.Extensions.Endpoints;

/// <summary>
///     One filter condition — a field, an operation, and a value — as accepted by
///     <see cref="FluentsEntityEndpointMapperExtensions.MapGetList{TEntity,TModel}" />.
/// </summary>
/// <remarks>
///     The listing endpoints are HTTP GETs, so conditions arrive in the query string rather than a body.
///     Implementing <see cref="IParsable{TSelf}" /> is what lets minimal APIs bind a repeated
///     <c>?filter=…&amp;filter=…</c> straight into a <see cref="ListFilter" /> array, so the endpoint signature
///     and handler work in terms of this type while the wire format stays a plain, shareable, cacheable URL.
///     Callers composing filters in code construct it directly rather than formatting strings.
/// </remarks>
/// <param name="Field">
///     Name of the field to filter on. Matched case-insensitively against the endpoint's model, and accepts
///     <c>snake_case</c> or <c>kebab-case</c> spellings of it.
/// </param>
/// <param name="Operation">The comparison to apply.</param>
/// <param name="Value">
///     The value to compare against, as text; coerced to the field's CLR type when the filter is applied.
///     For <see cref="Ops.In" />/<see cref="Ops.NotIn" /> this is a comma-separated list.
/// </param>
[JsonConverter(typeof(ListFilterJsonConverter))]
public readonly record struct ListFilter(string Field, Ops Operation, string Value) : IParsable<ListFilter>
{
    #region Fields

    /// <summary>Separates the three parts of the textual form.</summary>
    private const char PartSeparator = ':';

    /// <summary>Number of parts in the textual form: field, operation, value.</summary>
    private const int PartCount = 3;

    #endregion

    #region Methods

    /// <summary>
    ///     Parses the textual <c>field:operation:value</c> form used in the query string.
    /// </summary>
    /// <param name="s">The text to parse.</param>
    /// <param name="provider">Unused; the format is culture-invariant.</param>
    /// <returns>The parsed filter.</returns>
    /// <exception cref="FormatException"><paramref name="s" /> is not a valid filter.</exception>
    public static ListFilter Parse(string s, IFormatProvider? provider) =>
        TryParse(s, provider, out var result)
            ? result
            : throw new FormatException(
                $"'{s}' is not a valid filter. Expected 'field:operation:value' where operation is one of: " +
                $"{string.Join(", ", Enum.GetNames<Ops>())}.");

    /// <summary>
    ///     Attempts to parse the textual <c>field:operation:value</c> form used in the query string.
    /// </summary>
    /// <param name="s">The text to parse.</param>
    /// <param name="provider">Unused; the format is culture-invariant.</param>
    /// <param name="result">The parsed filter on success; otherwise the default value.</param>
    /// <returns><see langword="true" /> when <paramref name="s" /> was a valid filter; otherwise <see langword="false" />.</returns>
    public static bool TryParse([NotNullWhen(true)] string? s, IFormatProvider? provider, out ListFilter result)
    {
        result = default;
        if (string.IsNullOrWhiteSpace(s)) return false;

        // Split on the first two separators only, so a value may contain separators of its own — an ISO-8601
        // timestamp being the case that matters.
        var parts = s.Split(PartSeparator, PartCount);
        if (parts.Length != PartCount || parts[0].Length == 0) return false;
        if (!Enum.TryParse<Ops>(parts[1], ignoreCase: true, out var operation)) return false;

        result = new ListFilter(parts[0], operation, parts[2]);
        return true;
    }

    /// <summary>Renders the filter back into its textual <c>field:operation:value</c> form.</summary>
    /// <returns>The textual form, as it would appear in a query string.</returns>
    public override string ToString() => $"{Field}{PartSeparator}{Operation}{PartSeparator}{Value}";

    #endregion
}

/// <summary>
///     Serializes a <see cref="ListFilter" /> as its textual <c>field:operation:value</c> form.
/// </summary>
/// <remarks>
///     The textual form IS the type's representation — it is what the query string carries — so JSON should
///     carry the same string rather than an object of three properties. This is also what makes the OpenAPI
///     document describe the <c>filter</c> parameter as an array of strings: the schema follows the JSON
///     shape, and an object schema there would have every Swagger UI offering a JSON form for a parameter the
///     endpoint can only bind from the colon-separated string.
/// </remarks>
public sealed class ListFilterJsonConverter : JsonConverter<ListFilter>
{
    /// <inheritdoc />
    public override ListFilter Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        => ListFilter.Parse(reader.GetString() ?? string.Empty, provider: null);

    /// <inheritdoc />
    public override void Write(Utf8JsonWriter writer, ListFilter value, JsonSerializerOptions options)
        => writer.WriteStringValue(value.ToString());
}
