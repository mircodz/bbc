namespace Bond.Parser.Syntax;

public enum TypeConstraint
{
    None,
    Value  // primitives only
}

public record TypeParam(
    string Name,
    TypeConstraint Constraint = TypeConstraint.None
)
{
    public override string ToString() =>
        Constraint == TypeConstraint.Value ? $"{Name} : value" : Name;
}
