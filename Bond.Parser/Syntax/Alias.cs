using System.Linq;

namespace Bond.Parser.Syntax;

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
