namespace Bond.Parser.Syntax;

/// <summary>
/// Represents an enum declaration
/// </summary>
public sealed record EnumDeclaration : Declaration
{
    public required Attribute[] Attributes { get; init; }
    public required Constant[] Constants { get; init; }

    public override string Kind => "enum";

    // Enums don't have type parameters, so set to empty array
    public EnumDeclaration()
    {
        TypeParameters = [];
    }

    public override string ToString() => $"enum {Name}";
}
