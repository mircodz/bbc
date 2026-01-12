namespace Bond.Parser.Syntax;

/// <summary>
/// Represents a service declaration
/// </summary>
public sealed record ServiceDeclaration : Declaration
{
    public required Attribute[] Attributes { get; init; }
    public BondType? BaseType { get; init; }
    public required Method[] Methods { get; init; }

    public override string Kind => "service";

    public override string ToString()
    {
        var typeParams = TypeParameters.Length > 0
            ? $"<{string.Join(", ", TypeParameters.Select(p => p.ToString()))}>"
            : "";
        var baseType = BaseType != null ? $" : {BaseType}" : "";
        return $"service {Name}{typeParams}{baseType}";
    }
}
