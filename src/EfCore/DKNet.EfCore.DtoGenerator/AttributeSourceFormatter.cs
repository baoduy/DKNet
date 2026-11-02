using System;
using System.Collections.Immutable;
using System.Globalization;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace DKNet.Generators.Shared;

/// <summary>
/// Renders an <see cref="AttributeData"/> back into C# attribute-usage source. This file is the single
/// copy of the argument formatting: it is compiled into <c>DKNet.EfCore.DtoGenerator</c> and
/// <c>Compile Include</c>-linked into <c>DKNet.SlimBus.Generators</c> (analyzer projects cannot
/// reference each other), so both generators emit the same, compilable argument text.
/// </summary>
internal static class AttributeSourceFormatter
{
    /// <summary>
    /// Formats <paramref name="attribute"/> as <c>[Name]</c> or <c>[Name(args)]</c>.
    /// </summary>
    /// <param name="attributeName">The name to render; each caller decides its qualification and suffix.</param>
    /// <param name="attribute">The attribute data whose constructor and named arguments are rendered.</param>
    /// <returns>The attribute-usage source.</returns>
    public static string Format(string attributeName, AttributeData attribute)
    {
        // A `params` constructor argument (e.g. SensitiveDataAttribute(params string[] roles)) arrives
        // here as a single Array-kind TypedConstant. Spread its values as individual arguments — the
        // natural attribute-usage form ("pricing", "audit") rather than an array-creation literal — and
        // an empty array spreads to zero arguments instead of the uncompilable "new[] {  }". An
        // explicit `null` array argument (e.g. [SensitiveData(null)]) is still Array-kind but its
        // Values is default — spreading that throws, so route it through FormatArgument's own null
        // handling instead. Only a genuinely `params` constructor parameter
        // is spread — a plain (non-`params`) array parameter keeps its array-creation form, resolved
        // positionally against the constructor actually bound. A lone null
        // element (e.g. [AllowedValues((object)null)]) keeps its array form: spread to a bare `null!` it
        // would rebind to the whole params array being null, not to a one-element array holding null.
        var parameters = attribute.AttributeConstructor?.Parameters ?? ImmutableArray<IParameterSymbol>.Empty;
        var arguments = attribute.ConstructorArguments
            .SelectMany((arg, i) => arg.Kind == TypedConstantKind.Array && !arg.IsNull &&
                                     i < parameters.Length && parameters[i].IsParams &&
                                     !(arg.Values.Length == 1 && arg.Values[0].IsNull)
                ? arg.Values.Select(FormatArgument)
                : [FormatArgument(arg)])
            .Concat(attribute.NamedArguments.Select(named => $"{named.Key} = {FormatArgument(named.Value)}"))
            .ToList();

        return arguments.Count == 0 ? $"[{attributeName}]" : $"[{attributeName}({string.Join(", ", arguments)})]";
    }

    private static string FormatArgument(TypedConstant arg)
    {
        // The null-forgiving `!` keeps this a valid attribute-argument constant while suppressing every
        // nullable warning at both the literal (CS8625) and the call site (CS8604) regardless of the
        // target parameter's declared type — including a target `arg.Type` that is null or an error type,
        // which a type-driven cast/`default` form could not handle uniformly.
        if (arg.IsNull)
            return "null!";

        if (arg.Kind == TypedConstantKind.Array)
        {
            // An implicitly-typed `new[] { ... }` fails to compile for an empty array (CS0826: no best
            // type found) or an array containing only a null element (CS1503 — a lone `null!` carries no
            // inferable type) — both real shapes for a non-`params` array constructor argument. Naming the argument's own declared element type sidesteps inference
            // entirely. The untyped `new[] { ` fallback below is defensive rather than reachable: an attribute
            // constructor whose array parameter has an unresolved element type never binds to an
            // Array-kind TypedConstant in practice — Roslyn instead collapses the whole argument to
            // TypedConstantKind.Error (undeclared element type), leaves AttributeConstructor/
            // ConstructorArguments empty (missing-assembly-reference element type), or resolves cleanly
            // with IsNull true (ambiguous overload picks the valid one) — verified empirically against
            // five constructions rather than assumed.
            // Fully qualified because the generated files' usings do not cover arbitrary namespaces — an
            // element type from any other namespace would not resolve otherwise. FullyQualifiedFormat sets
            // UseSpecialTypes, so `string` still renders `string`.
            var elementTypeName = arg.Type is IArrayTypeSymbol { ElementType.TypeKind: not TypeKind.Error } arrayType
                ? arrayType.ElementType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat)
                : null;

            var prefix = elementTypeName is not null ? $"new {elementTypeName}[] {{ " : "new[] { ";
            return prefix + string.Join(", ", arg.Values.Select(FormatArgument)) + " }";
        }

        // TypedConstant.Kind — not the parameter's declared SpecialType — is the only discriminator that
        // reaches every typeof(...) shape: object-typed and array-element parameters share Kind.Type with
        // a Type-typed parameter, but a SpecialType.System_Object guard only ever matches the object-typed
        // case. Fully qualified for the same reason as array elements.
        if (arg.Kind == TypedConstantKind.Type && arg.Value is ITypeSymbol typeSymbol)
        {
            return $"typeof({typeSymbol.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat)})";
        }

        if (arg.Kind == TypedConstantKind.Enum && arg.Type is not null)
        {
            // arg.Value is the boxed underlying integer and arg.Type.Name carries no namespace, so both
            // resolving the actual member and qualifying the enum type are required for the emitted
            // argument to compile outside the generated files' usings. A flags combination or an
            // out-of-range value has no single matching field — fall back to a parenthesised cast, which
            // still compiles for any integral underlying type.
            var qualifiedEnumType = arg.Type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
            var matchingMember = arg.Type.GetMembers()
                .OfType<IFieldSymbol>()
                .FirstOrDefault(f => f.HasConstantValue && Equals(f.ConstantValue, arg.Value));

            return matchingMember is not null
                ? $"{qualifiedEnumType}.{matchingMember.Name}"
                : $"(({qualifiedEnumType}){arg.Value})";
        }

        if (arg.Value is string stringValue)
        {
            // SymbolDisplay.FormatPrimitive escapes '\' and control characters as well as '"' — a
            // hand-rolled Replace("\"", "\\\"") missed both.
            return SymbolDisplay.FormatPrimitive(stringValue, quoteStrings: true, useHexadecimalNumbers: false);
        }

        // `System.Boolean` does not implement `IFormattable`, so this arm's position relative to the
        // `IFormattable` arm below is immaterial — a boolean never reaches that arm either way. It exists
        // because without it a boolean falls through to the default `arg.Value?.ToString()` and emits
        // `True`/`False`, which do not bind to a `bool` attribute parameter. The BooleanArgument_* facts
        // pin the emitted text and close the arm's 0-of-2 branch gap.
        if (arg.Value is bool boolValue)
        {
            return boolValue ? "true" : "false";
        }

        if (arg.Value is char charValue)
        {
            // Same escaping gap as the string arm — a raw `'{c}'` breaks for `'`, `\`, and control
            // characters.
            return SymbolDisplay.FormatPrimitive(charValue, quoteStrings: true, useHexadecimalNumbers: false);
        }

        // Every numeric constant must re-parse as its own type, so the bound constructor overload and
        // each boxed value stay the same: an unsuffixed decimal-point literal binds to `double`, not
        // `float`/`decimal` (CS0664); an unsuffixed whole number binds to `int`; and `NaN`/`±Infinity`
        // have no C# literal form at all. These arms run ahead of the generic `IFormattable` arm below,
        // which only `int` still reaches.
        if (arg.Value is float floatValue)
        {
            if (float.IsNaN(floatValue))
                return "float.NaN";
            if (float.IsPositiveInfinity(floatValue))
                return "float.PositiveInfinity";
            if (float.IsNegativeInfinity(floatValue))
                return "float.NegativeInfinity";

            return SymbolDisplay.FormatPrimitive(floatValue, quoteStrings: false, useHexadecimalNumbers: false) + "f";
        }

        if (arg.Value is double doubleValue)
        {
            if (double.IsNaN(doubleValue))
                return "double.NaN";
            if (double.IsPositiveInfinity(doubleValue))
                return "double.PositiveInfinity";
            if (double.IsNegativeInfinity(doubleValue))
                return "double.NegativeInfinity";

            // A finite double with a fraction or exponent already reads back as `double` and keeps its
            // text; a whole number gets `.0` so it does not rebind to `int` (e.g. Range(0.0, 100.0)).
            var doubleText = doubleValue.ToString(null, CultureInfo.InvariantCulture);
            return doubleText.IndexOfAny(['.', 'E', 'e']) < 0 ? doubleText + ".0" : doubleText;
        }

        // Integral types other than `int` have no literal of their own type for small values: `long`,
        // `uint` and `ulong` take a suffix, the narrower types a cast.
        switch (arg.Value)
        {
            case long longValue:
                return longValue.ToString(CultureInfo.InvariantCulture) + "L";
            case uint uintValue:
                return uintValue.ToString(CultureInfo.InvariantCulture) + "U";
            case ulong ulongValue:
                return ulongValue.ToString(CultureInfo.InvariantCulture) + "UL";
            case short shortValue:
                return $"((short){shortValue.ToString(CultureInfo.InvariantCulture)})";
            case ushort ushortValue:
                return $"((ushort){ushortValue.ToString(CultureInfo.InvariantCulture)})";
            case byte byteValue:
                return $"((byte){byteValue.ToString(CultureInfo.InvariantCulture)})";
            case sbyte sbyteValue:
                return $"((sbyte){sbyteValue.ToString(CultureInfo.InvariantCulture)})";
        }

        if (arg.Value is decimal decimalValue)
        {
            return SymbolDisplay.FormatPrimitive(decimalValue, quoteStrings: false, useHexadecimalNumbers: false) + "m";
        }

        if (arg.Value is IFormattable formattableValue)
        {
            return formattableValue.ToString(null, CultureInfo.InvariantCulture);
        }

        // Default: ToString
        return arg.Value?.ToString() ?? "null";
    }
}
