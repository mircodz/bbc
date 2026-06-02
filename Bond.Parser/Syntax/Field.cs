namespace Bond.Parser.Syntax;

public enum FieldModifier
{
    Optional,
    Required,
    RequiredOptional
}

public record Field(
    Attribute[] Attributes,
    ushort Ordinal,
    FieldModifier Modifier,
    BondType Type,
    string Name,
    Default? DefaultValue
)
{
    public SourceLocation Location { get; init; } = SourceLocation.Unknown;

    /// <summary>Comments immediately preceding this field.</summary>
    public Trivia[] LeadingTrivia { get; init; } = [];

    /// <summary>Comment on the same line as this field.</summary>
    public Trivia? TrailingTrivia { get; init; }

    public override string ToString()
    {
        var modifier = Modifier switch
        {
            FieldModifier.Required => "required ",
            FieldModifier.RequiredOptional => "required_optional ",
            _ => ""
        };
        var defaultVal = DefaultValue != null ? $" = {DefaultValue}" : "";
        return $"{Ordinal}: {modifier}{Type} {Name}{defaultVal}";
    }
}
