using System;
using System.Collections.Generic;
using System.Linq;
using Bond.Parser.Syntax;

namespace Bond.Parser.Compatibility;

public class CompatibilityChecker
{
    public List<SchemaChange> CheckCompatibility(Syntax.Bond oldSchema, Syntax.Bond newSchema)
    {
        var changes = new List<SchemaChange>();

        var oldDecls = oldSchema.Declarations.ToDictionary(d => d.QualifiedName, d => d);
        var newDecls = newSchema.Declarations.ToDictionary(d => d.QualifiedName, d => d);

        foreach (var oldDecl in oldDecls.Values)
        {
            if (!newDecls.ContainsKey(oldDecl.QualifiedName))
            {
                changes.Add(new SchemaChange(
                    ChangeCategory.BreakingWire,
                    $"{oldDecl.Kind} '{oldDecl.Name}' was removed",
                    oldDecl.QualifiedName,
                    "Removing declarations breaks existing code using them"));
            }
        }

        foreach (var newDecl in newDecls.Values)
        {
            if (!oldDecls.ContainsKey(newDecl.QualifiedName))
            {
                changes.Add(new SchemaChange(
                    ChangeCategory.Compatible,
                    $"{newDecl.Kind} '{newDecl.Name}' was added",
                    newDecl.QualifiedName));
            }
        }

        foreach (var (name, oldDecl) in oldDecls)
        {
            if (newDecls.TryGetValue(name, out var newDecl))
            {
                CompareDeclarations(oldDecl, newDecl, changes);
            }
        }

        return changes;
    }

    private void CompareDeclarations(Declaration oldDecl, Declaration newDecl, List<SchemaChange> changes)
    {
        if (oldDecl.GetType() != newDecl.GetType())
        {
            changes.Add(new SchemaChange(
                ChangeCategory.BreakingWire,
                $"Declaration kind changed from {oldDecl.Kind} to {newDecl.Kind}",
                oldDecl.QualifiedName));
            return;
        }

        switch (oldDecl, newDecl)
        {
            case (StructDeclaration oldStruct, StructDeclaration newStruct):
                CompareStructs(oldStruct, newStruct, changes);
                break;
            case (EnumDeclaration oldEnum, EnumDeclaration newEnum):
                CompareEnums(oldEnum, newEnum, changes);
                break;
            case (ServiceDeclaration oldService, ServiceDeclaration newService):
                CompareServices(oldService, newService, changes);
                break;
            case (AliasDeclaration oldAlias, AliasDeclaration newAlias):
                CompareAliases(oldAlias, newAlias, changes);
                break;
            case (ForwardDeclaration, ForwardDeclaration):
                // Forwards carry only Name + TypeParameters, both already checked by
                // QualifiedName matching and the declaration-kind check above.
                break;
            default:
                throw new InvalidOperationException(
                    $"CompareDeclarations: unhandled declaration kind '{oldDecl.Kind}' (type {oldDecl.GetType().Name})");
        }
    }

    private void CompareStructs(StructDeclaration oldStruct, StructDeclaration newStruct, List<SchemaChange> changes)
    {
        var location = $"struct {oldStruct.Name}";

        var oldBase = oldStruct.BaseType?.ToString() ?? "";
        var newBase = newStruct.BaseType?.ToString() ?? "";
        if (oldBase != newBase)
        {
            changes.Add(new SchemaChange(
                ChangeCategory.BreakingWire,
                $"Inheritance hierarchy changed from '{oldBase}' to '{newBase}'",
                location,
                "Changing inheritance breaks wire compatibility"));
        }

        var oldFields = oldStruct.Fields.ToDictionary(f => f.Ordinal, f => f);
        var newFields = newStruct.Fields.ToDictionary(f => f.Ordinal, f => f);

        foreach (var oldField in oldFields.Values)
        {
            if (!newFields.ContainsKey(oldField.Ordinal))
            {
                var category = oldField.Modifier == FieldModifier.Required
                    ? ChangeCategory.BreakingWire
                    : ChangeCategory.Compatible;

                var recommendation = oldField.Modifier == FieldModifier.Required
                    ? "Removing required fields breaks compatibility."
                    : "Consider commenting out the field rather than removing it to avoid ordinal/name reuse.";

                changes.Add(new SchemaChange(
                    category,
                    $"Field {oldField.Ordinal} '{oldField.Name}' ({oldField.Modifier}) was removed",
                    $"{location}.{oldField.Name}",
                    recommendation));
            }
        }

        foreach (var newField in newFields.Values)
        {
            if (!oldFields.ContainsKey(newField.Ordinal))
            {
                var category = newField.Modifier == FieldModifier.Required
                    ? ChangeCategory.BreakingWire
                    : ChangeCategory.Compatible;

                var recommendation = newField.Modifier == FieldModifier.Required
                    ? "Adding required fields breaks compatibility with old data"
                    : null;

                changes.Add(new SchemaChange(
                    category,
                    $"Field {newField.Ordinal} '{newField.Name}' ({newField.Modifier}) was added",
                    $"{location}.{newField.Name}",
                    recommendation));
            }
        }

        foreach (var (ordinal, oldField) in oldFields)
        {
            if (newFields.TryGetValue(ordinal, out var newField))
            {
                CompareFields(oldField, newField, location, changes);
            }
        }
    }

    private void CompareFields(Field oldField, Field newField, string structLocation, List<SchemaChange> changes)
    {
        var location = $"{structLocation}.{oldField.Name}";

        // Field name changes are safe on the wire (ordinals are used) but break
        // text-based protocols like SimpleJSON which key on field names.
        if (oldField.Name != newField.Name)
        {
            changes.Add(new SchemaChange(
                ChangeCategory.BreakingText,
                $"Field name changed from '{oldField.Name}' to '{newField.Name}'",
                location));
        }

        if (oldField.Modifier != newField.Modifier)
        {
            var changeCategory = ClassifyModifierChange(oldField.Modifier, newField.Modifier);
            var recommendation = GetModifierChangeRecommendation(oldField.Modifier, newField.Modifier);

            changes.Add(new SchemaChange(
                changeCategory,
                $"Modifier changed from {oldField.Modifier} to {newField.Modifier}",
                location,
                recommendation));
        }

        if (oldField.Type != newField.Type)
        {
            var typeChange = ClassifyTypeChange(oldField.Type, newField.Type);
            changes.Add(new SchemaChange(
                typeChange.Category,
                $"Type changed from {oldField.Type} to {newField.Type}",
                location,
                typeChange.Recommendation));
        }

        if (oldField.DefaultValue != newField.DefaultValue)
        {
            changes.Add(new SchemaChange(
                ChangeCategory.BreakingWire,
                $"Default value changed from {oldField.DefaultValue} to {newField.DefaultValue}",
                location,
                "Changing default values breaks wire compatibility"));
        }
    }

    private void CompareEnums(EnumDeclaration oldEnum, EnumDeclaration newEnum, List<SchemaChange> changes)
    {
        var location = $"enum {oldEnum.Name}";

        // Bond rule: adding constants is safe iff they don't shift any existing
        // constant's effective integer value.
        var oldEffective = EffectiveValues(oldEnum.Constants);
        var newEffective = EffectiveValues(newEnum.Constants);

        var oldByName = oldEnum.Constants
            .Select((c, i) => (c.Name, Value: oldEffective[i]))
            .ToDictionary(x => x.Name, x => x.Value);
        var newByName = newEnum.Constants
            .Select((c, i) => (c.Name, Value: newEffective[i]))
            .ToDictionary(x => x.Name, x => x.Value);

        foreach (var (name, _) in oldByName)
        {
            if (!newByName.ContainsKey(name))
            {
                changes.Add(new SchemaChange(
                    ChangeCategory.BreakingWire,
                    $"Enum constant '{name}' was removed",
                    $"{location}.{name}",
                    "Removing enum constants breaks compatibility"));
            }
        }

        foreach (var (name, newValue) in newByName)
        {
            if (!oldByName.ContainsKey(name))
            {
                // Same integer mapping to two names breaks round-tripping.
                var colliding = oldByName
                    .Where(kv => kv.Value == newValue)
                    .Select(kv => kv.Key)
                    .FirstOrDefault();

                var category = colliding != null ? ChangeCategory.BreakingWire : ChangeCategory.Compatible;
                var recommendation = colliding != null
                    ? $"New constant '{name}' has effective value {newValue} which collides with existing constant '{colliding}'"
                    : null;

                changes.Add(new SchemaChange(
                    category,
                    $"Enum constant '{name}' was added",
                    $"{location}.{name}",
                    recommendation));
            }
        }

        // Attribute downward shifts to preceding removals when the math lines up: if a
        // constant shifted down by N and exactly N removed constants used to sit at lower
        // ordinals, the removals explain the shift. Per-constant signals stay (a user may
        // care about a specific name) but the recommendation now names the root cause.
        var removedBeforeByValue = oldByName
            .Where(kv => !newByName.ContainsKey(kv.Key))
            .OrderBy(kv => kv.Value)
            .ToList();

        foreach (var (name, oldValue) in oldByName)
        {
            if (!newByName.TryGetValue(name, out var newValue) || oldValue == newValue) continue;

            var delta = oldValue - newValue;
            var precedingRemovals = removedBeforeByValue
                .Where(r => r.Value < oldValue)
                .Select(r => r.Key)
                .ToList();

            var description = $"Enum constant '{name}' value changed from {oldValue} to {newValue}";
            if (delta > 0 && precedingRemovals.Count == delta)
            {
                description += $" (caused by removal of {string.Join(", ", precedingRemovals.Select(r => $"'{r}'"))})";
            }

            changes.Add(new SchemaChange(
                ChangeCategory.BreakingWire,
                description,
                $"{location}.{name}",
                "Changing enum constant values breaks compatibility"));
        }
    }

    private static long[] EffectiveValues(Constant[] constants)
    {
        var values = new long[constants.Length];
        long next = 0;
        for (int i = 0; i < constants.Length; i++)
        {
            values[i] = constants[i].Value ?? next;
            next = values[i] + 1;
        }
        return values;
    }

    private void CompareServices(ServiceDeclaration oldService, ServiceDeclaration newService, List<SchemaChange> changes)
    {
        var location = $"service {oldService.Name}";

        var oldBase = oldService.BaseType?.ToString() ?? "";
        var newBase = newService.BaseType?.ToString() ?? "";
        if (oldBase != newBase)
        {
            changes.Add(new SchemaChange(
                ChangeCategory.BreakingWire,
                $"Inheritance changed from '{(oldService.BaseType != null ? oldBase : "none")}' to '{(newService.BaseType != null ? newBase : "none")}'",
                location,
                "Changing service inheritance breaks compatibility"));
        }

        var oldMethods = oldService.Methods.ToDictionary(m => m.Name, m => m);
        var newMethods = newService.Methods.ToDictionary(m => m.Name, m => m);

        foreach (var oldMethod in oldMethods.Values)
        {
            if (!newMethods.ContainsKey(oldMethod.Name))
            {
                changes.Add(new SchemaChange(
                    ChangeCategory.BreakingWire,
                    $"Method '{oldMethod.Name}' was removed",
                    $"{location}.{oldMethod.Name}"));
            }
        }

        foreach (var newMethod in newMethods.Values)
        {
            if (!oldMethods.ContainsKey(newMethod.Name))
            {
                changes.Add(new SchemaChange(
                    ChangeCategory.Compatible,
                    $"Method '{newMethod.Name}' was added",
                    $"{location}.{newMethod.Name}"));
            }
        }

        foreach (var (name, oldMethod) in oldMethods)
        {
            if (newMethods.TryGetValue(name, out var newMethod))
            {
                CompareMethods(location, oldMethod, newMethod, changes);
            }
        }
    }

    private void CompareMethods(string serviceLocation, Method oldMethod, Method newMethod, List<SchemaChange> changes)
    {
        var location = $"{serviceLocation}.{oldMethod.Name}";

        if (oldMethod.GetType() != newMethod.GetType())
        {
            changes.Add(new SchemaChange(
                ChangeCategory.BreakingWire,
                $"Method '{oldMethod.Name}' kind changed from {MethodKindName(oldMethod)} to {MethodKindName(newMethod)}",
                location));
            return;
        }

        var oldInput = MethodInput(oldMethod);
        var newInput = MethodInput(newMethod);
        if (oldInput != newInput)
        {
            changes.Add(new SchemaChange(
                ChangeCategory.BreakingWire,
                $"Method '{oldMethod.Name}' input changed from {oldInput} to {newInput}",
                location));
        }

        if (oldMethod is FunctionMethod fOld && newMethod is FunctionMethod fNew && fOld.ResultType != fNew.ResultType)
        {
            changes.Add(new SchemaChange(
                ChangeCategory.BreakingWire,
                $"Method '{oldMethod.Name}' result changed from {fOld.ResultType} to {fNew.ResultType}",
                location));
        }
    }

    private static MethodType MethodInput(Method method) => method switch
    {
        FunctionMethod f => f.InputType,
        EventMethod e => e.InputType,
        _ => MethodType.Void.Instance
    };

    private static string MethodKindName(Method method) => method switch
    {
        FunctionMethod => "function",
        EventMethod => "event",
        _ => "unknown"
    };

    private void CompareAliases(AliasDeclaration oldAlias, AliasDeclaration newAlias, List<SchemaChange> changes)
    {
        if (oldAlias.AliasedType != newAlias.AliasedType)
        {
            var typeChange = ClassifyTypeChange(oldAlias.AliasedType, newAlias.AliasedType);
            changes.Add(new SchemaChange(
                typeChange.Category,
                $"Alias type changed from {oldAlias.AliasedType} to {newAlias.AliasedType}",
                $"alias {oldAlias.Name}",
                typeChange.Recommendation));
        }
    }

    // optional ↔ required directly is breaking; transitions through required_optional
    // are safe with careful rollout.
    private static ChangeCategory ClassifyModifierChange(FieldModifier oldMod, FieldModifier newMod)
    {
        if ((oldMod == FieldModifier.Optional && newMod == FieldModifier.Required) ||
            (oldMod == FieldModifier.Required && newMod == FieldModifier.Optional))
        {
            return ChangeCategory.BreakingWire;
        }

        return ChangeCategory.Compatible;
    }

    private static string GetModifierChangeRecommendation(FieldModifier oldMod, FieldModifier newMod)
    {
        if ((oldMod == FieldModifier.Optional && newMod == FieldModifier.Required) ||
            (oldMod == FieldModifier.Required && newMod == FieldModifier.Optional))
        {
            return "Use required_optional as intermediate step: optional → required_optional → required";
        }

        return "Deploy to all consumers before deploying to producers";
    }

    private static (ChangeCategory Category, string? Recommendation) ClassifyTypeChange(BondType oldType, BondType newType)
    {
        // Recursion base case: structural equality (e.g. unchanged map key when only the
        // value type differs).
        if (oldType == newType) return (ChangeCategory.Compatible, null);

        if (IsInt32ToEnumChange(oldType, newType) || IsInt32ToEnumChange(newType, oldType))
            return (ChangeCategory.Compatible, null);

        if (IsVectorListChange(oldType, newType))
            return (ChangeCategory.Compatible, null);

        if (IsBlobVectorChange(oldType, newType))
            return (ChangeCategory.Compatible, null);

        if (IsBondedChange(oldType, newType))
            return (ChangeCategory.Compatible, null);

        if (IsNumericPromotion(oldType, newType))
            return (ChangeCategory.Compatible, "Deploy to consumers before producers when promoting numeric types");

        if (IsIntToEnumPromotion(oldType, newType))
            return (ChangeCategory.Compatible, "Deploy to consumers before producers when promoting int8/int16 to enum");

        // Same container shape: classify the inner change(s). Lets `map<string, int8>` →
        // `map<string, int16>` be recognized as a numeric-promotion-of-the-value-type
        // instead of an opaque BreakingWire.
        var inner = ClassifyContainerChange(oldType, newType);
        if (inner is { } result) return result;

        return (ChangeCategory.BreakingWire, "This type change is not compatible");
    }

    private static (ChangeCategory Category, string? Recommendation)? ClassifyContainerChange(BondType oldType, BondType newType) =>
        (oldType, newType) switch
        {
            (BondType.List a,     BondType.List b)     => ClassifyTypeChange(a.ElementType, b.ElementType),
            (BondType.Vector a,   BondType.Vector b)   => ClassifyTypeChange(a.ElementType, b.ElementType),
            (BondType.Set a,      BondType.Set b)      => ClassifyTypeChange(a.KeyType,     b.KeyType),
            (BondType.Nullable a, BondType.Nullable b) => ClassifyTypeChange(a.ElementType, b.ElementType),
            (BondType.Maybe a,    BondType.Maybe b)    => ClassifyTypeChange(a.ElementType, b.ElementType),
            (BondType.Bonded a,   BondType.Bonded b)   => ClassifyTypeChange(a.StructType,  b.StructType),
            (BondType.Map a,      BondType.Map b)      => CombineChanges(
                ClassifyTypeChange(a.KeyType,   b.KeyType),
                ClassifyTypeChange(a.ValueType, b.ValueType)),
            _ => null
        };

    // For map<K, V> we may have independent changes to K and V. The combined verdict is
    // the more-breaking of the two; the recommendation falls back to whichever side has
    // one (typically the compatible-with-rollout-note side).
    private static (ChangeCategory Category, string? Recommendation) CombineChanges(
        (ChangeCategory Category, string? Recommendation) key,
        (ChangeCategory Category, string? Recommendation) value)
    {
        var category = MoreSevere(key.Category, value.Category);
        var recommendation = key.Recommendation ?? value.Recommendation;
        return (category, recommendation);
    }

    private static ChangeCategory MoreSevere(ChangeCategory a, ChangeCategory b) =>
        Severity(a) >= Severity(b) ? a : b;

    private static int Severity(ChangeCategory category) => category switch
    {
        ChangeCategory.Compatible   => 0,
        ChangeCategory.BreakingText => 1,
        ChangeCategory.BreakingWire => 2,
        _ => 0
    };

    private static bool IsInt32ToEnumChange(BondType type1, BondType type2) =>
        type1 is BondType.Int32 &&
        type2 is BondType.TypeReference { Declaration: EnumDeclaration };

    private static bool IsVectorListChange(BondType type1, BondType type2) =>
        (type1, type2) switch
        {
            (BondType.Vector v, BondType.List l)   => v.ElementType == l.ElementType,
            (BondType.List l,   BondType.Vector v) => l.ElementType == v.ElementType,
            _ => false
        };

    private static bool IsBlobVectorChange(BondType type1, BondType type2) =>
        (type1, type2) switch
        {
            (BondType.Blob, BondType.Vector { ElementType: BondType.Int8 }) => true,
            (BondType.Vector { ElementType: BondType.Int8 }, BondType.Blob) => true,
            (BondType.Blob, BondType.List   { ElementType: BondType.Int8 }) => true,
            (BondType.List   { ElementType: BondType.Int8 }, BondType.Blob) => true,
            _ => false
        };

    private static bool IsBondedChange(BondType type1, BondType type2) =>
        (type1, type2) switch
        {
            (BondType.Bonded bonded, var t) => bonded.StructType == t,
            (var t, BondType.Bonded bonded) => t == bonded.StructType,
            _ => false
        };

    private static readonly HashSet<(Type, Type)> NumericPromotions =
    [
        (typeof(BondType.Float),  typeof(BondType.Double)),
        (typeof(BondType.UInt8),  typeof(BondType.UInt16)),
        (typeof(BondType.UInt8),  typeof(BondType.UInt32)),
        (typeof(BondType.UInt8),  typeof(BondType.UInt64)),
        (typeof(BondType.UInt16), typeof(BondType.UInt32)),
        (typeof(BondType.UInt16), typeof(BondType.UInt64)),
        (typeof(BondType.UInt32), typeof(BondType.UInt64)),
        (typeof(BondType.Int8),   typeof(BondType.Int16)),
        (typeof(BondType.Int8),   typeof(BondType.Int32)),
        (typeof(BondType.Int8),   typeof(BondType.Int64)),
        (typeof(BondType.Int16),  typeof(BondType.Int32)),
        (typeof(BondType.Int16),  typeof(BondType.Int64)),
        (typeof(BondType.Int32),  typeof(BondType.Int64)),
    ];

    private static bool IsNumericPromotion(BondType oldType, BondType newType) =>
        NumericPromotions.Contains((oldType.GetType(), newType.GetType()));

    private static bool IsIntToEnumPromotion(BondType oldType, BondType newType) =>
        oldType is BondType.Int8 or BondType.Int16 &&
        newType is BondType.TypeReference { Declaration: EnumDeclaration };
}
