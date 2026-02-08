namespace Bond.Parser.Syntax;

/// <summary>
/// Represents an enum constant with optional explicit value
/// Supports int64 values to handle large hex literals
/// </summary>
public record Constant(
    string Name,
    long? Value
)
{
    public override string ToString() =>
        Value.HasValue ? $"{Name} = {Value.Value}" : Name;
}
