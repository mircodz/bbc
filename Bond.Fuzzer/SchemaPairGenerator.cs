using Bond.Parser.Compatibility;
using Bond.Parser.Parser;

namespace Bond.Fuzzer;

public static class SchemaPairGenerator
{
    private const int MaxAttempts = 256;

    public static async Task<GeneratedSchemaPair> GenerateAsync(FuzzerOptions options)
    {
        options.Validate();

        var rng = new Random(options.Seed);

        for (var attempt = 0; attempt < MaxAttempts; attempt++)
        {
            var before = SchemaGenerator.Generate(rng, options);
            if (!SchemaMutator.TryMutate(before, rng, options, out var after, out var mutation))
            {
                continue;
            }

            var beforeText = BondSchemaWriter.Write(before);
            var afterText = BondSchemaWriter.Write(after);

            var beforeParse = await ParserFacade.ParseStringAsync(beforeText);
            var afterParse = await ParserFacade.ParseStringAsync(afterText);

            if (!beforeParse.Success || !afterParse.Success)
            {
                throw FuzzerProducedInvalidSchema(beforeText, afterText, beforeParse.Errors, afterParse.Errors);
            }

            var checker = new CompatibilityChecker();
            var changes = checker.CheckCompatibility(beforeParse.Ast!, afterParse.Ast!);

            return new GeneratedSchemaPair(
                options.Seed,
                mutation,
                beforeText,
                afterText,
                Summarize(changes),
                changes);
        }

        throw new InvalidOperationException(
            $"Unable to generate a schema pair for mutation '{options.Mutation}' with seed {options.Seed} after {MaxAttempts} attempts.");
    }

    // The categories are ordered: any breaking-wire change dominates; otherwise any
    // breaking-text dominates; otherwise compatible.
    private static ChangeCategory Summarize(IReadOnlyList<SchemaChange> changes)
    {
        if (changes.Any(change => change.Category == ChangeCategory.BreakingWire))
            return ChangeCategory.BreakingWire;
        if (changes.Any(change => change.Category == ChangeCategory.BreakingText))
            return ChangeCategory.BreakingText;
        return ChangeCategory.Compatible;
    }

    private static InvalidOperationException FuzzerProducedInvalidSchema(
        string beforeText,
        string afterText,
        IReadOnlyList<ParseError> beforeErrors,
        IReadOnlyList<ParseError> afterErrors) =>
        new(
            "Bond.Fuzzer generated invalid schema text.\n"
            + $"Before errors: {FormatErrors(beforeErrors)}\n"
            + $"After errors: {FormatErrors(afterErrors)}\n"
            + "Before schema:\n"
            + beforeText
            + "\nAfter schema:\n"
            + afterText);

    private static string FormatErrors(IReadOnlyList<ParseError> errors) =>
        errors.Count == 0
            ? "<none>"
            : string.Join(" | ", errors.Select(error => error.Message));
}
