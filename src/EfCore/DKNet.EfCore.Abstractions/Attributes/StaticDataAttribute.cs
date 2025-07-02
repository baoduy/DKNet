using System.ComponentModel.DataAnnotations.Schema;

namespace DKNet.EfCore.Abstractions.Attributes;

/// <summary>
/// This attribute will load the enums into a table in the database.
/// When applied to an enum, it indicates that the enum values should be
/// persisted in a static data table within the database. This is particularly
/// useful for maintaining reference data that doesn't change frequently.
/// </summary>
[AttributeUsage(AttributeTargets.Enum)]
public sealed class StaticDataAttribute(string name) : TableAttribute(name);
