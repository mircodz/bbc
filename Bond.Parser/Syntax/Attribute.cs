namespace Bond.Parser.Syntax;

/// <summary>
/// Represents a custom attribute applied to declarations
/// </summary>
public record Attribute(
    string[] QualifiedName,
    string Value
)
{
    public override string ToString() =>
        $"[{string.Join(".", QualifiedName)}(\"{Value}\")]";
}
