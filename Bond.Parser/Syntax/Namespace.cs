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
}
