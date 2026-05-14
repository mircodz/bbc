using System.Linq;

namespace Bond.Parser.Syntax;

public enum Language
{
    Cpp,
    Cs,
    Java
}

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
    /// Names must match exactly; language qualifiers must match when both are set,
    /// otherwise the unqualified side is treated as language-agnostic.
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
