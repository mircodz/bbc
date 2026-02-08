using System;
using System.Numerics;
using Bond.Parser.Syntax;
using Bond.Parser.Util;

namespace Bond.Parser.Parser;

/// <summary>
/// Validates types and default values
/// </summary>
public static class TypeValidator
{
    /// <summary>
    /// Validates that a default value matches the field type
    /// </summary>
    public static bool ValidateDefaultValue(BondType fieldType, Default? defaultValue)
    {
        if (defaultValue == null)
        {
            return true;
        }

        // Maybe/Nullable types can have 'nothing' default
        if (fieldType is BondType.Maybe or BondType.Nullable)
        {
            return defaultValue is Default.Nothing;
        }

        // Container types (list, set, map, vector) can have 'nothing' default
        if (fieldType is BondType.List or BondType.Set or BondType.Map or BondType.Vector)
        {
            return defaultValue is Default.Nothing;
        }

        return (fieldType, defaultValue) switch
        {
            (BondType.Int8, Default.Integer i) => i.Value.IsInBounds<sbyte>(),
            (BondType.Int16, Default.Integer i) => i.Value.IsInBounds<short>(),
            (BondType.Int32, Default.Integer i) => i.Value.IsInBounds<int>(),
            (BondType.Int64, Default.Integer i) => i.Value.IsInBounds<long>(),
            (BondType.UInt8, Default.Integer i) => i.Value >= 0 && i.Value.IsInBounds<byte>(),
            (BondType.UInt16, Default.Integer i) => i.Value >= 0 && i.Value.IsInBounds<ushort>(),
            (BondType.UInt32, Default.Integer i) => i.Value >= 0 && i.Value.IsInBounds<uint>(),
            (BondType.UInt64, Default.Integer i) => i.Value >= 0 && i.Value.IsInBounds<ulong>(),
            (BondType.Float, Default.Float) => true,
            (BondType.Float, Default.Integer) => true, // Can convert int to float
            (BondType.Double, Default.Float) => true,
            (BondType.Double, Default.Integer) => true, // Can convert int to double
            (BondType.Bool, Default.Bool) => true,
            (BondType.String, Default.String) => true,
            (BondType.WString, Default.String) => true,
            (BondType.UserDefined { Declaration: EnumDeclaration }, Default.Enum) => true,
            (BondType.UserDefined { Declaration: EnumDeclaration }, Default.Nothing) => true,
            (BondType.UnresolvedUserType, Default.Enum) => true,
            (BondType.UnresolvedUserType, Default.String) => true,
            (BondType.UnresolvedUserType, Default.Integer) => true,
            (BondType.UnresolvedUserType, Default.Float) => true,
            (BondType.UnresolvedUserType, Default.Bool) => true,
            (BondType.UnresolvedUserType, Default.Nothing) => true,
            (BondType.TypeParameter, _) => true,
            _ => false
        };
    }

    /// <summary>
    /// Validates that a type can be used as a map/set key
    /// </summary>
    public static bool IsValidKeyType(BondType type)
    {
        return type.IsValidKeyType();
    }

    /// <summary>
    /// Validates field ordinal is within valid range
    /// </summary>
    public static bool IsValidOrdinal(ushort ordinal)
    {
        // Ordinals must be 0-65535 (covered by ushort type)
        return true;
    }

    /// <summary>
    /// Validates that enum fields have default values
    /// </summary>
    public static void ValidateEnumField(Field field)
    {
        if (field.Type.IsEnum() && field.DefaultValue == null)
        {
            throw new InvalidOperationException(
                $"Enum field '{field.Name}' must have a default value");
        }
    }

    /// <summary>
    /// Validates that struct fields don't have 'nothing' default
    /// </summary>
    public static void ValidateStructField(Field field)
    {
        if (field.Type.IsStruct() && field.DefaultValue is Default.Nothing)
        {
            throw new InvalidOperationException(
                $"Struct field '{field.Name}' cannot have default value of 'nothing'");
        }
    }
}
