namespace Bond.Parser.Syntax;

public record Import(string FilePath)
{
    public override string ToString() => $"import \"{FilePath}\"";
}
