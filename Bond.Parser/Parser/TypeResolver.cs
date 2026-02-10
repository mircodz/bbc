using System;
using System.Linq;
using Bond.Parser.Syntax;

namespace Bond.Parser.Parser;

/// <summary>
/// Resolves UnresolvedUserType references to concrete UserDefined types
/// Returns a new AST with all types resolved
/// </summary>
public class TypeResolver(SymbolTable symbolTable)
{
    /// <summary>
    /// Resolves all types in the AST, returning a new Bond AST with resolved types
    /// Multi-pass resolution to handle aliases that reference other aliases
    /// </summary>
    public Syntax.Bond ResolveTypes(Syntax.Bond ast)
    {
        var currentAst = ast;
        const int maxPasses = 10; // Prevent infinite loops

        // Preserve declarations that came from imports so we can re-add them each pass
        var importedDeclarations = symbolTable.Declarations
            .Where(d => !ast.Declarations.Contains(d))
            .ToArray();

        for (int pass = 0; pass < maxPasses; pass++)
        {
            // Update symbol table with current declarations
            symbolTable.Clear();
            foreach (var importDecl in importedDeclarations)
            {
                symbolTable.AddDeclaration(importDecl, currentAst.Namespaces);
            }
            foreach (var decl in currentAst.Declarations)
            {
                symbolTable.AddDeclaration(decl, currentAst.Namespaces);
            }

            // Resolve all declarations
            var resolvedDeclarations = currentAst.Declarations
                .Select(decl => ResolveDeclaration(decl, currentAst.Namespaces))
                .ToArray();

            var newAst = currentAst with { Declarations = resolvedDeclarations };

            // Check if anything changed
            if (DeclarationsEqual(currentAst.Declarations, newAst.Declarations))
            {
                return newAst; // No more changes, we're done
            }

            currentAst = newAst;
        }

        // If we hit max passes, still return what we have
        return currentAst;
    }

    private static bool DeclarationsEqual(Declaration[] a, Declaration[] b)
    {
        if (a.Length != b.Length) return false;

        for (int i = 0; i < a.Length; i++)
        {
            // Simple reference equality check - records will be different if types changed
            if (!ReferenceEquals(a[i], b[i]))
                return false;
        }

        return true;
    }

    private Declaration ResolveDeclaration(Declaration declaration, Namespace[] namespaces)
    {
        return declaration switch
        {
            StructDeclaration structDecl => ResolveStruct(structDecl, namespaces),
            AliasDeclaration aliasDecl => ResolveAlias(aliasDecl, namespaces),
            ServiceDeclaration serviceDecl => ResolveService(serviceDecl, namespaces),
            EnumDeclaration enumDecl => enumDecl, // Enums don't have types to resolve
            ForwardDeclaration fwdDecl => fwdDecl, // Forward decls don't have types
            _ => declaration
        };
    }

    private StructDeclaration ResolveStruct(StructDeclaration structDecl, Namespace[] namespaces)
    {
        var resolvedFields = structDecl.Fields
            .Select(field => ResolveField(field, namespaces, structDecl))
            .ToArray();

        var resolvedBase = structDecl.BaseType != null
            ? ResolveType(structDecl.BaseType, namespaces, structDecl)
            : null;

        return structDecl with
        {
            Fields = resolvedFields,
            BaseType = resolvedBase
        };
    }

    private AliasDeclaration ResolveAlias(AliasDeclaration aliasDecl, Namespace[] namespaces)
    {
        var resolvedType = ResolveType(aliasDecl.AliasedType, namespaces);
        return aliasDecl with { AliasedType = resolvedType };
    }

    private ServiceDeclaration ResolveService(ServiceDeclaration serviceDecl, Namespace[] namespaces)
    {
        var resolvedMethods = serviceDecl.Methods
            .Select(method => ResolveMethod(method, namespaces))
            .ToArray();

        var resolvedBase = serviceDecl.BaseType != null
            ? ResolveType(serviceDecl.BaseType, namespaces)
            : null;

        return serviceDecl with
        {
            Methods = resolvedMethods,
            BaseType = resolvedBase
        };
    }

    private Field ResolveField(Field field, Namespace[] namespaces, StructDeclaration? currentStruct = null)
    {
        var resolvedType = ResolveType(field.Type, namespaces, currentStruct);
        return field with { Type = resolvedType };
    }

    private Method ResolveMethod(Method method, Namespace[] namespaces)
    {
        return method switch
        {
            FunctionMethod func => func with
            {
                InputType = ResolveMethodType(func.InputType, namespaces),
                ResultType = ResolveMethodType(func.ResultType, namespaces)
            },
            EventMethod evt => evt with
            {
                InputType = ResolveMethodType(evt.InputType, namespaces)
            },
            _ => method
        };
    }

    private MethodType ResolveMethodType(MethodType methodType, Namespace[] namespaces)
    {
        return methodType switch
        {
            MethodType.Unary unary => new MethodType.Unary(ResolveType(unary.Type, namespaces)),
            MethodType.Streaming streaming => new MethodType.Streaming(ResolveType(streaming.Type, namespaces)),
            _ => methodType
        };
    }

    /// <summary>
    /// Recursively resolves a BondType, handling nested container types
    /// </summary>
    private BondType ResolveType(BondType type, Namespace[] namespaces, StructDeclaration? currentStruct = null)
    {
        return type switch
        {
            // Primitives - no resolution needed
            BondType.Int8 or BondType.Int16 or BondType.Int32 or BondType.Int64
                or BondType.UInt8 or BondType.UInt16 or BondType.UInt32 or BondType.UInt64
                or BondType.Float or BondType.Double or BondType.Bool
                or BondType.String or BondType.WString or BondType.Blob
                or BondType.MetaName or BondType.MetaFullName
                or BondType.TypeParameter or BondType.IntTypeArg
                => type,

            // Container types - resolve element types recursively
            BondType.List list => new BondType.List(
                ResolveType(list.ElementType, namespaces, currentStruct)),

            BondType.Vector vector => new BondType.Vector(
                ResolveType(vector.ElementType, namespaces, currentStruct)),

            BondType.Set set => new BondType.Set(
                ResolveType(set.KeyType, namespaces, currentStruct)),

            BondType.Map map => new BondType.Map(
                ResolveType(map.KeyType, namespaces, currentStruct),
                ResolveType(map.ValueType, namespaces, currentStruct)),

            BondType.Nullable nullable => new BondType.Nullable(
                ResolveType(nullable.ElementType, namespaces, currentStruct)),

            BondType.Maybe maybe => new BondType.Maybe(
                ResolveType(maybe.ElementType, namespaces, currentStruct)),

            BondType.Bonded bonded => new BondType.Bonded(
                ResolveType(bonded.StructType, namespaces, currentStruct)),

            // This is what we're here for - resolve unresolved types!
            BondType.UnresolvedUserType unresolved =>
                ResolveUnresolvedType(unresolved, namespaces, currentStruct),

            // Already resolved, but type arguments might need resolution
            BondType.UserDefined userDefined =>
                ResolveUserDefinedType(userDefined, namespaces, currentStruct),

            _ => throw new InvalidOperationException($"Unknown BondType: {type.GetType().Name}")
        };
    }

    private BondType ResolveUnresolvedType(BondType.UnresolvedUserType unresolved, Namespace[] namespaces, StructDeclaration? currentStruct)
    {
        // Look up the declaration in the symbol table
        var declaration = symbolTable.FindSymbol(unresolved.QualifiedName, namespaces);

        // Gracefully accept primitive types with different casing (e.g., "String")
        if (declaration == null && unresolved.TypeArguments.Length == 0)
        {
            if (TryResolvePrimitive(unresolved.QualifiedName, out var primitive))
            {
                return primitive;
            }
        }

        if (declaration == null)
        {
            throw new InvalidOperationException(
                $"Type '{string.Join(".", unresolved.QualifiedName)}' not found in symbol table");
        }

        // Resolve type arguments recursively
        var resolvedTypeArgs = unresolved.TypeArguments
            .Select(arg => ResolveType(arg, namespaces, currentStruct))
            .ToArray();

        // If this is a self-reference, emit a forward declaration to prevent infinite nesting
        if (currentStruct != null &&
            declaration is StructDeclaration structDecl &&
            IsSameDeclaration(structDecl, currentStruct))
        {
            var forward = new ForwardDeclaration
            {
                Namespaces = structDecl.Namespaces,
                Name = structDecl.Name,
                TypeParameters = structDecl.TypeParameters
            };

            return new BondType.UserDefined(forward, resolvedTypeArgs);
        }

        // Special handling for aliases - we need to resolve the alias declaration itself first
        if (declaration is AliasDeclaration alias)
        {
            // Resolve the aliased type
            var resolvedAlias = ResolveAlias(alias, namespaces);

            // Now create UserDefined with the resolved alias
            return new BondType.UserDefined(resolvedAlias, resolvedTypeArgs);
        }

        // Create UserDefined type with resolved declaration and type arguments
        return new BondType.UserDefined(declaration, resolvedTypeArgs);
    }

    private static bool TryResolvePrimitive(string[] qualifiedName, out BondType primitive)
    {
        primitive = null!;
        if (qualifiedName.Length != 1)
        {
            return false;
        }

        switch (qualifiedName[0].ToLowerInvariant())
        {
            case "int8": primitive = BondType.Int8.Instance; return true;
            case "int16": primitive = BondType.Int16.Instance; return true;
            case "int32": primitive = BondType.Int32.Instance; return true;
            case "int64": primitive = BondType.Int64.Instance; return true;
            case "uint8": primitive = BondType.UInt8.Instance; return true;
            case "uint16": primitive = BondType.UInt16.Instance; return true;
            case "uint32": primitive = BondType.UInt32.Instance; return true;
            case "uint64": primitive = BondType.UInt64.Instance; return true;
            case "float": primitive = BondType.Float.Instance; return true;
            case "double": primitive = BondType.Double.Instance; return true;
            case "bool": primitive = BondType.Bool.Instance; return true;
            case "string": primitive = BondType.String.Instance; return true;
            case "wstring": primitive = BondType.WString.Instance; return true;
            case "blob": primitive = BondType.Blob.Instance; return true;
            default: return false;
        }
    }

    private BondType ResolveUserDefinedType(BondType.UserDefined userDefined, Namespace[] namespaces, StructDeclaration? currentStruct)
    {
        // Look up the latest version of the declaration from the symbol table
        // This is important for self-referential types and multi-pass resolution
        var qualifiedName = userDefined.Declaration.Namespaces.Length > 0
            ? userDefined.Declaration.Namespaces[0].Name.Concat([userDefined.Declaration.Name]).ToArray()
            : [userDefined.Declaration.Name];

        var latestDeclaration = symbolTable.FindSymbol(qualifiedName, namespaces);

        // If not found in symbol table (shouldn't happen), use the existing declaration
        var declaration = latestDeclaration ?? userDefined.Declaration;

        // Resolve type arguments
        var resolvedTypeArgs = userDefined.TypeArguments
            .Select(arg => ResolveType(arg, namespaces, currentStruct))
            .ToArray();

        // Preserve forward declarations for self references
        if (currentStruct != null &&
            declaration is StructDeclaration structDecl &&
            IsSameDeclaration(structDecl, currentStruct))
        {
            var forward = new ForwardDeclaration
            {
                Namespaces = structDecl.Namespaces,
                Name = structDecl.Name,
                TypeParameters = structDecl.TypeParameters
            };
            return new BondType.UserDefined(forward, resolvedTypeArgs);
        }

        // Special handling for aliases - resolve through them
        if (declaration is AliasDeclaration alias)
        {
            var resolvedAlias = ResolveAlias(alias, namespaces);
            return new BondType.UserDefined(resolvedAlias, resolvedTypeArgs);
        }

        // If nothing changed, return the original
        if (ReferenceEquals(declaration, userDefined.Declaration) &&
            resolvedTypeArgs.SequenceEqual(userDefined.TypeArguments))
        {
            return userDefined;
        }

        // Return new UserDefined with latest declaration and resolved type arguments
        return new BondType.UserDefined(declaration, resolvedTypeArgs);
    }

    private static bool IsSameDeclaration(StructDeclaration declaration, StructDeclaration currentStruct)
    {
        if (!string.Equals(declaration.Name, currentStruct.Name, StringComparison.Ordinal))
        {
            return false;
        }

        return declaration.Namespaces.Any(ns1 =>
            currentStruct.Namespaces.Any(ns2 => NamespacesMatch(ns1, ns2)));
    }

    private static bool NamespacesMatch(Namespace ns1, Namespace ns2)
    {
        if (!ns1.Name.SequenceEqual(ns2.Name))
        {
            return false;
        }

        if (ns1.LanguageQualifier.HasValue && ns2.LanguageQualifier.HasValue)
        {
            return ns1.LanguageQualifier == ns2.LanguageQualifier;
        }

        return true;
    }
}
