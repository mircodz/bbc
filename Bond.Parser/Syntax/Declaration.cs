namespace Bond.Parser.Syntax;

public abstract record Declaration
{
    public required Namespace[] Namespaces { get; init; }
    public required string Name { get; init; }
    public TypeParam[] TypeParameters { get; init; } = [];
    public SourceLocation Location { get; init; } = SourceLocation.Unknown;

    public string QualifiedName =>
        Namespaces.Length > 0
            ? $"{string.Join(".", Namespaces[0].Name)}.{Name}"
            : Name;

    public abstract string Kind { get; }
}
