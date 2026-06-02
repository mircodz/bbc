namespace Bond.Parser.Syntax;

/// <summary>Enum constant. int64 to fit large hex literals.</summary>
public record Constant(
    string Name,
    long? Value
)
{
    public SourceLocation Location { get; init; } = SourceLocation.Unknown;

    /// <summary>Comments immediately preceding this enum constant.</summary>
    public Trivia[] LeadingTrivia { get; init; } = [];

    /// <summary>Comment on the same line as this enum constant.</summary>
    public Trivia? TrailingTrivia { get; init; }

    public override string ToString() =>
        Value.HasValue ? $"{Name} = {Value.Value}" : Name;
}
