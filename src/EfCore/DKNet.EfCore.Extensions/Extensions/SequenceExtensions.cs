using DKNet.Fw.Extensions;
using DKNet.Fw.Extensions.TypeExtractors;

namespace DKNet.EfCore.Extensions.Extensions;

internal static class SequenceExtensions
{
    #region Methods

    internal static SqlSequenceAttribute? GetAttribute(Type enumType) =>
        enumType.GetCustomAttribute<SqlSequenceAttribute>();

    internal static SequenceAttribute GetFieldAttributeOrDefault(Type enumType, object field) =>
        enumType.GetMember(field.ToString()!)[0].GetCustomAttribute<SequenceAttribute>() ??
        new SequenceAttribute();

    internal static string GetSequenceName(object field) => $"Seq_{field}";

    /// <summary>
    ///     Register Sequence from Enums
    /// </summary>
    /// <param name="modelBuilder"></param>
    /// <param name="assemblies"></param>
    internal static void RegisterSequences(this ModelBuilder modelBuilder, params Assembly[] assemblies)
    {
        var sequenceEnums = assemblies.SelectMany(a => a.Extract().Enums()).ToList()
            .Where(e => e.HasAttribute<SqlSequenceAttribute>())
            .ToList();

        foreach (var type in sequenceEnums)
            modelBuilder.RegisterSequencesFromEnumType(type);
    }

    private static void RegisterSequencesFromEnumType(this ModelBuilder modelBuilder, Type enumType)
    {
        var att = GetAttribute(enumType);
        if (att == null) return;

        var fields = Enum.GetValues(enumType);

        foreach (var f in fields)
        {
            var fieldAtt = GetFieldAttributeOrDefault(enumType, f);
            var name = GetSequenceName(f);

            var seq = modelBuilder.HasSequence(fieldAtt.Type, name, att.Schema);

            if (fieldAtt.StartAt > 0)
                seq.StartsAt(fieldAtt.StartAt);
            if (fieldAtt.IncrementsBy > 0)
                seq.IncrementsBy(fieldAtt.IncrementsBy);
            if (fieldAtt.Min > 0)
                seq.HasMin(fieldAtt.Min);
            if (fieldAtt.Max > 0)
                seq.HasMax(fieldAtt.Max);

            seq.IsCyclic(fieldAtt.Cyclic);
        }
    }

    #endregion
}