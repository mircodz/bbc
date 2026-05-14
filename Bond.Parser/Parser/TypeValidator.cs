using System.Linq;
using Bond.Parser.Syntax;
using Bond.Parser.Util;

namespace Bond.Parser.Parser;

/// <summary>
/// Validates default values against fully-resolved field types. Callers are
/// expected to have already unwrapped aliases (see SemanticAnalyzer.UnwrapAlias).
/// </summary>
public static class TypeValidator
{
    public static bool ValidateDefaultValue(BondType fieldType, Default? defaultValue)
    {
        if (defaultValue == null) return true;
        if (fieldType is BondType.Maybe or BondType.Nullable) return defaultValue is Default.Nothing;
        if (fieldType is BondType.List or BondType.Set or BondType.Map or BondType.Vector) return defaultValue is Default.Nothing;

        return (fieldType, defaultValue) switch
        {
            (BondType.Int8,   Default.Integer i) => i.Value.IsInBounds<sbyte>(),
            (BondType.Int16,  Default.Integer i) => i.Value.IsInBounds<short>(),
            (BondType.Int32,  Default.Integer i) => i.Value.IsInBounds<int>(),
            (BondType.Int64,  Default.Integer i) => i.Value.IsInBounds<long>(),
            (BondType.UInt8,  Default.Integer i) => i.Value >= 0 && i.Value.IsInBounds<byte>(),
            (BondType.UInt16, Default.Integer i) => i.Value >= 0 && i.Value.IsInBounds<ushort>(),
            (BondType.UInt32, Default.Integer i) => i.Value >= 0 && i.Value.IsInBounds<uint>(),
            (BondType.UInt64, Default.Integer i) => i.Value >= 0 && i.Value.IsInBounds<ulong>(),
            (BondType.Float,  Default.Float or Default.Integer) => true, // int → float widening
            (BondType.Double, Default.Float or Default.Integer) => true, // int → double widening
            (BondType.Bool,   Default.Bool) => true,
            (BondType.String, Default.String) => true,
            (BondType.WString, Default.String) => true,
            (BondType.TypeReference { Declaration: EnumDeclaration e }, Default.Enum d) => e.Constants.Any(c => c.Name == d.Identifier),
            (BondType.TypeReference { Declaration: EnumDeclaration }, Default.Nothing) => true,
            (BondType.TypeParameter, _) => true,
            _ => false
        };
    }
}
