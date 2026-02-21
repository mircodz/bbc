namespace Bond.Parser.Syntax;

/// <summary>
/// Base class for all top-level declarations (struct, enum, service, alias, forward)
/// </summary>
public abstract record Declaration
{
    public required Namespace[] Namespaces { get; init; }
    public required string Name { get; init; }
    public required TypeParam[] TypeParameters { get; init; }
    public SourceLocation Location { get; init; } = SourceLocation.Unknown;

    /// <summary>
    /// Gets the qualified name for display purposes
    /// </summary>
    public string QualifiedName =>
        Namespaces.Length > 0
            ? $"{string.Join(".", Namespaces[0].Name)}.{Name}"
            : Name;

    public abstract string Kind { get; }
}
