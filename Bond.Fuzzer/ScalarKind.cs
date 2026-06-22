using Bond.Parser.Syntax;

namespace Bond.Fuzzer;

internal enum ScalarKind
{
    Bool,
    Int8,
    Int16,
    Int32,
    Int64,
    UInt8,
    UInt16,
    UInt32,
    UInt64,
    Float,
    Double,
    String
}

internal static class ScalarKindExtensions
{
    public static BondType ToBondType(this ScalarKind kind) =>
        kind switch
        {
            ScalarKind.Bool => BondType.Bool.Instance,
            ScalarKind.Int8 => BondType.Int8.Instance,
            ScalarKind.Int16 => BondType.Int16.Instance,
            ScalarKind.Int32 => BondType.Int32.Instance,
            ScalarKind.Int64 => BondType.Int64.Instance,
            ScalarKind.UInt8 => BondType.UInt8.Instance,
            ScalarKind.UInt16 => BondType.UInt16.Instance,
            ScalarKind.UInt32 => BondType.UInt32.Instance,
            ScalarKind.UInt64 => BondType.UInt64.Instance,
            ScalarKind.Float => BondType.Float.Instance,
            ScalarKind.Double => BondType.Double.Instance,
            ScalarKind.String => BondType.String.Instance,
            _ => throw new InvalidOperationException($"Unsupported scalar kind: {kind}")
        };

    public static string ToBondTypeName(this ScalarKind kind) => kind.ToBondType().ToString();

    public static bool TryGetScalarKind(this BondType type, out ScalarKind kind)
    {
        kind = type switch
        {
            BondType.Bool => ScalarKind.Bool,
            BondType.Int8 => ScalarKind.Int8,
            BondType.Int16 => ScalarKind.Int16,
            BondType.Int32 => ScalarKind.Int32,
            BondType.Int64 => ScalarKind.Int64,
            BondType.UInt8 => ScalarKind.UInt8,
            BondType.UInt16 => ScalarKind.UInt16,
            BondType.UInt32 => ScalarKind.UInt32,
            BondType.UInt64 => ScalarKind.UInt64,
            BondType.Float => ScalarKind.Float,
            BondType.Double => ScalarKind.Double,
            BondType.String => ScalarKind.String,
            _ => default
        };

        return type is BondType.Bool
            or BondType.Int8
            or BondType.Int16
            or BondType.Int32
            or BondType.Int64
            or BondType.UInt8
            or BondType.UInt16
            or BondType.UInt32
            or BondType.UInt64
            or BondType.Float
            or BondType.Double
            or BondType.String;
    }
}
