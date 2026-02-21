namespace Bond.Parser.Syntax;

/// <summary>
/// Immutable source position within a Bond IDL file.
/// Line and Column are 1-based. The default (zero-valued) instance means unknown.
/// </summary>
public readonly record struct SourceLocation(int Line, int Column)
{
    public static readonly SourceLocation Unknown = default;
    public bool IsKnown => Line > 0;
    public override string ToString() => IsKnown ? $"{Line}:{Column}" : "unknown";
}
