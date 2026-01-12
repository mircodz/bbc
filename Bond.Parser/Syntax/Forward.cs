namespace Bond.Parser.Syntax;

/// <summary>
/// Represents a forward declaration of a struct
/// </summary>
public sealed record ForwardDeclaration : Declaration
{
    public override string Kind => "forward declaration";

    public override string ToString() =>
        TypeParameters.Length > 0
            ? $"struct {Name}<{string.Join(", ", TypeParameters.Select(p => p.ToString()))}>"
            : $"struct {Name}";
}
