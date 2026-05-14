using Bond.Parser.Syntax;
using BondSchema = Bond.Parser.Syntax.Bond;

namespace Bond.Fuzzer;

internal static class SchemaGenerator
{
    public static BondSchema Generate(Random rng, FuzzerOptions options)
    {
        var fileNamespace = new Namespace(null, options.NamespaceName.Split('.', StringSplitOptions.RemoveEmptyEntries));
        var enums = GenerateEnums(rng, options, fileNamespace);
        var aliases = GenerateAliases(rng, options, fileNamespace, enums);
        var structs = GenerateStructs(rng, options, fileNamespace, enums, aliases);

        return new BondSchema(
            Imports: [],
            Namespaces: [fileNamespace],
            Declarations: enums.Cast<Declaration>()
                .Concat(aliases)
                .Concat(structs)
                .ToArray());
    }

    public static Field CreateField(ushort ordinal, FieldModifier modifier, BondType type, string name) =>
        new(
            Attributes: [],
            Ordinal: ordinal,
            Modifier: modifier,
            Type: type,
            Name: name,
            DefaultValue: CreateDefaultValue(type, modifier))
        {
            Location = SourceLocation.Unknown
        };

    public static Default? CreateDefaultValue(BondType type, FieldModifier modifier)
    {
        if (modifier == FieldModifier.Required)
        {
            return null;
        }

        return TryCreateEnumDefault(type, out var defaultValue) ? defaultValue : null;
    }

    public static BondType GenerateType(Random rng, int depthRemaining, IReadOnlyList<StructDeclaration> availableStructs, IReadOnlyList<EnumDeclaration> availableEnums, IReadOnlyList<AliasDeclaration> availableAliases) =>
        GenerateType(rng, depthRemaining, new AvailableTypes(availableStructs, availableEnums, availableAliases));

    private static EnumDeclaration[] GenerateEnums(Random rng, FuzzerOptions options, Namespace fileNamespace)
    {
        var enums = new EnumDeclaration[options.EnumCount];

        for (var enumIndex = 0; enumIndex < enums.Length; enumIndex++)
        {
            enums[enumIndex] = new EnumDeclaration
            {
                Attributes = [],
                Constants = GenerateConstants(rng),
                Name = $"E{enumIndex}",
                Namespaces = [fileNamespace],
                TypeParameters = [],
                Location = SourceLocation.Unknown
            };
        }

        return enums;
    }

    private static Constant[] GenerateConstants(Random rng)
    {
        var count = rng.Next(2, 5);
        var constants = new Constant[count];
        long nextValue = 0;

        for (var i = 0; i < count; i++)
        {
            constants[i] = new Constant($"C{i}", nextValue);
            nextValue += rng.Next(1, 4);
        }

        return constants;
    }

    private static AliasDeclaration[] GenerateAliases(
        Random rng,
        FuzzerOptions options,
        Namespace fileNamespace,
        IReadOnlyList<EnumDeclaration> enums)
    {
        var aliases = new AliasDeclaration[options.AliasCount];

        for (var aliasIndex = 0; aliasIndex < aliases.Length; aliasIndex++)
        {
            var availableAliases = aliases
                .Take(aliasIndex)
                .Where(alias => alias is not null)
                .ToArray()!;

            aliases[aliasIndex] = new AliasDeclaration
            {
                AliasedType = GenerateAliasType(rng, enums, availableAliases),
                Name = $"A{aliasIndex}",
                Namespaces = [fileNamespace],
                TypeParameters = [],
                Location = SourceLocation.Unknown
            };
        }

        return aliases;
    }

    private static StructDeclaration[] GenerateStructs(
        Random rng,
        FuzzerOptions options,
        Namespace fileNamespace,
        IReadOnlyList<EnumDeclaration> enums,
        IReadOnlyList<AliasDeclaration> aliases)
    {
        var structs = new StructDeclaration[options.StructCount];

        for (var structIndex = 0; structIndex < structs.Length; structIndex++)
        {
            var availableStructs = structs
                .Take(structIndex)
                .Where(structDeclaration => structDeclaration is not null)
                .ToArray()!;

            var typePool = new AvailableTypes(availableStructs, enums, aliases);
            var fields = GenerateFields(rng, options, typePool);

            structs[structIndex] = new StructDeclaration
            {
                Attributes = [],
                BaseType = null,
                Fields = fields,
                Name = $"S{structIndex}",
                Namespaces = [fileNamespace],
                TypeParameters = [],
                Location = SourceLocation.Unknown
            };
        }

        return structs;
    }

    private static Field[] GenerateFields(Random rng, FuzzerOptions options, AvailableTypes availableTypes)
    {
        var fieldCount = rng.Next(1, options.MaxFieldsPerStruct + 1);
        var fields = new Field[fieldCount];
        ushort nextOrdinal = 0;

        for (var fieldIndex = 0; fieldIndex < fieldCount; fieldIndex++)
        {
            nextOrdinal += (ushort)rng.Next(fieldIndex == 0 ? 1 : 2, 5);
            var type = GenerateType(rng, options.MaxTypeDepth, availableTypes);
            fields[fieldIndex] = CreateField(nextOrdinal, GenerateModifier(rng), type, $"f{fieldIndex}");
        }

        return fields;
    }

    private static BondType GenerateAliasType(Random rng, IReadOnlyList<EnumDeclaration> enums, IReadOnlyList<AliasDeclaration> previousAliases)
    {
        var roll = rng.Next(100);

        if (roll < 45)
        {
            return GenerateScalarType(rng);
        }

        if (roll < 65)
        {
            return new BondType.List(GenerateScalarType(rng));
        }

        if (roll < 85)
        {
            return new BondType.Vector(GenerateScalarType(rng));
        }

        if (enums.Count > 0 && roll < 95)
        {
            return CreateReference(Pick(enums, rng));
        }

        if (previousAliases.Count > 0)
        {
            return CreateReference(Pick(previousAliases, rng));
        }

        return GenerateScalarType(rng);
    }

    private static BondType GenerateType(Random rng, int depthRemaining, AvailableTypes availableTypes)
    {
        if (depthRemaining <= 0)
        {
            return GenerateLeafType(rng, availableTypes);
        }

        var roll = rng.Next(100);

        if (roll < 45)
        {
            return GenerateScalarType(rng);
        }

        if (roll < 62)
        {
            return new BondType.List(GenerateType(rng, depthRemaining - 1, availableTypes));
        }

        if (roll < 79)
        {
            return new BondType.Vector(GenerateType(rng, depthRemaining - 1, availableTypes));
        }

        if (availableTypes.Enums.Count > 0 && roll < 87)
        {
            return CreateReference(Pick(availableTypes.Enums, rng));
        }

        if (availableTypes.Aliases.Count > 0 && roll < 94)
        {
            return CreateReference(Pick(availableTypes.Aliases, rng));
        }

        if (availableTypes.Structs.Count > 0 && roll < 98)
        {
            return CreateReference(Pick(availableTypes.Structs, rng));
        }

        if (availableTypes.Structs.Count > 0)
        {
            return new BondType.Bonded(CreateReference(Pick(availableTypes.Structs, rng)));
        }

        return GenerateScalarType(rng);
    }

    private static BondType GenerateLeafType(Random rng, AvailableTypes availableTypes)
    {
        var roll = rng.Next(100);

        if (availableTypes.Enums.Count > 0 && roll < 15)
        {
            return CreateReference(Pick(availableTypes.Enums, rng));
        }

        if (availableTypes.Aliases.Count > 0 && roll < 30)
        {
            return CreateReference(Pick(availableTypes.Aliases, rng));
        }

        if (availableTypes.Structs.Count > 0 && roll < 45)
        {
            return CreateReference(Pick(availableTypes.Structs, rng));
        }

        return GenerateScalarType(rng);
    }

    private static FieldModifier GenerateModifier(Random rng) =>
        rng.Next(100) < 65 ? FieldModifier.Optional : FieldModifier.Required;

    private static BondType GenerateScalarType(Random rng) => GenerateScalarKind(rng).ToBondType();

    private static ScalarKind GenerateScalarKind(Random rng)
    {
        var roll = rng.Next(100);

        return roll switch
        {
            < 10 => ScalarKind.Bool,
            < 18 => ScalarKind.Int8,
            < 28 => ScalarKind.Int16,
            < 42 => ScalarKind.Int32,
            < 50 => ScalarKind.Int64,
            < 58 => ScalarKind.UInt8,
            < 68 => ScalarKind.UInt16,
            < 78 => ScalarKind.UInt32,
            < 86 => ScalarKind.UInt64,
            < 92 => ScalarKind.Float,
            < 96 => ScalarKind.Double,
            _ => ScalarKind.String
        };
    }

    private static bool TryCreateEnumDefault(BondType type, out Default defaultValue)
    {
        switch (type)
        {
            case BondType.TypeReference { Declaration: EnumDeclaration enumDeclaration }:
                defaultValue = new Default.Enum(enumDeclaration.Constants[0].Name);
                return true;
            case BondType.TypeReference { Declaration: AliasDeclaration aliasDeclaration }:
                return TryCreateEnumDefault(aliasDeclaration.AliasedType, out defaultValue);
            default:
                defaultValue = null!;
                return false;
        }
    }

    private static BondType.TypeReference CreateReference(Declaration declaration) => new(declaration, []);

    private static T Pick<T>(IReadOnlyList<T> items, Random rng) => items[rng.Next(items.Count)];

    private readonly record struct AvailableTypes(
        IReadOnlyList<StructDeclaration> Structs,
        IReadOnlyList<EnumDeclaration> Enums,
        IReadOnlyList<AliasDeclaration> Aliases);
}
