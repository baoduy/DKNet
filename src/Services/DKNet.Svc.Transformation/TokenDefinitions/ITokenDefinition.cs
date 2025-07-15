namespace DKNet.Svc.Transformation.TokenDefinitions;

public interface ITokenDefinition
{
    string BeginTag { get; }

    string EndTag { get; }

    bool IsToken(string value);
}