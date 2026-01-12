namespace Bond.Parser.Syntax;

/// <summary>
/// Root AST node representing a parsed Bond file
/// </summary>
public record Bond(
    Import[] Imports,
    Namespace[] Namespaces,
    Declaration[] Declarations
)
{
    public override string ToString() =>
        $"Bond file with {Imports.Length} imports, {Namespaces.Length} namespaces, {Declarations.Length} declarations";
}
