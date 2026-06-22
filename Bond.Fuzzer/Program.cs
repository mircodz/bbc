using System.Text.Json;
using Bond.Parser.Compatibility;

namespace Bond.Fuzzer;

internal static class Program
{
    public static async Task<int> Main(string[] args)
    {
        try
        {
            if (args.Contains("--help", StringComparer.Ordinal) || args.Contains("-h", StringComparer.Ordinal))
            {
                WriteUsage();
                return 0;
            }

            var options = ParseOptions(args);
            return options.Count == 1
                ? await RunSingleAsync(options.Fuzzer)
                : await RunBatchAsync(options);
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine(ex.Message);
            return 1;
        }
    }

    private sealed record CliOptions(FuzzerOptions Fuzzer, int Count);

    private static CliOptions ParseOptions(string[] args)
    {
        int? seed = null;
        var mutation = MutationKind.Random;
        var enumCount = 2;
        var aliasCount = 2;
        var structCount = 3;
        var maxFieldsPerStruct = 5;
        var maxTypeDepth = 2;
        var namespaceName = "fuzz.generated";
        string? outputDirectory = null;
        var count = 1;

        for (var i = 0; i < args.Length; i++)
        {
            switch (args[i])
            {
                case "-n":
                case "--count":
                    count = ParseInt(args, ref i, "--count");
                    break;
                case "--seed":
                    seed = ParseInt(args, ref i, "--seed");
                    break;
                case "--mutation":
                    mutation = ParseMutation(args, ref i);
                    break;
                case "--enums":
                    enumCount = ParseInt(args, ref i, "--enums");
                    break;
                case "--aliases":
                    aliasCount = ParseInt(args, ref i, "--aliases");
                    break;
                case "--structs":
                    structCount = ParseInt(args, ref i, "--structs");
                    break;
                case "--max-fields":
                    maxFieldsPerStruct = ParseInt(args, ref i, "--max-fields");
                    break;
                case "--max-depth":
                    maxTypeDepth = ParseInt(args, ref i, "--max-depth");
                    break;
                case "--namespace":
                    namespaceName = ParseString(args, ref i, "--namespace");
                    break;
                case "--output-dir":
                    outputDirectory = ParseString(args, ref i, "--output-dir");
                    break;
                default:
                    throw new ArgumentException($"Unknown argument: {args[i]}");
            }
        }

        if (count < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(count), "Count must be at least 1.");
        }

        return new CliOptions(
            new FuzzerOptions(
                seed ?? Random.Shared.Next(1, int.MaxValue),
                mutation,
                enumCount,
                aliasCount,
                structCount,
                maxFieldsPerStruct,
                maxTypeDepth,
                namespaceName,
                outputDirectory),
            count);
    }

    private static async Task<int> RunSingleAsync(FuzzerOptions options)
    {
        var pair = await SchemaPairGenerator.GenerateAsync(options);

        if (options.OutputDirectory is not null)
        {
            await WriteFilesAsync(options.OutputDirectory, pair);
        }
        else
        {
            WriteToStdout(pair);
        }

        return pair.MatchesChecker ? 0 : 2;
    }

    private static async Task<int> RunBatchAsync(CliOptions options)
    {
        var passed = 0;

        for (var i = 0; i < options.Count; i++)
        {
            var seed = checked(options.Fuzzer.Seed + i);
            var runOptions = options.Fuzzer with
            {
                Seed = seed,
                OutputDirectory = null
            };

            GeneratedSchemaPair pair;
            try
            {
                pair = await SchemaPairGenerator.GenerateAsync(runOptions);
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"FAILED: seed={seed} mutation={ToCliName(runOptions.Mutation)}");
                Console.Error.WriteLine(ex.Message);
                return 1;
            }

            Console.WriteLine(
                $"[{i + 1}/{options.Count}] seed={pair.Seed} mutation={ToCliName(pair.Mutation.Kind)} expected={ToCliName(pair.Mutation.ExpectedCategory)} observed={ToCliName(pair.ObservedCategory)}");

            if (!pair.MatchesChecker)
            {
                await HandleBatchFailureAsync(options, pair);
                return 2;
            }

            passed++;
        }

        Console.WriteLine();
        Console.WriteLine($"PASS: {passed}/{options.Count} runs succeeded");
        return 0;
    }

    private static async Task HandleBatchFailureAsync(CliOptions options, GeneratedSchemaPair pair)
    {
        Console.Error.WriteLine($"MISMATCH: seed={pair.Seed} mutation={ToCliName(pair.Mutation.Kind)}");
        Console.Error.WriteLine($"expected={ToCliName(pair.Mutation.ExpectedCategory)} observed={ToCliName(pair.ObservedCategory)}");

        if (options.Fuzzer.OutputDirectory is not null)
        {
            var runDirectory = Path.Combine(options.Fuzzer.OutputDirectory, $"seed-{pair.Seed}");
            await WriteFilesAsync(runDirectory, pair);
            Console.Error.WriteLine($"artifacts: {runDirectory}");
            return;
        }

        WriteToStdout(pair);
    }

    private static int ParseInt(string[] args, ref int index, string option)
    {
        var value = ParseString(args, ref index, option);
        if (!int.TryParse(value, out var parsed))
        {
            throw new ArgumentException($"Invalid integer for {option}: {value}");
        }

        return parsed;
    }

    private static readonly Dictionary<MutationKind, string> MutationCliNames = new()
    {
        [MutationKind.Random]                      = "random",
        [MutationKind.AddOptionalField]            = "add-optional-field",
        [MutationKind.AddRequiredField]            = "add-required-field",
        [MutationKind.RemoveOptionalField]         = "remove-optional-field",
        [MutationKind.RemoveRequiredField]         = "remove-required-field",
        [MutationKind.AddEnumConstant]             = "add-enum-constant",
        [MutationKind.RemoveEnumConstant]          = "remove-enum-constant",
        [MutationKind.ChangeEnumValue]             = "change-enum-value",
        [MutationKind.ChangeAliasType]             = "change-alias-type",
        [MutationKind.RenameField]                 = "rename-field",
        [MutationKind.ChangeFieldOrdinal]          = "change-field-ordinal",
        [MutationKind.WidenNumericField]           = "widen-numeric-field",
        [MutationKind.NarrowNumericField]          = "narrow-numeric-field",
        [MutationKind.ChangeIncompatibleFieldType] = "change-incompatible-field-type",
        [MutationKind.OptionalToRequired]          = "optional-to-required",
        [MutationKind.RequiredToOptional]          = "required-to-optional",
        [MutationKind.AddCollidingEnumConstant]    = "add-colliding-enum-constant",
        [MutationKind.MakeEnumValueImplicit]       = "make-enum-value-implicit",
        [MutationKind.WrapBonded]                  = "wrap-bonded",
        [MutationKind.UnwrapBonded]                = "unwrap-bonded",
        [MutationKind.VectorToList]                = "vector-to-list",
        [MutationKind.ListToVector]                = "list-to-vector",
        [MutationKind.Int32ToEnum]                 = "int32-to-enum",
        [MutationKind.EnumToInt32]                 = "enum-to-int32",
    };

    private static readonly Dictionary<string, MutationKind> MutationByCliName =
        MutationCliNames.ToDictionary(kv => kv.Value, kv => kv.Key, StringComparer.Ordinal);

    private static MutationKind ParseMutation(string[] args, ref int index)
    {
        var value = ParseString(args, ref index, "--mutation").ToLowerInvariant();
        return MutationByCliName.TryGetValue(value, out var kind)
            ? kind
            : throw new ArgumentException($"Unknown mutation: {value}");
    }

    private static string ParseString(string[] args, ref int index, string option)
    {
        if (index + 1 >= args.Length)
        {
            throw new ArgumentException($"Missing value for {option}");
        }

        index++;
        return args[index];
    }

    private static async Task WriteFilesAsync(string outputDirectory, GeneratedSchemaPair pair)
    {
        Directory.CreateDirectory(outputDirectory);

        await File.WriteAllTextAsync(Path.Combine(outputDirectory, "before.bond"), pair.BeforeSchema);
        await File.WriteAllTextAsync(Path.Combine(outputDirectory, "after.bond"), pair.AfterSchema);

        var metadata = new
        {
            seed = pair.Seed,
            mutation = ToCliName(pair.Mutation.Kind),
            description = pair.Mutation.Description,
            expectedCategory = ToCliName(pair.Mutation.ExpectedCategory),
            observedCategory = ToCliName(pair.ObservedCategory),
            matchesChecker = pair.MatchesChecker,
            checkerChanges = pair.CheckerChanges.Select(change => new
            {
                category = ToCliName(change.Category),
                change.Location,
                change.Description,
                change.Recommendation
            })
        };

        var json = JsonSerializer.Serialize(metadata, new JsonSerializerOptions { WriteIndented = true });
        await File.WriteAllTextAsync(Path.Combine(outputDirectory, "metadata.json"), json);

        Console.WriteLine($"Wrote schema pair to {outputDirectory}");
        Console.WriteLine($"Seed: {pair.Seed}");
        Console.WriteLine($"Mutation: {ToCliName(pair.Mutation.Kind)}");
        Console.WriteLine($"Expected: {ToCliName(pair.Mutation.ExpectedCategory)}");
        Console.WriteLine($"Observed: {ToCliName(pair.ObservedCategory)}");
    }

    private static void WriteToStdout(GeneratedSchemaPair pair)
    {
        Console.WriteLine($"seed: {pair.Seed}");
        Console.WriteLine($"mutation: {ToCliName(pair.Mutation.Kind)}");
        Console.WriteLine($"expected: {ToCliName(pair.Mutation.ExpectedCategory)}");
        Console.WriteLine($"observed: {ToCliName(pair.ObservedCategory)}");
        Console.WriteLine($"matches-checker: {pair.MatchesChecker.ToString().ToLowerInvariant()}");
        Console.WriteLine($"description: {pair.Mutation.Description}");
        Console.WriteLine();
        Console.WriteLine("=== before.bond ===");
        Console.WriteLine(pair.BeforeSchema);
        Console.WriteLine("=== after.bond ===");
        Console.WriteLine(pair.AfterSchema);

        if (pair.CheckerChanges.Count > 0)
        {
            Console.WriteLine("=== checker changes ===");
            foreach (var change in pair.CheckerChanges)
            {
                Console.WriteLine($"{ToCliName(change.Category)}: {change.Location}: {change.Description}");
            }
        }
    }

    private static string ToCliName(MutationKind kind) => MutationCliNames.TryGetValue(kind, out var name)
        ? name
        : throw new ArgumentOutOfRangeException(nameof(kind), kind, "Unsupported mutation kind.");

    private static string ToCliName(ChangeCategory category) =>
        category switch
        {
            ChangeCategory.Compatible => "compatible",
            ChangeCategory.BreakingWire => "breaking_wire",
            ChangeCategory.BreakingText => "breaking_text",
            _ => throw new ArgumentOutOfRangeException(nameof(category), category, "Unsupported change category.")
        };

    private static void WriteUsage()
    {
        Console.WriteLine("Usage:");
        Console.WriteLine("  dotnet run --project Bond.Fuzzer -- [options]");
        Console.WriteLine();
        Console.WriteLine("Options:");
        Console.WriteLine("  -n, --count <int>                 Number of runs to execute in one process. Default: 1");
        Console.WriteLine("  --seed <int>                      Deterministic random seed. Defaults to a random seed.");
        Console.WriteLine("  --mutation <name>                 random | add-optional-field | add-required-field");
        Console.WriteLine("                                    remove-optional-field | remove-required-field");
        Console.WriteLine("                                    add-enum-constant | remove-enum-constant | change-enum-value");
        Console.WriteLine("                                    change-alias-type | rename-field");
        Console.WriteLine("                                    change-field-ordinal | widen-numeric-field | narrow-numeric-field");
        Console.WriteLine("                                    change-incompatible-field-type");
        Console.WriteLine("                                    optional-to-required | required-to-optional");
        Console.WriteLine("  --enums <int>                     Number of enums to generate. Default: 2");
        Console.WriteLine("  --aliases <int>                   Number of aliases to generate. Default: 2");
        Console.WriteLine("  --structs <int>                   Number of structs to generate. Default: 3");
        Console.WriteLine("  --max-fields <int>                Maximum fields per struct. Default: 5");
        Console.WriteLine("  --max-depth <int>                 Maximum generated type nesting depth. Default: 2");
        Console.WriteLine("  --namespace <name>                Namespace to emit. Default: fuzz.generated");
        Console.WriteLine("  --output-dir <path>               For one run: write before/after schemas and metadata.json.");
        Console.WriteLine("                                    For batch runs: write only the first failing case to seed-<n>/.");
        Console.WriteLine("  --help                            Show this message.");
    }
}
