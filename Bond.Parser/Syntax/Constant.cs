namespace Bond.Parser.Syntax;

/// <summary>Enum constant. int64 to fit large hex literals.</summary>
public record Constant(
    string Name,
    long? Value
)
{
    public SourceLocation Location { get; init; } = SourceLocation.Unknown;

    public override string ToString() =>
        Value.HasValue ? $"{Name} = {Value.Value}" : Name;
}
