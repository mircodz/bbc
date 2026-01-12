namespace Bond.Parser.Syntax;

/// <summary>
/// Represents a type alias declaration
/// </summary>
public sealed record AliasDeclaration : Declaration
{
    public required BondType AliasedType { get; init; }

    public override string Kind => "alias";

    public override string ToString()
    {
        var typeParams = TypeParameters.Length > 0
            ? $"<{string.Join(", ", TypeParameters.Select(p => p.ToString()))}>"
            : "";
        return $"using {Name}{typeParams} = {AliasedType}";
    }
}
