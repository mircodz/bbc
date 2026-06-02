namespace Bond.Parser.Syntax;

public abstract record Declaration
{
    public required Namespace[] Namespaces { get; init; }
    public required string Name { get; init; }
    public TypeParam[] TypeParameters { get; init; } = [];
    public SourceLocation Location { get; init; } = SourceLocation.Unknown;

    /// <summary>Comments immediately preceding this declaration.</summary>
    public Trivia[] LeadingTrivia { get; init; } = [];

    /// <summary>Comment on the same line as this declaration's opening token.</summary>
    public Trivia? TrailingTrivia { get; init; }

    public string QualifiedName =>
        Namespaces.Length > 0
            ? $"{string.Join(".", Namespaces[0].Name)}.{Name}"
            : Name;

    public abstract string Kind { get; }
}
