namespace Bond.Parser.Syntax;

/// <summary>1-based source position. Default value means unknown.</summary>
public readonly record struct SourceLocation(int Line, int Column)
{
    public static readonly SourceLocation Unknown = default;
    public bool IsKnown => Line > 0;
    public override string ToString() => IsKnown ? $"{Line}:{Column}" : "unknown";
}
