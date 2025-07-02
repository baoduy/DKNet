namespace DKNet.Svc.Transformation.TokenDefinitions;

public class TokenDefinition(string begin, string end) : ITokenDefinition
{
    public string BeginTag { get; } = begin;
    public string EndTag { get; } = end;

    public bool IsToken(string value) =>
        !string.IsNullOrWhiteSpace(value)
            && value.StartsWith(BeginTag, StringComparison.OrdinalIgnoreCase)
            && value.EndsWith(EndTag, StringComparison.OrdinalIgnoreCase);
}