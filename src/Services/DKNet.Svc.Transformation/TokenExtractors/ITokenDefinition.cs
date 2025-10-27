namespace DKNet.Svc.Transformation.TokenExtractors;

public interface ITokenDefinition
{
    #region Properties

    string BeginTag { get; }

    string EndTag { get; }

    #endregion

    #region Methods

    bool IsToken(string value);

    #endregion
}

public class TokenDefinition(string begin, string end) : ITokenDefinition
{
    #region Properties

    public string BeginTag { get; } = begin;
    public string EndTag { get; } = end;

    #endregion

    #region Methods

    public bool IsToken(string value)
    {
        if (string.IsNullOrWhiteSpace(value)) return false;

        var span = value.AsSpan();

        if (!span.StartsWith(BeginTag.AsSpan(), StringComparison.OrdinalIgnoreCase)) return false;
        if (!span.EndsWith(EndTag.AsSpan(), StringComparison.OrdinalIgnoreCase)) return false;

        var inner = span.Slice(BeginTag.Length, span.Length - BeginTag.Length - EndTag.Length);
        if (inner.Length == 0) return false;

        foreach (var c in BeginTag)
            if (inner.Contains(c))
                return false;

        foreach (var c in EndTag)
            if (inner.Contains(c))
                return false;

        return true;
    }

    #endregion
}