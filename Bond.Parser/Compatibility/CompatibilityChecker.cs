using System;
using System.Collections.Generic;
using System.Linq;
using Bond.Parser.Syntax;

namespace Bond.Parser.Compatibility;

/// <summary>
/// Checks compatibility between two Bond schemas to detect breaking changes
/// </summary>
public class CompatibilityChecker
{
    /// <summary>
    /// Checks compatibility between old and new schemas
    /// </summary>
    public List<SchemaChange> CheckCompatibility(Syntax.Bond oldSchema, Syntax.Bond newSchema)
    {
        var changes = new List<SchemaChange>();

        // Compare declarations by name
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

        // Compare matching declarations
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
        // Check if declaration type changed
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
        }
    }

    private void CompareStructs(StructDeclaration oldStruct, StructDeclaration newStruct, List<SchemaChange> changes)
    {
        var location = $"struct {oldStruct.Name}";

        // Check inheritance change
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

        // Compare fields by ordinal
        var oldFields = oldStruct.Fields.ToDictionary(f => f.Ordinal, f => f);
        var newFields = newStruct.Fields.ToDictionary(f => f.Ordinal, f => f);

        // Check for removed fields
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

        // Check for added fields
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

        // Compare matching fields
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

        // Check modifier changes
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

        // Check type changes
        if (!TypesEqual(oldField.Type, newField.Type))
        {
            var typeChange = ClassifyTypeChange(oldField.Type, newField.Type);
            changes.Add(new SchemaChange(
                typeChange.Category,
                $"Type changed from {oldField.Type} to {newField.Type}",
                location,
                typeChange.Recommendation));
        }

        // Check default value changes
        if (!DefaultsEqual(oldField.DefaultValue, newField.DefaultValue))
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

        var oldConstants = oldEnum.Constants.ToList();
        var newConstants = newEnum.Constants.ToList();

        // Build maps by name and value
        var oldByName = oldConstants.ToDictionary(c => c.Name, c => c);
        var newByName = newConstants.ToDictionary(c => c.Name, c => c);

        // Check for removed constants
        foreach (var oldConst in oldConstants)
        {
            if (!newByName.ContainsKey(oldConst.Name))
            {
                changes.Add(new SchemaChange(
                    ChangeCategory.BreakingWire,
                    $"Enum constant '{oldConst.Name}' was removed",
                    $"{location}.{oldConst.Name}",
                    "Removing enum constants breaks compatibility"));
            }
        }

        // Check for added constants
        foreach (var newConst in newConstants)
        {
            if (!oldByName.ContainsKey(newConst.Name))
            {
                // Adding a constant without an explicit value in the middle shifts
                // all subsequent implicit values, which is wire-breaking.
                var newIndex = newConstants.IndexOf(newConst);
                var couldReorder = newIndex < oldConstants.Count && !newConst.Value.HasValue;

                var category = couldReorder ? ChangeCategory.BreakingWire : ChangeCategory.Compatible;
                var recommendation = couldReorder
                    ? "Adding constant without explicit value in the middle can cause implicit reordering"
                    : null;

                changes.Add(new SchemaChange(
                    category,
                    $"Enum constant '{newConst.Name}' was added",
                    $"{location}.{newConst.Name}",
                    recommendation));
            }
        }

        // Check for changed constants
        foreach (var (name, oldConst) in oldByName)
        {
            if (newByName.TryGetValue(name, out var newConst))
            {
                if (oldConst.Value != newConst.Value)
                {
                    changes.Add(new SchemaChange(
                        ChangeCategory.BreakingWire,
                        $"Enum constant '{name}' value changed from {oldConst.Value} to {newConst.Value}",
                        $"{location}.{name}",
                        "Changing enum constant values breaks compatibility"));
                }

                // Check for position change (implicit reordering)
                var oldIndex = oldConstants.IndexOf(oldConst);
                var newIndex = newConstants.IndexOf(newConst);
                if (oldIndex != newIndex)
                {
                    changes.Add(new SchemaChange(
                        ChangeCategory.BreakingWire,
                        $"Enum constant '{name}' position changed (implicit reordering)",
                        $"{location}.{name}",
                        "Reordering enum constants can change implicit values"));
                }
            }
        }
    }

    private void CompareServices(ServiceDeclaration oldService, ServiceDeclaration newService, List<SchemaChange> changes)
    {
        var location = $"service {oldService.Name}";

        // Compare methods by name
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

        // Compare matching methods
        foreach (var (name, oldMethod) in oldMethods)
        {
            if (newMethods.TryGetValue(name, out var newMethod))
            {
                if (oldMethod.ToString() != newMethod.ToString())
                {
                    changes.Add(new SchemaChange(
                        ChangeCategory.BreakingWire,
                        $"Method signature changed",
                        $"{location}.{name}",
                        $"Old: {oldMethod}\nNew: {newMethod}"));
                }
            }
        }
    }

    private void CompareAliases(AliasDeclaration oldAlias, AliasDeclaration newAlias, List<SchemaChange> changes)
    {
        if (!TypesEqual(oldAlias.AliasedType, newAlias.AliasedType))
        {
            changes.Add(new SchemaChange(
                ChangeCategory.BreakingWire,
                $"Alias type changed from {oldAlias.AliasedType} to {newAlias.AliasedType}",
                $"alias {oldAlias.Name}"));
        }
    }

    private static ChangeCategory ClassifyModifierChange(FieldModifier oldMod, FieldModifier newMod)
    {
        // Direct optional <-> required is breaking
        if ((oldMod == FieldModifier.Optional && newMod == FieldModifier.Required) ||
            (oldMod == FieldModifier.Required && newMod == FieldModifier.Optional))
        {
            return ChangeCategory.BreakingWire;
        }

        // Two-step changes via required_optional are safe but need careful rollout
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
        // Safe type changes
        if (IsInt32ToEnumChange(oldType, newType) || IsInt32ToEnumChange(newType, oldType))
            return (ChangeCategory.Compatible, null);

        if (IsVectorListChange(oldType, newType))
            return (ChangeCategory.Compatible, null);

        if (IsBlobVectorChange(oldType, newType))
            return (ChangeCategory.Compatible, null);

        if (IsBondedChange(oldType, newType))
            return (ChangeCategory.Compatible, null);

        // Numeric promotions (require careful rollout — consumers must update first)
        if (IsNumericPromotion(oldType, newType))
            return (ChangeCategory.Compatible, "Deploy to consumers before producers when promoting numeric types");

        if (IsIntToEnumPromotion(oldType, newType))
            return (ChangeCategory.Compatible, "Deploy to consumers before producers when promoting int8/int16 to enum");

        // Everything else is breaking
        return (ChangeCategory.BreakingWire, "This type change is not compatible");
    }

    private static bool IsInt32ToEnumChange(BondType type1, BondType type2) =>
        type1 is BondType.Int32 &&
        type2 is BondType.UserDefined { Declaration: EnumDeclaration };

    private static bool IsVectorListChange(BondType type1, BondType type2) =>
        (type1, type2) switch
        {
            (BondType.Vector v, BondType.List l)   => TypesEqual(v.ElementType, l.ElementType),
            (BondType.List l,   BondType.Vector v) => TypesEqual(l.ElementType, v.ElementType),
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
            (BondType.Bonded bonded, var t) => TypesEqual(bonded.StructType, t),
            (var t, BondType.Bonded bonded) => TypesEqual(t, bonded.StructType),
            _ => false
        };

    // Static table allocated once; HashSet gives O(1) lookup.
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
        newType is BondType.UserDefined { Declaration: EnumDeclaration };

    // Structural type equality. BondType.UserDefined contains a BondType[] for type
    // arguments; array reference equality is insufficient, so we recurse manually.
    // For all other BondType subtypes the generated record == operator is correct.
    private static bool TypesEqual(BondType type1, BondType type2) =>
        (type1, type2) switch
        {
            (BondType.UserDefined u1, BondType.UserDefined u2) =>
                u1.Declaration.QualifiedName == u2.Declaration.QualifiedName &&
                u1.TypeArguments.Length == u2.TypeArguments.Length &&
                u1.TypeArguments.Zip(u2.TypeArguments).All(p => TypesEqual(p.First, p.Second)),
            _ => type1 == type2
        };

    // Default record equality handles all subtypes correctly. Unlike ToString(),
    // this correctly distinguishes Default.Float(1.0) from Default.Integer(1).
    private static bool DefaultsEqual(Default? default1, Default? default2) =>
        default1 == default2;
}
