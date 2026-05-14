namespace Bond.Parser.Syntax;

public record Attribute(
    string[] QualifiedName,
    string Value
)
{
    public override string ToString() =>
        $"[{string.Join(".", QualifiedName)}(\"{Value}\")]";
}
