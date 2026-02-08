using System.Linq;

namespace Bond.Parser.Syntax;

/// <summary>
/// Represents all possible Bond types
/// </summary>
public abstract record BondType
{
    private BondType() { }

    // Primitive types
    public sealed record Int8 : BondType
    {
        public static readonly Int8 Instance = new();
        private Int8() { }
        public override string ToString() => "int8";
    }

    public sealed record Int16 : BondType
    {
        public static readonly Int16 Instance = new();
        private Int16() { }
        public override string ToString() => "int16";
    }

    public sealed record Int32 : BondType
    {
        public static readonly Int32 Instance = new();
        private Int32() { }
        public override string ToString() => "int32";
    }

    public sealed record Int64 : BondType
    {
        public static readonly Int64 Instance = new();
        private Int64() { }
        public override string ToString() => "int64";
    }

    public sealed record UInt8 : BondType
    {
        public static readonly UInt8 Instance = new();
        private UInt8() { }
        public override string ToString() => "uint8";
    }

    public sealed record UInt16 : BondType
    {
        public static readonly UInt16 Instance = new();
        private UInt16() { }
        public override string ToString() => "uint16";
    }

    public sealed record UInt32 : BondType
    {
        public static readonly UInt32 Instance = new();
        private UInt32() { }
        public override string ToString() => "uint32";
    }

    public sealed record UInt64 : BondType
    {
        public static readonly UInt64 Instance = new();
        private UInt64() { }
        public override string ToString() => "uint64";
    }

    public sealed record Float : BondType
    {
        public static readonly Float Instance = new();
        private Float() { }
        public override string ToString() => "float";
    }

    public sealed record Double : BondType
    {
        public static readonly Double Instance = new();
        private Double() { }
        public override string ToString() => "double";
    }

    public sealed record String : BondType
    {
        public static readonly String Instance = new();
        private String() { }
        public override string ToString() => "string";
    }

    public sealed record WString : BondType
    {
        public static readonly WString Instance = new();
        private WString() { }
        public override string ToString() => "wstring";
    }

    public sealed record Bool : BondType
    {
        public static readonly Bool Instance = new();
        private Bool() { }
        public override string ToString() => "bool";
    }

    // Container types
    public sealed record List(BondType ElementType) : BondType
    {
        public override string ToString() => $"list<{ElementType}>";
    }

    public sealed record Vector(BondType ElementType) : BondType
    {
        public override string ToString() => $"vector<{ElementType}>";
    }

    public sealed record Set(BondType KeyType) : BondType
    {
        public override string ToString() => $"set<{KeyType}>";
    }

    public sealed record Map(BondType KeyType, BondType ValueType) : BondType
    {
        public override string ToString() => $"map<{KeyType}, {ValueType}>";
    }

    public sealed record Nullable(BondType ElementType) : BondType
    {
        public override string ToString() => $"nullable<{ElementType}>";
    }

    public sealed record Blob : BondType
    {
        public static readonly Blob Instance = new();
        private Blob() { }
        public override string ToString() => "blob";
    }

    public sealed record Bonded(BondType StructType) : BondType
    {
        public override string ToString() => $"bonded<{StructType}>";
    }

    // Meta types
    public sealed record MetaName : BondType
    {
        public static readonly MetaName Instance = new();
        private MetaName() { }
        public override string ToString() => "bond_meta::name";
    }

    public sealed record MetaFullName : BondType
    {
        public static readonly MetaFullName Instance = new();
        private MetaFullName() { }
        public override string ToString() => "bond_meta::full_name";
    }

    // User-defined types
    public sealed record UserDefined(Declaration Declaration, BondType[] TypeArguments) : BondType
    {
        public override string ToString()
        {
            var name = Declaration.Name;
            if (TypeArguments.Length > 0)
            {
                return $"{name}<{string.Join(", ", TypeArguments.Select(t => t.ToString()))}>";
            }
            return name;
        }
    }

    public sealed record TypeParameter(TypeParam Param) : BondType
    {
        public override string ToString() => Param.Name;
    }

    // Maybe wrapper (created when default is 'nothing')
    public sealed record Maybe(BondType ElementType) : BondType
    {
        public override string ToString() => $"nullable<{ElementType}>";
    }

    // Integer type argument for generic instantiation
    public sealed record IntTypeArg(long Value) : BondType
    {
        public override string ToString() => Value.ToString();
    }

    // Unresolved user-defined type (resolved by semantic analyzer)
    public sealed record UnresolvedUserType(string[] QualifiedName, BondType[] TypeArguments) : BondType
    {
        public override string ToString()
        {
            var name = string.Join(".", QualifiedName);
            if (TypeArguments.Length > 0)
            {
                return $"{name}<{string.Join(", ", TypeArguments.Select(t => t.ToString()))}>";
            }
            return name;
        }
    }
}

/// <summary>
/// Helper methods for type classification
/// </summary>
public static class BondTypeExtensions
{
    public static bool IsScalar(this BondType type) =>
        type is BondType.Int8 or BondType.Int16 or BondType.Int32 or BondType.Int64
            or BondType.UInt8 or BondType.UInt16 or BondType.UInt32 or BondType.UInt64
            or BondType.Float or BondType.Double or BondType.Bool;

    public static bool IsString(this BondType type) =>
        type is BondType.String or BondType.WString;

    public static bool IsEnum(this BondType type) =>
        type is BondType.UserDefined { Declaration: EnumDeclaration };

    public static bool IsStruct(this BondType type) =>
        type is BondType.UserDefined { Declaration: StructDeclaration or ForwardDeclaration };

    public static bool IsValidKeyType(this BondType type) =>
        type.IsScalar() || type.IsString() || type.IsEnum() || type is BondType.TypeParameter;
}
