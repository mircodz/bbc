using System.Linq;

namespace Bond.Parser.Syntax;

/// <summary>
/// Programming language for namespace qualifiers
/// </summary>
public enum Language
{
    Cpp,
    Cs,
    Java
}

/// <summary>
/// Represents a namespace declaration with optional language qualifier
/// </summary>
public record Namespace(
    Language? LanguageQualifier,
    string[] Name
)
{
    public override string ToString() =>
        LanguageQualifier.HasValue
            ? $"namespace {LanguageQualifier.Value.ToString().ToLower()} {string.Join(".", Name)}"
            : $"namespace {string.Join(".", Name)}";

    /// <summary>
    /// True if two namespaces refer to the same logical namespace. Names must match
    /// exactly; language qualifiers must match when both are set, otherwise the
    /// unqualified side is treated as language-agnostic.
    /// </summary>
    public bool Matches(Namespace other)
    {
        if (!Name.SequenceEqual(other.Name))
        {
            return false;
        }

        if (LanguageQualifier.HasValue && other.LanguageQualifier.HasValue)
        {
            return LanguageQualifier == other.LanguageQualifier;
        }

        return true;
    }
}
