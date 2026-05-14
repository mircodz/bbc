namespace Bond.Parser.Syntax;

public sealed record EnumDeclaration : Declaration
{
    public required Attribute[] Attributes { get; init; }
    public required Constant[] Constants { get; init; }

    public override string Kind => "enum";

    public override string ToString() => $"enum {Name}";
}
