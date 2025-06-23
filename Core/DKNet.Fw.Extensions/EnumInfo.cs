using System.Diagnostics.CodeAnalysis;

namespace DKNet.Fw.Extensions;

[ExcludeFromCodeCoverage]
public sealed record EnumInfo
{
    public string? Description { get; init; }

    public string? GroupName { get; init; }

    public required string Key { get; init; }

    public required string Name { get; init; }
    
}