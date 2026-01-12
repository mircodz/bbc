namespace Bond.Parser.Compatibility;

/// <summary>
/// Categorizes schema changes by their compatibility impact
/// </summary>
public enum ChangeCategory
{
    /// <summary>
    /// Compatible changes that don't break wire compatibility
    /// </summary>
    Compatible,

    /// <summary>
    /// Breaking changes that are not compatible
    /// </summary>
    Breaking
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
            ChangeCategory.Compatible => "COMPATIBLE",
            ChangeCategory.Breaking => "BREAKING",
            _ => "UNKNOWN"
        };

        var result = $"[{categoryStr}] {Location}: {Description}";
        if (Recommendation != null)
        {
            result += $"\n  → {Recommendation}";
        }
        return result;
    }
}
