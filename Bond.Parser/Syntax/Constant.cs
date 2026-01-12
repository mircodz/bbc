namespace Bond.Parser.Syntax;

/// <summary>
/// Represents an enum constant with optional explicit value
/// </summary>
public record Constant(
    string Name,
    int? Value
)
{
    public override string ToString() =>
        Value.HasValue ? $"{Name} = {Value.Value}" : Name;
}
