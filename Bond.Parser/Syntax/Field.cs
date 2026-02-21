namespace Bond.Parser.Syntax;

/// <summary>
/// Field modifier for struct fields
/// </summary>
public enum FieldModifier
{
    Optional,
    Required,
    RequiredOptional
}

/// <summary>
/// Represents a field in a struct
/// </summary>
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
