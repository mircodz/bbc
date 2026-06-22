using Bond.Parser.Compatibility;

namespace Bond.Fuzzer;

public enum MutationKind
{
    Random,
    AddOptionalField,
    AddRequiredField,
    RemoveOptionalField,
    RemoveRequiredField,
    AddEnumConstant,
    RemoveEnumConstant,
    ChangeEnumValue,
    ChangeAliasType,
    RenameField,
    ChangeFieldOrdinal,
    WidenNumericField,
    NarrowNumericField,
    ChangeIncompatibleFieldType,
    OptionalToRequired,
    RequiredToOptional,
    AddCollidingEnumConstant,
    MakeEnumValueImplicit,
    WrapBonded,
    UnwrapBonded,
    VectorToList,
    ListToVector,
    Int32ToEnum,
    EnumToInt32
}

public sealed record FuzzerOptions(
    int Seed,
    MutationKind Mutation = MutationKind.Random,
    int EnumCount = 2,
    int AliasCount = 2,
    int StructCount = 3,
    int MaxFieldsPerStruct = 5,
    int MaxTypeDepth = 2,
    string NamespaceName = "fuzz.generated",
    string? OutputDirectory = null)
{
    public void Validate()
    {
        if (StructCount < 1)
            throw new ArgumentOutOfRangeException(nameof(StructCount), "StructCount must be at least 1.");
        if (MaxFieldsPerStruct < 1)
            throw new ArgumentOutOfRangeException(nameof(MaxFieldsPerStruct), "MaxFieldsPerStruct must be at least 1.");
        if (MaxTypeDepth < 0)
            throw new ArgumentOutOfRangeException(nameof(MaxTypeDepth), "MaxTypeDepth must be non-negative.");
        if (string.IsNullOrWhiteSpace(NamespaceName))
            throw new ArgumentException("NamespaceName must not be empty.", nameof(NamespaceName));
    }
}

public sealed record AppliedMutation(
    MutationKind Kind,
    ChangeCategory ExpectedCategory,
    string Description);

public sealed record GeneratedSchemaPair(
    int Seed,
    AppliedMutation Mutation,
    string BeforeSchema,
    string AfterSchema,
    ChangeCategory ObservedCategory,
    IReadOnlyList<SchemaChange> CheckerChanges)
{
    public bool MatchesChecker => Mutation.ExpectedCategory == ObservedCategory;
}
