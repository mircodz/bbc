namespace Bond.Parser.Syntax;

/// <summary>
/// Constraint on a type parameter
/// </summary>
public enum TypeConstraint
{
    None,
    Value  // Restricts to value types (primitives)
}

/// <summary>
/// Represents a generic type parameter
/// </summary>
public record TypeParam(
    string Name,
    TypeConstraint Constraint = TypeConstraint.None
)
{
    public override string ToString() =>
        Constraint == TypeConstraint.Value ? $"{Name} : value" : Name;
}
