using System.Linq;

namespace Bond.Parser.Syntax;

public sealed record ForwardDeclaration : Declaration
{
    public override string Kind => "forward declaration";

    public override string ToString() =>
        TypeParameters.Length > 0
            ? $"struct {Name}<{string.Join(", ", TypeParameters.Select(p => p.ToString()))}>"
            : $"struct {Name}";
}
