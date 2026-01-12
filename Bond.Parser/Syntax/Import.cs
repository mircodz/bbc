namespace Bond.Parser.Syntax;

/// <summary>
/// Represents an import statement
/// </summary>
public record Import(string FilePath)
{
    public override string ToString() => $"import \"{FilePath}\"";
}
