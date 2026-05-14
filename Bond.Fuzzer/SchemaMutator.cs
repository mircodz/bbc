using Bond.Parser.Compatibility;
using Bond.Parser.Syntax;
using BondSchema = Bond.Parser.Syntax.Bond;

namespace Bond.Fuzzer;

internal static class SchemaMutator
{
    private static readonly MutationKind[] ConcreteMutations =
    [
        MutationKind.AddOptionalField,
        MutationKind.AddRequiredField,
        MutationKind.RemoveOptionalField,
        MutationKind.RemoveRequiredField,
        MutationKind.AddEnumConstant,
        MutationKind.AddCollidingEnumConstant,
        MutationKind.RemoveEnumConstant,
        MutationKind.ChangeEnumValue,
        MutationKind.MakeEnumValueImplicit,
        MutationKind.ChangeAliasType,
        MutationKind.RenameField,
        MutationKind.ChangeFieldOrdinal,
        MutationKind.WidenNumericField,
        MutationKind.NarrowNumericField,
        MutationKind.ChangeIncompatibleFieldType,
        MutationKind.OptionalToRequired,
        MutationKind.RequiredToOptional,
        MutationKind.WrapBonded,
        MutationKind.UnwrapBonded,
        MutationKind.VectorToList,
        MutationKind.ListToVector,
        MutationKind.Int32ToEnum,
        MutationKind.EnumToInt32
    ];

    public static bool TryMutate(
        BondSchema before,
        Random rng,
        FuzzerOptions options,
        out BondSchema after,
        out AppliedMutation mutation)
    {
        if (options.Mutation != MutationKind.Random)
        {
            return TryApplySpecificMutation(before, rng, options, options.Mutation, out after, out mutation);
        }

        var shuffled = ConcreteMutations.ToArray();
        Shuffle(shuffled, rng);

        foreach (var candidate in shuffled)
        {
            if (TryApplySpecificMutation(before, rng, options, candidate, out after, out mutation))
            {
                return true;
            }
        }

        after = before;
        mutation = new AppliedMutation(MutationKind.Random, ChangeCategory.Compatible, "No mutation applied.");
        return false;
    }

    private static bool TryApplySpecificMutation(
        BondSchema before,
        Random rng,
        FuzzerOptions options,
        MutationKind mutationKind,
        out BondSchema after,
        out AppliedMutation mutation)
    {
        return mutationKind switch
        {
            MutationKind.AddOptionalField => TryAddField(before, rng, options, FieldModifier.Optional, ChangeCategory.Compatible, out after, out mutation),
            MutationKind.AddRequiredField => TryAddField(before, rng, options, FieldModifier.Required, ChangeCategory.BreakingWire, out after, out mutation),
            MutationKind.RemoveOptionalField => TryRemoveField(before, rng, FieldModifier.Optional, ChangeCategory.Compatible, out after, out mutation),
            MutationKind.RemoveRequiredField => TryRemoveField(before, rng, FieldModifier.Required, ChangeCategory.BreakingWire, out after, out mutation),
            MutationKind.AddEnumConstant => TryAddEnumConstant(before, rng, out after, out mutation),
            MutationKind.RemoveEnumConstant => TryRemoveEnumConstant(before, rng, out after, out mutation),
            MutationKind.ChangeEnumValue => TryChangeEnumValue(before, rng, out after, out mutation),
            MutationKind.ChangeAliasType => TryChangeAliasType(before, rng, out after, out mutation),
            MutationKind.RenameField => TryRenameField(before, rng, out after, out mutation),
            MutationKind.ChangeFieldOrdinal => TryChangeFieldOrdinal(before, rng, out after, out mutation),
            MutationKind.WidenNumericField => TryChangeNumericField(before, rng, CanWiden, Widen, MutationKind.WidenNumericField, ChangeCategory.Compatible, "Widened", out after, out mutation),
            MutationKind.NarrowNumericField => TryChangeNumericField(before, rng, CanNarrow, Narrow, MutationKind.NarrowNumericField, ChangeCategory.BreakingWire, "Narrowed", out after, out mutation),
            MutationKind.ChangeIncompatibleFieldType => TryChangeIncompatibleFieldType(before, rng, out after, out mutation),
            MutationKind.OptionalToRequired => TryChangeModifier(before, rng, FieldModifier.Optional, FieldModifier.Required, ChangeCategory.BreakingWire, MutationKind.OptionalToRequired, out after, out mutation),
            MutationKind.RequiredToOptional => TryChangeModifier(before, rng, FieldModifier.Required, FieldModifier.Optional, ChangeCategory.BreakingWire, MutationKind.RequiredToOptional, out after, out mutation),
            MutationKind.AddCollidingEnumConstant => TryAddCollidingEnumConstant(before, rng, out after, out mutation),
            MutationKind.MakeEnumValueImplicit => TryMakeEnumValueImplicit(before, rng, out after, out mutation),
            MutationKind.WrapBonded => TryWrapBonded(before, rng, out after, out mutation),
            MutationKind.UnwrapBonded => TryUnwrapBonded(before, rng, out after, out mutation),
            MutationKind.VectorToList => TryConvertVectorList(before, rng, vectorToList: true, out after, out mutation),
            MutationKind.ListToVector => TryConvertVectorList(before, rng, vectorToList: false, out after, out mutation),
            MutationKind.Int32ToEnum => TryInt32ToEnum(before, rng, out after, out mutation),
            MutationKind.EnumToInt32 => TryEnumToInt32(before, rng, out after, out mutation),
            _ => throw new ArgumentOutOfRangeException(nameof(mutationKind), mutationKind, "Unsupported mutation kind.")
        };
    }

    private static bool TryAddField(
        BondSchema schema,
        Random rng,
        FuzzerOptions options,
        FieldModifier modifier,
        ChangeCategory expectedCategory,
        out BondSchema after,
        out AppliedMutation mutation)
    {
        var structs = GetStructs(schema);
        var candidate = Pick(structs, rng);
        var target = candidate.Struct;
        var nextOrdinal = NextOrdinal(target);
        var nextName = NextFieldName(target);
        var availableStructs = structs.Select(s => s.Struct).Take(candidate.StructListIndex).ToArray();
        var availableEnums = GetEnums(schema).Select(candidate => candidate.Enum).ToArray();
        var availableAliases = GetAliases(schema).Select(candidate => candidate.Alias).ToArray();
        var newField = SchemaGenerator.CreateField(
            nextOrdinal,
            modifier,
            SchemaGenerator.GenerateType(rng, options.MaxTypeDepth, availableStructs, availableEnums, availableAliases),
            nextName);

        var updated = target with
        {
            Fields = target.Fields
                .Append(newField)
                .OrderBy(field => field.Ordinal)
                .ToArray()
        };

        after = ReplaceStruct(schema, candidate.DeclarationIndex, updated);
        mutation = new AppliedMutation(
            modifier == FieldModifier.Required ? MutationKind.AddRequiredField : MutationKind.AddOptionalField,
            expectedCategory,
            $"Added {modifier.ToString().ToLowerInvariant()} field '{newField.Name}' ({newField.Type}) to struct '{target.Name}' at ordinal {newField.Ordinal}.");
        return true;
    }

    private static bool TryRemoveField(
        BondSchema schema,
        Random rng,
        FieldModifier modifier,
        ChangeCategory expectedCategory,
        out BondSchema after,
        out AppliedMutation mutation)
    {
        var candidates = FieldCandidates(schema, field => field.Modifier == modifier).ToArray();
        if (candidates.Length == 0)
        {
            after = schema;
            mutation = null!;
            return false;
        }

        var candidate = Pick(candidates, rng);
        var removed = candidate.Struct.Fields[candidate.FieldIndex];
        var updated = candidate.Struct with
        {
            Fields = candidate.Struct.Fields
                .Where((_, index) => index != candidate.FieldIndex)
                .ToArray()
        };

        after = ReplaceStruct(schema, candidate.DeclarationIndex, updated);
        mutation = new AppliedMutation(
            modifier == FieldModifier.Required ? MutationKind.RemoveRequiredField : MutationKind.RemoveOptionalField,
            expectedCategory,
            $"Removed {modifier.ToString().ToLowerInvariant()} field '{removed.Name}' from struct '{candidate.Struct.Name}'.");
        return true;
    }

    private static bool TryAddEnumConstant(BondSchema schema, Random rng, out BondSchema after, out AppliedMutation mutation)
    {
        var candidates = GetEnums(schema);
        if (candidates.Length == 0)
        {
            after = schema;
            mutation = null!;
            return false;
        }

        var candidate = Pick(candidates, rng);
        var target = candidate.Enum;
        var nextConstant = new Constant(NextConstantName(target), NextConstantValue(target));
        var updated = target with { Constants = target.Constants.Append(nextConstant).ToArray() };

        after = ReplaceDeclaration(schema, candidate.DeclarationIndex, updated);
        mutation = new AppliedMutation(
            MutationKind.AddEnumConstant,
            ChangeCategory.Compatible,
            $"Added enum constant '{nextConstant.Name} = {nextConstant.Value}' to enum '{target.Name}'.");
        return true;
    }

    private static bool TryRemoveEnumConstant(BondSchema schema, Random rng, out BondSchema after, out AppliedMutation mutation)
    {
        var candidates = GetEnums(schema).Where(candidate => candidate.Enum.Constants.Length > 1).ToArray();
        if (candidates.Length == 0)
        {
            after = schema;
            mutation = null!;
            return false;
        }

        var candidate = Pick(candidates, rng);
        var constantIndex = rng.Next(candidate.Enum.Constants.Length);
        var removed = candidate.Enum.Constants[constantIndex];
        var updated = candidate.Enum with
        {
            Constants = candidate.Enum.Constants
                .Where((_, index) => index != constantIndex)
                .ToArray()
        };

        after = ReplaceDeclaration(schema, candidate.DeclarationIndex, updated);
        mutation = new AppliedMutation(
            MutationKind.RemoveEnumConstant,
            ChangeCategory.BreakingWire,
            $"Removed enum constant '{removed.Name}' from enum '{candidate.Enum.Name}'.");
        return true;
    }

    private static bool TryChangeEnumValue(BondSchema schema, Random rng, out BondSchema after, out AppliedMutation mutation)
    {
        var candidates = GetEnums(schema);
        if (candidates.Length == 0)
        {
            after = schema;
            mutation = null!;
            return false;
        }

        var candidate = Pick(candidates, rng);
        var constantIndex = rng.Next(candidate.Enum.Constants.Length);
        var original = candidate.Enum.Constants[constantIndex];
        var nextValue = NextConstantValue(candidate.Enum) + rng.Next(1, 4);
        var constants = candidate.Enum.Constants.ToArray();
        constants[constantIndex] = original with { Value = nextValue };
        var updated = candidate.Enum with { Constants = constants };

        after = ReplaceDeclaration(schema, candidate.DeclarationIndex, updated);
        mutation = new AppliedMutation(
            MutationKind.ChangeEnumValue,
            ChangeCategory.BreakingWire,
            $"Changed enum constant '{original.Name}' in enum '{candidate.Enum.Name}' from {original.Value} to {nextValue}.");
        return true;
    }

    private static bool TryChangeAliasType(BondSchema schema, Random rng, out BondSchema after, out AppliedMutation mutation)
    {
        var candidates = GetAliases(schema);
        if (candidates.Length == 0)
        {
            after = schema;
            mutation = null!;
            return false;
        }

        var candidate = Pick(candidates, rng);
        var updatedAlias = candidate.Alias with { AliasedType = CreateIncompatibleType(candidate.Alias.AliasedType) };
        after = ReplaceAliasAndNormalizeFields(schema, candidate, updatedAlias);
        mutation = new AppliedMutation(
            MutationKind.ChangeAliasType,
            ChangeCategory.BreakingWire,
            $"Changed alias '{candidate.Alias.Name}' from {candidate.Alias.AliasedType} to {updatedAlias.AliasedType}.");
        return true;
    }

    private static bool TryRenameField(BondSchema schema, Random rng, out BondSchema after, out AppliedMutation mutation)
    {
        var candidates = FieldCandidates(schema, _ => true).ToArray();
        var candidate = Pick(candidates, rng);
        var original = candidate.Struct.Fields[candidate.FieldIndex];
        var renamed = original with { Name = NextFieldName(candidate.Struct) };

        after = ReplaceField(schema, candidate, renamed);
        mutation = new AppliedMutation(
            MutationKind.RenameField,
            ChangeCategory.BreakingText,
            $"Renamed field '{original.Name}' to '{renamed.Name}' in struct '{candidate.Struct.Name}' without changing ordinal {original.Ordinal}.");
        return true;
    }

    private static bool TryChangeFieldOrdinal(BondSchema schema, Random rng, out BondSchema after, out AppliedMutation mutation)
    {
        var candidates = FieldCandidates(schema, _ => true).ToArray();
        var candidate = Pick(candidates, rng);
        var original = candidate.Struct.Fields[candidate.FieldIndex];
        var updated = original with { Ordinal = NextOrdinal(candidate.Struct) };
        var expectedCategory = original.Modifier == FieldModifier.Required
            ? ChangeCategory.BreakingWire
            : ChangeCategory.Compatible;

        after = ReplaceField(schema, candidate, updated);
        mutation = new AppliedMutation(
            MutationKind.ChangeFieldOrdinal,
            expectedCategory,
            $"Changed ordinal for field '{original.Name}' in struct '{candidate.Struct.Name}' from {original.Ordinal} to {updated.Ordinal}.");
        return true;
    }

    private static bool TryChangeNumericField(
        BondSchema schema,
        Random rng,
        Func<ScalarKind, bool> predicate,
        Func<ScalarKind, ScalarKind> transform,
        MutationKind kind,
        ChangeCategory expectedCategory,
        string verb,
        out BondSchema after,
        out AppliedMutation mutation)
    {
        var candidates = NumericFieldCandidates(schema, predicate).ToArray();
        if (candidates.Length == 0)
        {
            after = schema;
            mutation = null!;
            return false;
        }

        var candidate = Pick(candidates, rng);
        var original = candidate.Struct.Fields[candidate.FieldIndex];
        var nextKind = transform(candidate.ScalarKind);
        var updated = original with { Type = nextKind.ToBondType() };

        after = ReplaceField(schema, candidate, updated);
        mutation = new AppliedMutation(
            kind,
            expectedCategory,
            $"{verb} field '{original.Name}' in struct '{candidate.Struct.Name}' from {candidate.ScalarKind.ToBondTypeName()} to {nextKind.ToBondTypeName()}.");
        return true;
    }

    private static bool TryChangeIncompatibleFieldType(BondSchema schema, Random rng, out BondSchema after, out AppliedMutation mutation)
    {
        var candidates = FieldCandidates(schema, _ => true).ToArray();
        var candidate = Pick(candidates, rng);
        var original = candidate.Struct.Fields[candidate.FieldIndex];
        var replacement = CreateIncompatibleType(original.Type);
        var updated = NormalizeField(original with { Type = replacement });

        after = ReplaceField(schema, candidate, updated);
        mutation = new AppliedMutation(
            MutationKind.ChangeIncompatibleFieldType,
            ChangeCategory.BreakingWire,
            $"Changed field '{original.Name}' in struct '{candidate.Struct.Name}' from {original.Type} to {replacement}.");
        return true;
    }

    private static bool TryChangeModifier(
        BondSchema schema,
        Random rng,
        FieldModifier from,
        FieldModifier to,
        ChangeCategory expectedCategory,
        MutationKind kind,
        out BondSchema after,
        out AppliedMutation mutation)
    {
        var candidates = FieldCandidates(schema, field => field.Modifier == from).ToArray();
        if (candidates.Length == 0)
        {
            after = schema;
            mutation = null!;
            return false;
        }

        var candidate = Pick(candidates, rng);
        var original = candidate.Struct.Fields[candidate.FieldIndex];
        var updated = NormalizeField(original with { Modifier = to });

        after = ReplaceField(schema, candidate, updated);
        mutation = new AppliedMutation(
            kind,
            expectedCategory,
            $"Changed modifier for field '{original.Name}' in struct '{candidate.Struct.Name}' from {original.Modifier} to {to}.");
        return true;
    }

    private static bool TryAddCollidingEnumConstant(BondSchema schema, Random rng, out BondSchema after, out AppliedMutation mutation)
    {
        var candidates = GetEnums(schema);
        if (candidates.Length == 0)
        {
            after = schema;
            mutation = null!;
            return false;
        }

        var candidate = Pick(candidates, rng);
        var target = candidate.Enum;
        var collidingValue = target.Constants[rng.Next(target.Constants.Length)].Value
            ?? target.Constants[rng.Next(target.Constants.Length)].Value
            ?? 0;
        var nextConstant = new Constant(NextConstantName(target), collidingValue);
        var updated = target with { Constants = target.Constants.Append(nextConstant).ToArray() };

        after = ReplaceDeclaration(schema, candidate.DeclarationIndex, updated);
        mutation = new AppliedMutation(
            MutationKind.AddCollidingEnumConstant,
            ChangeCategory.BreakingWire,
            $"Added enum constant '{nextConstant.Name} = {collidingValue}' to enum '{target.Name}', colliding with an existing value.");
        return true;
    }

    private static bool TryMakeEnumValueImplicit(BondSchema schema, Random rng, out BondSchema after, out AppliedMutation mutation)
    {
        // Find a constant whose explicit value matches what its implicit value would be.
        // Stripping the explicit value leaves the effective integer unchanged, so the
        // checker should report no changes.
        var enums = GetEnums(schema).ToArray();
        Shuffle(enums, rng);
        foreach (var candidate in enums)
        {
            var constants = candidate.Enum.Constants;
            for (var i = 1; i < constants.Length; i++)
            {
                if (!constants[i].Value.HasValue) continue;
                var prevValue = constants[i - 1].Value ?? i - 1;
                if (constants[i].Value != prevValue + 1) continue;

                var updatedConstants = constants.ToArray();
                updatedConstants[i] = constants[i] with { Value = null };
                var updated = candidate.Enum with { Constants = updatedConstants };

                after = ReplaceDeclaration(schema, candidate.DeclarationIndex, updated);
                mutation = new AppliedMutation(
                    MutationKind.MakeEnumValueImplicit,
                    ChangeCategory.Compatible,
                    $"Made enum constant '{constants[i].Name}' in enum '{candidate.Enum.Name}' implicit (effective value {constants[i].Value} unchanged).");
                return true;
            }
        }

        after = schema;
        mutation = null!;
        return false;
    }

    private static bool TryWrapBonded(BondSchema schema, Random rng, out BondSchema after, out AppliedMutation mutation)
    {
        var candidates = FieldCandidates(schema, f => f.Type is BondType.TypeReference { Declaration: StructDeclaration }).ToArray();
        if (candidates.Length == 0)
        {
            after = schema;
            mutation = null!;
            return false;
        }

        var candidate = Pick(candidates, rng);
        var original = candidate.Struct.Fields[candidate.FieldIndex];
        var updated = original with { Type = new BondType.Bonded(original.Type) };

        after = ReplaceField(schema, candidate, updated);
        mutation = new AppliedMutation(
            MutationKind.WrapBonded,
            ChangeCategory.Compatible,
            $"Wrapped field '{original.Name}' in struct '{candidate.Struct.Name}' from {original.Type} to {updated.Type}.");
        return true;
    }

    private static bool TryUnwrapBonded(BondSchema schema, Random rng, out BondSchema after, out AppliedMutation mutation)
    {
        var candidates = FieldCandidates(schema, f => f.Type is BondType.Bonded).ToArray();
        if (candidates.Length == 0)
        {
            after = schema;
            mutation = null!;
            return false;
        }

        var candidate = Pick(candidates, rng);
        var original = candidate.Struct.Fields[candidate.FieldIndex];
        var bonded = (BondType.Bonded)original.Type;
        var updated = original with { Type = bonded.StructType };

        after = ReplaceField(schema, candidate, updated);
        mutation = new AppliedMutation(
            MutationKind.UnwrapBonded,
            ChangeCategory.Compatible,
            $"Unwrapped field '{original.Name}' in struct '{candidate.Struct.Name}' from {original.Type} to {updated.Type}.");
        return true;
    }

    private static bool TryConvertVectorList(BondSchema schema, Random rng, bool vectorToList, out BondSchema after, out AppliedMutation mutation)
    {
        var candidates = vectorToList
            ? FieldCandidates(schema, f => f.Type is BondType.Vector).ToArray()
            : FieldCandidates(schema, f => f.Type is BondType.List).ToArray();

        if (candidates.Length == 0)
        {
            after = schema;
            mutation = null!;
            return false;
        }

        var candidate = Pick(candidates, rng);
        var original = candidate.Struct.Fields[candidate.FieldIndex];
        BondType convertedType = vectorToList
            ? new BondType.List(((BondType.Vector)original.Type).ElementType)
            : new BondType.Vector(((BondType.List)original.Type).ElementType);
        var updated = original with { Type = convertedType };

        after = ReplaceField(schema, candidate, updated);
        mutation = new AppliedMutation(
            vectorToList ? MutationKind.VectorToList : MutationKind.ListToVector,
            ChangeCategory.Compatible,
            $"Converted field '{original.Name}' in struct '{candidate.Struct.Name}' from {original.Type} to {convertedType}.");
        return true;
    }

    private static bool TryInt32ToEnum(BondSchema schema, Random rng, out BondSchema after, out AppliedMutation mutation)
    {
        var enums = GetEnums(schema);
        // Restricted to required fields so the type swap doesn't drag a default-value
        // change along with it (optional enum fields must have a default).
        var candidates = FieldCandidates(schema, f => f.Type is BondType.Int32 && f.Modifier == FieldModifier.Required).ToArray();
        if (enums.Length == 0 || candidates.Length == 0)
        {
            after = schema;
            mutation = null!;
            return false;
        }

        var candidate = Pick(candidates, rng);
        var original = candidate.Struct.Fields[candidate.FieldIndex];
        var enumDecl = Pick(enums, rng).Enum;
        var updated = NormalizeField(original with { Type = new BondType.TypeReference(enumDecl, []) });

        after = ReplaceField(schema, candidate, updated);
        mutation = new AppliedMutation(
            MutationKind.Int32ToEnum,
            ChangeCategory.Compatible,
            $"Changed field '{original.Name}' in struct '{candidate.Struct.Name}' from int32 to enum '{enumDecl.Name}'.");
        return true;
    }

    private static bool TryEnumToInt32(BondSchema schema, Random rng, out BondSchema after, out AppliedMutation mutation)
    {
        // See TryInt32ToEnum: restricted to required so the default-removal isn't reported.
        var candidates = FieldCandidates(schema, f => f.Type is BondType.TypeReference { Declaration: EnumDeclaration } && f.Modifier == FieldModifier.Required).ToArray();
        if (candidates.Length == 0)
        {
            after = schema;
            mutation = null!;
            return false;
        }

        var candidate = Pick(candidates, rng);
        var original = candidate.Struct.Fields[candidate.FieldIndex];
        var updated = NormalizeField(original with { Type = BondType.Int32.Instance });

        after = ReplaceField(schema, candidate, updated);
        mutation = new AppliedMutation(
            MutationKind.EnumToInt32,
            ChangeCategory.Compatible,
            $"Changed field '{original.Name}' in struct '{candidate.Struct.Name}' from {original.Type} to int32.");
        return true;
    }

    private static IEnumerable<FieldCandidate> FieldCandidates(BondSchema schema, Func<Field, bool> predicate)
    {
        foreach (var s in GetStructs(schema))
        {
            for (var fieldIndex = 0; fieldIndex < s.Struct.Fields.Length; fieldIndex++)
            {
                if (predicate(s.Struct.Fields[fieldIndex]))
                {
                    yield return new FieldCandidate(s.Struct, s.DeclarationIndex, s.StructListIndex, fieldIndex);
                }
            }
        }
    }

    private static IEnumerable<NumericFieldCandidate> NumericFieldCandidates(BondSchema schema, Func<ScalarKind, bool> predicate)
    {
        foreach (var c in FieldCandidates(schema, _ => true))
        {
            if (c.Struct.Fields[c.FieldIndex].Type.TryGetScalarKind(out var kind) && predicate(kind))
            {
                yield return new NumericFieldCandidate(c.Struct, c.DeclarationIndex, c.StructListIndex, c.FieldIndex, kind);
            }
        }
    }

    private static EnumCandidate[] GetEnums(BondSchema schema) =>
        schema.Declarations
            .Select((decl, idx) => decl is EnumDeclaration e ? new EnumCandidate(e, idx) : null)
            .OfType<EnumCandidate>()
            .ToArray();

    private static AliasCandidate[] GetAliases(BondSchema schema) =>
        schema.Declarations
            .Select((decl, idx) => decl is AliasDeclaration a ? new AliasCandidate(a, idx) : null)
            .OfType<AliasCandidate>()
            .ToArray();

    private static StructCandidate[] GetStructs(BondSchema schema)
    {
        var result = new List<StructCandidate>();
        var structListIndex = 0;
        for (var i = 0; i < schema.Declarations.Length; i++)
        {
            if (schema.Declarations[i] is StructDeclaration s)
            {
                result.Add(new StructCandidate(s, i, structListIndex++));
            }
        }
        return result.ToArray();
    }

    private static BondSchema ReplaceField(BondSchema schema, FieldCandidate candidate, Field updatedField)
    {
        var fields = candidate.Struct.Fields.ToArray();
        fields[candidate.FieldIndex] = updatedField;
        var updatedStruct = candidate.Struct with { Fields = fields };
        return ReplaceStruct(schema, candidate.DeclarationIndex, updatedStruct);
    }

    private static BondSchema ReplaceStruct(BondSchema schema, int declarationIndex, StructDeclaration updatedStruct)
    {
        return ReplaceDeclaration(schema, declarationIndex, updatedStruct);
    }

    private static BondSchema ReplaceDeclaration(BondSchema schema, int declarationIndex, Declaration updatedDeclaration)
    {
        var declarations = schema.Declarations.ToArray();
        declarations[declarationIndex] = updatedDeclaration;
        return schema with { Declarations = declarations };
    }

    private static BondSchema ReplaceAliasAndNormalizeFields(BondSchema schema, AliasCandidate candidate, AliasDeclaration updatedAlias)
    {
        var declarations = schema.Declarations.ToArray();
        declarations[candidate.DeclarationIndex] = updatedAlias;

        var updatedSchema = schema with { Declarations = declarations };
        updatedSchema = RebindAliases(updatedSchema);
        return NormalizeStructFields(updatedSchema);
    }

    private static string NextFieldName(StructDeclaration target)
    {
        var used = target.Fields.Select(field => field.Name).ToHashSet(StringComparer.Ordinal);
        var index = target.Fields.Length;
        while (!used.Add($"f{index}"))
        {
            index++;
        }

        return $"f{index}";
    }

    private static string NextConstantName(EnumDeclaration target)
    {
        var used = target.Constants.Select(constant => constant.Name).ToHashSet(StringComparer.Ordinal);
        var index = target.Constants.Length;
        while (!used.Add($"C{index}"))
        {
            index++;
        }

        return $"C{index}";
    }

    private static long NextConstantValue(EnumDeclaration target) =>
        target.Constants
            .Select(constant => constant.Value ?? 0)
            .DefaultIfEmpty(0)
            .Max() + 1;

    private static ushort NextOrdinal(StructDeclaration target) =>
        (ushort)(target.Fields.Max(field => field.Ordinal) + 1);

    // After a type/modifier change we re-evaluate the field's default:
    // - If the new type wants an enum default and the field doesn't have one, add one.
    // - If the field had an enum default but the new type isn't an enum, drop it.
    private static Field NormalizeField(Field field, BondSchema? schema = null)
    {
        var desiredDefault = schema is null
            ? SchemaGenerator.CreateDefaultValue(field.Type, field.Modifier)
            : CreateDefaultValue(field.Type, field.Modifier, schema);
        var currentDefault = field.DefaultValue;

        if (desiredDefault is not null)
        {
            currentDefault = schema is null ? currentDefault ?? desiredDefault : desiredDefault;
        }
        else if (currentDefault is Default.Enum)
        {
            currentDefault = null;
        }

        return field with { DefaultValue = currentDefault };
    }

    private static BondSchema RebindAliases(BondSchema schema)
    {
        var aliasMap = GetAliases(schema).ToDictionary(candidate => candidate.Alias.QualifiedName, candidate => candidate.Alias, StringComparer.Ordinal);
        var declarations = schema.Declarations
            .Select(declaration => declaration switch
            {
                AliasDeclaration alias => alias with { AliasedType = RebindAliases(alias.AliasedType, aliasMap) },
                StructDeclaration structDeclaration => structDeclaration with
                {
                    Fields = structDeclaration.Fields
                        .Select(field => field with { Type = RebindAliases(field.Type, aliasMap) })
                        .ToArray()
                },
                _ => declaration
            })
            .ToArray();

        return schema with { Declarations = declarations };
    }

    private static BondType RebindAliases(BondType type, IReadOnlyDictionary<string, AliasDeclaration> aliasMap) =>
        type switch
        {
            BondType.List list => new BondType.List(RebindAliases(list.ElementType, aliasMap)),
            BondType.Vector vector => new BondType.Vector(RebindAliases(vector.ElementType, aliasMap)),
            BondType.Set set => new BondType.Set(RebindAliases(set.KeyType, aliasMap)),
            BondType.Map map => new BondType.Map(
                RebindAliases(map.KeyType, aliasMap),
                RebindAliases(map.ValueType, aliasMap)),
            BondType.Nullable nullable => new BondType.Nullable(RebindAliases(nullable.ElementType, aliasMap)),
            BondType.Maybe maybe => new BondType.Maybe(RebindAliases(maybe.ElementType, aliasMap)),
            BondType.Bonded bonded => new BondType.Bonded(RebindAliases(bonded.StructType, aliasMap)),
            BondType.TypeReference { Declaration: AliasDeclaration aliasDeclaration } typeReference
                when aliasMap.TryGetValue(aliasDeclaration.QualifiedName, out var currentAlias) =>
                    new BondType.TypeReference(currentAlias, typeReference.TypeArguments.Select(arg => RebindAliases(arg, aliasMap)).ToArray()),
            BondType.TypeReference typeReference =>
                new BondType.TypeReference(typeReference.Declaration, typeReference.TypeArguments.Select(arg => RebindAliases(arg, aliasMap)).ToArray()),
            BondType.UnresolvedType unresolved =>
                new BondType.UnresolvedType(unresolved.QualifiedName, unresolved.TypeArguments.Select(arg => RebindAliases(arg, aliasMap)).ToArray()),
            _ => type
        };

    private static BondSchema NormalizeStructFields(BondSchema schema)
    {
        var declarations = schema.Declarations
            .Select(declaration => declaration is StructDeclaration structDeclaration
                ? structDeclaration with
                {
                    Fields = structDeclaration.Fields
                        .Select(field => NormalizeField(field, schema))
                        .ToArray()
                }
                : declaration)
            .ToArray();

        return schema with { Declarations = declarations };
    }

    private static Default? CreateDefaultValue(BondType type, FieldModifier modifier, BondSchema schema)
    {
        if (modifier == FieldModifier.Required)
        {
            return null;
        }

        return ResolveToEnum(type, schema, new HashSet<string>(StringComparer.Ordinal)) is { } enumDeclaration
            ? new Default.Enum(enumDeclaration.Constants[0].Name)
            : null;
    }

    private static EnumDeclaration? ResolveToEnum(BondType type, BondSchema schema, HashSet<string> visitedAliases)
    {
        if (type is BondType.TypeReference { Declaration: EnumDeclaration enumDeclaration })
        {
            return enumDeclaration;
        }

        if (type is not BondType.TypeReference { Declaration: AliasDeclaration aliasDeclaration })
        {
            return null;
        }

        if (!visitedAliases.Add(aliasDeclaration.QualifiedName))
        {
            return null;
        }

        var currentAlias = schema.Declarations
            .OfType<AliasDeclaration>()
            .FirstOrDefault(alias => alias.QualifiedName == aliasDeclaration.QualifiedName);

        return currentAlias is null
            ? null
            : ResolveToEnum(currentAlias.AliasedType, schema, visitedAliases);
    }

    private static BondType CreateIncompatibleType(BondType oldType) =>
        oldType switch
        {
            BondType.String => BondType.Int32.Instance,
            BondType.Bool => BondType.String.Instance,
            BondType.Int8 or BondType.Int16 or BondType.Int32 or BondType.Int64
                or BondType.UInt8 or BondType.UInt16 or BondType.UInt32 or BondType.UInt64
                or BondType.Float or BondType.Double => BondType.String.Instance,
            _ => BondType.Bool.Instance
        };

    private static bool CanWiden(ScalarKind kind) =>
        kind is ScalarKind.Float
            or ScalarKind.Int8
            or ScalarKind.Int16
            or ScalarKind.Int32
            or ScalarKind.UInt8
            or ScalarKind.UInt16
            or ScalarKind.UInt32;

    private static bool CanNarrow(ScalarKind kind) =>
        kind is ScalarKind.Double
            or ScalarKind.Int16
            or ScalarKind.Int32
            or ScalarKind.Int64
            or ScalarKind.UInt16
            or ScalarKind.UInt32
            or ScalarKind.UInt64;

    private static ScalarKind Widen(ScalarKind kind) =>
        kind switch
        {
            ScalarKind.Float => ScalarKind.Double,
            ScalarKind.Int8 => ScalarKind.Int16,
            ScalarKind.Int16 => ScalarKind.Int32,
            ScalarKind.Int32 => ScalarKind.Int64,
            ScalarKind.UInt8 => ScalarKind.UInt16,
            ScalarKind.UInt16 => ScalarKind.UInt32,
            ScalarKind.UInt32 => ScalarKind.UInt64,
            _ => throw new InvalidOperationException($"Cannot widen scalar kind {kind}.")
        };

    private static ScalarKind Narrow(ScalarKind kind) =>
        kind switch
        {
            ScalarKind.Double => ScalarKind.Float,
            ScalarKind.Int64 => ScalarKind.Int32,
            ScalarKind.Int32 => ScalarKind.Int16,
            ScalarKind.Int16 => ScalarKind.Int8,
            ScalarKind.UInt64 => ScalarKind.UInt32,
            ScalarKind.UInt32 => ScalarKind.UInt16,
            ScalarKind.UInt16 => ScalarKind.UInt8,
            _ => throw new InvalidOperationException($"Cannot narrow scalar kind {kind}.")
        };

    private static T Pick<T>(IReadOnlyList<T> items, Random rng) => items[rng.Next(items.Count)];

    private static void Shuffle<T>(T[] items, Random rng)
    {
        for (var i = items.Length - 1; i > 0; i--)
        {
            var j = rng.Next(i + 1);
            (items[i], items[j]) = (items[j], items[i]);
        }
    }

    private sealed record EnumCandidate(EnumDeclaration Enum, int DeclarationIndex);

    private sealed record AliasCandidate(AliasDeclaration Alias, int DeclarationIndex);

    private sealed record StructCandidate(StructDeclaration Struct, int DeclarationIndex, int StructListIndex);

    private record FieldCandidate(
        StructDeclaration Struct,
        int DeclarationIndex,
        int StructListIndex,
        int FieldIndex);

    private sealed record NumericFieldCandidate(
        StructDeclaration Struct,
        int DeclarationIndex,
        int StructListIndex,
        int FieldIndex,
        ScalarKind ScalarKind) : FieldCandidate(Struct, DeclarationIndex, StructListIndex, FieldIndex);
}
