using System;
using System.Collections.Generic;
using System.Linq;
using Bond.Parser.Syntax;

namespace Bond.Parser.Parser;

/// <summary>
/// Walks an AST and replaces every UnresolvedType with a TypeReference wrapper
/// around the resolved Declaration. Operates against a populated SymbolTable plus
/// the current file's alias list.
/// </summary>
public static class TypeResolver
{
    private readonly record struct Context(
        SymbolTable Symbols,
        IReadOnlyList<AliasDeclaration> Aliases,
        Namespace[] Namespaces);

    public static Syntax.Bond Resolve(Syntax.Bond ast, SymbolTable symbols, IReadOnlyList<AliasDeclaration> aliases)
    {
        var ctx = new Context(symbols, aliases, ast.Namespaces);
        var resolved = ast.Declarations.Select(d => ResolveDeclaration(d, ctx)).ToArray();
        return ast with { Declarations = resolved };
    }

    private static Declaration ResolveDeclaration(Declaration declaration, Context ctx) =>
        declaration switch
        {
            StructDeclaration s => ResolveStruct(s, ctx),
            AliasDeclaration a => ResolveAlias(a, ctx),
            ServiceDeclaration s => ResolveService(s, ctx),
            _ => declaration
        };

    private static StructDeclaration ResolveStruct(StructDeclaration structDecl, Context ctx) =>
        structDecl with
        {
            Fields = structDecl.Fields.Select(f => ResolveField(f, ctx, structDecl)).ToArray(),
            BaseType = structDecl.BaseType is null ? null : ResolveType(structDecl.BaseType, ctx, currentStruct: null, structDecl.Location)
        };

    private static AliasDeclaration ResolveAlias(AliasDeclaration aliasDecl, Context ctx) =>
        aliasDecl with { AliasedType = ResolveType(aliasDecl.AliasedType, ctx, currentStruct: null, aliasDecl.Location) };

    private static ServiceDeclaration ResolveService(ServiceDeclaration serviceDecl, Context ctx) =>
        serviceDecl with
        {
            Methods = serviceDecl.Methods.Select(m => ResolveMethod(m, ctx)).ToArray(),
            BaseType = serviceDecl.BaseType is null ? null : ResolveType(serviceDecl.BaseType, ctx, currentStruct: null, serviceDecl.Location)
        };

    private static Field ResolveField(Field field, Context ctx, StructDeclaration currentStruct) =>
        field with { Type = ResolveType(field.Type, ctx, currentStruct, field.Location) };

    private static Method ResolveMethod(Method method, Context ctx) => method switch
    {
        FunctionMethod f => f with { InputType = ResolveMethodType(f.InputType, ctx), ResultType = ResolveMethodType(f.ResultType, ctx) },
        EventMethod e => e with { InputType = ResolveMethodType(e.InputType, ctx) },
        _ => method
    };

    private static MethodType ResolveMethodType(MethodType methodType, Context ctx) => methodType switch
    {
        MethodType.Unary u => new MethodType.Unary(ResolveType(u.Type, ctx, currentStruct: null, default)),
        MethodType.Streaming s => new MethodType.Streaming(ResolveType(s.Type, ctx, currentStruct: null, default)),
        _ => methodType
    };

    private static BondType ResolveType(BondType type, Context ctx, StructDeclaration? currentStruct, SourceLocation callerLocation) => type switch
    {
        BondType.Int8 or BondType.Int16 or BondType.Int32 or BondType.Int64
            or BondType.UInt8 or BondType.UInt16 or BondType.UInt32 or BondType.UInt64
            or BondType.Float or BondType.Double or BondType.Bool
            or BondType.String or BondType.WString or BondType.Blob
            or BondType.MetaName or BondType.MetaFullName
            or BondType.TypeParameter or BondType.IntTypeArg
            => type,

        BondType.List list => new BondType.List(ResolveType(list.ElementType, ctx, currentStruct, callerLocation)),
        BondType.Vector vector => new BondType.Vector(ResolveType(vector.ElementType, ctx, currentStruct, callerLocation)),
        BondType.Set set => new BondType.Set(ResolveType(set.KeyType, ctx, currentStruct, callerLocation)),
        BondType.Map map => new BondType.Map(
            ResolveType(map.KeyType, ctx, currentStruct, callerLocation),
            ResolveType(map.ValueType, ctx, currentStruct, callerLocation)),
        BondType.Nullable n => new BondType.Nullable(ResolveType(n.ElementType, ctx, currentStruct, callerLocation)),
        BondType.Maybe m => new BondType.Maybe(ResolveType(m.ElementType, ctx, currentStruct, callerLocation)),
        BondType.Bonded b => new BondType.Bonded(ResolveType(b.StructType, ctx, currentStruct, callerLocation)),

        BondType.UnresolvedType u => ResolveUnresolvedType(u, ctx, currentStruct, callerLocation),
        BondType.TypeReference u => ResolveTypeReference(u, ctx, currentStruct),

        _ => throw new InvalidOperationException($"Unknown BondType: {type.GetType().Name}")
    };

    private static BondType ResolveUnresolvedType(BondType.UnresolvedType unresolved, Context ctx, StructDeclaration? currentStruct, SourceLocation callerLocation)
    {
        var declaration = ctx.Symbols.FindSymbol(unresolved.QualifiedName, ctx.Namespaces, ctx.Aliases);

        if (declaration is null && unresolved.TypeArguments.Length == 0 && TryResolvePrimitive(unresolved.QualifiedName, out var primitive))
        {
            return primitive;
        }

        if (declaration is null)
        {
            throw new SemanticErrorException(
                $"Type '{string.Join(".", unresolved.QualifiedName)}' not found in symbol table",
                callerLocation);
        }

        var resolvedTypeArgs = unresolved.TypeArguments
            .Select(arg => ResolveType(arg, ctx, currentStruct, callerLocation))
            .ToArray();

        // Self-references become forward declarations to break the recursion.
        if (currentStruct is not null && declaration is StructDeclaration s && IsSameDeclaration(s, currentStruct))
        {
            return new BondType.TypeReference(ToForward(s), resolvedTypeArgs);
        }

        // Resolve the alias body before wrapping.
        if (declaration is AliasDeclaration alias)
        {
            return new BondType.TypeReference(ResolveAlias(alias, ctx), resolvedTypeArgs);
        }

        return new BondType.TypeReference(declaration, resolvedTypeArgs);
    }

    private static BondType ResolveTypeReference(BondType.TypeReference typeReference, Context ctx, StructDeclaration? currentStruct)
    {
        var qualifiedName = typeReference.Declaration.Namespaces.Length > 0
            ? typeReference.Declaration.Namespaces[0].Name.Concat([typeReference.Declaration.Name]).ToArray()
            : [typeReference.Declaration.Name];

        var declaration = ctx.Symbols.FindSymbol(qualifiedName, ctx.Namespaces, ctx.Aliases) ?? typeReference.Declaration;

        var resolvedTypeArgs = typeReference.TypeArguments
            .Select(arg => ResolveType(arg, ctx, currentStruct, default))
            .ToArray();

        if (currentStruct is not null && declaration is StructDeclaration s && IsSameDeclaration(s, currentStruct))
        {
            return new BondType.TypeReference(ToForward(s), resolvedTypeArgs);
        }

        if (declaration is AliasDeclaration alias)
        {
            return new BondType.TypeReference(ResolveAlias(alias, ctx), resolvedTypeArgs);
        }

        if (ReferenceEquals(declaration, typeReference.Declaration) && resolvedTypeArgs.SequenceEqual(typeReference.TypeArguments))
        {
            return typeReference;
        }

        return new BondType.TypeReference(declaration, resolvedTypeArgs);
    }

    private static ForwardDeclaration ToForward(StructDeclaration s) => new()
    {
        Namespaces = s.Namespaces,
        Name = s.Name,
        TypeParameters = s.TypeParameters
    };

    private static bool IsSameDeclaration(StructDeclaration declaration, StructDeclaration currentStruct)
    {
        if (!string.Equals(declaration.Name, currentStruct.Name, StringComparison.Ordinal)) return false;
        return declaration.Namespaces.Any(n => currentStruct.Namespaces.Any(n.Matches));
    }

    private static bool TryResolvePrimitive(string[] qualifiedName, out BondType primitive)
    {
        primitive = null!;
        if (qualifiedName.Length != 1) return false;

        switch (qualifiedName[0].ToLowerInvariant())
        {
            case "int8":    primitive = BondType.Int8.Instance;    return true;
            case "int16":   primitive = BondType.Int16.Instance;   return true;
            case "int32":   primitive = BondType.Int32.Instance;   return true;
            case "int64":   primitive = BondType.Int64.Instance;   return true;
            case "uint8":   primitive = BondType.UInt8.Instance;   return true;
            case "uint16":  primitive = BondType.UInt16.Instance;  return true;
            case "uint32":  primitive = BondType.UInt32.Instance;  return true;
            case "uint64":  primitive = BondType.UInt64.Instance;  return true;
            case "float":   primitive = BondType.Float.Instance;   return true;
            case "double":  primitive = BondType.Double.Instance;  return true;
            case "bool":    primitive = BondType.Bool.Instance;    return true;
            case "string":  primitive = BondType.String.Instance;  return true;
            case "wstring": primitive = BondType.WString.Instance; return true;
            case "blob":    primitive = BondType.Blob.Instance;    return true;
            default: return false;
        }
    }
}
