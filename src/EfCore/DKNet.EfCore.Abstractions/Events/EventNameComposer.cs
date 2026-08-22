using System.Text;

namespace DKNet.EfCore.Abstractions.Events;

/// <summary>
///     Composes the fixed-convention name of a generated <c>[RaisesEvent]</c> payload record. This file is
///     the single source of the naming algorithm: it is compiled directly into
///     <c>DKNet.EfCore.Abstractions</c> and <c>Compile Include</c>-linked into <c>DKNet.EfCore.DtoGenerator</c>
///     (which cannot reference this assembly), so the build and the save-time runtime always agree.
/// </summary>
public static class EventNameComposer
{
    // Mirrors DKNet.EfCore.Abstractions.Events.EventOperations without requiring the enum type itself,
    // so this file stays compilable when linked into the netstandard2.0 generator project.
    private const int OperationCreated = 1;
    private const int OperationUpdated = 2;
    private const int OperationDeleted = 4;

    /// <summary>
    ///     Composes the payload record name: entity name, then the label (when non-empty), then the
    ///     de-duplicated narrowing properties sorted with <see cref="System.StringComparer.Ordinal" />, then
    ///     the declared operations in the canonical order Created, Updated, Deleted, then the literal
    ///     suffix <c>Event</c>. A whitespace-only label is treated as present (not equivalent to an absent
    ///     label) and is composed verbatim — since it cannot form part of a valid C# identifier, callers
    ///     validating the result will reject it rather than silently generating a corrupted name.
    /// </summary>
    /// <param name="entityName">The carrying entity's simple (unqualified) name.</param>
    /// <param name="label">The declaration's optional label segment; absent when <see langword="null" /> or empty.</param>
    /// <param name="properties">The declaration's narrowing property names, if any.</param>
    /// <param name="operations">The declared operations as an <c>EventOperations</c> bitmask.</param>
    /// <returns>The composed record name.</returns>
    public static string Compose(string entityName, string? label, IReadOnlyList<string>? properties, int operations)
    {
        var name = new StringBuilder(entityName);

        if (!string.IsNullOrEmpty(label))
            name.Append(label);

        if (properties is { Count: > 0 })
        {
            foreach (var property in properties.Distinct(StringComparer.Ordinal)
                         .OrderBy(p => p, StringComparer.Ordinal))
                name.Append(property);
        }

        if ((operations & OperationCreated) != 0)
            name.Append("Created");
        if ((operations & OperationUpdated) != 0)
            name.Append("Updated");
        if ((operations & OperationDeleted) != 0)
            name.Append("Deleted");

        return name.Append("Event").ToString();
    }
}
