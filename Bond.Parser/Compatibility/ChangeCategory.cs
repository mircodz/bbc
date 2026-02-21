namespace Bond.Parser.Compatibility;

/// <summary>
/// Categorizes schema changes by their compatibility impact
/// </summary>
public enum ChangeCategory
{
    /// <summary>
    /// Compatible with all protocols — safe to deploy without coordination.
    /// </summary>
    Compatible,

    /// <summary>
    /// Breaks binary wire protocols (Compact Binary, Fast Binary, …).
    /// Fields are identified by ordinal on the wire, so changes to ordinals,
    /// required-ness, types, defaults, or inheritance all fall here.
    /// </summary>
    BreakingWire,

    /// <summary>
    /// Breaks text-based protocols (SimpleJSON, SimpleXML, …) but is safe
    /// for binary protocols. Field name changes are the primary example:
    /// binary protocols use ordinals so they are unaffected, but text
    /// protocols key on the field name.
    /// </summary>
    BreakingText,
}

/// <summary>
/// Represents a single schema change detected during compatibility checking
/// </summary>
public record SchemaChange(
    ChangeCategory Category,
    string Description,
    string Location,
    string? Recommendation = null
)
{
    public override string ToString()
    {
        var categoryStr = Category switch
        {
            ChangeCategory.Compatible    => "COMPATIBLE",
            ChangeCategory.BreakingWire  => "BREAKING-WIRE",
            ChangeCategory.BreakingText  => "BREAKING-TEXT",
            _                            => "UNKNOWN"
        };

        var result = $"[{categoryStr}] {Location}: {Description}";
        if (Recommendation != null)
        {
            result += $"\n  → {Recommendation}";
        }
        return result;
    }
}
