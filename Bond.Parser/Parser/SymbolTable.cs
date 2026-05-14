using System.Collections.Generic;
using System.Linq;
using Bond.Parser.Syntax;

namespace Bond.Parser.Parser;

/// <summary>
/// Globally-visible declarations across this file and its transitive imports,
/// plus the set of import paths already processed. File-scoped aliases live
/// outside this table — SemanticAnalyzer owns them per-file.
/// </summary>
public class SymbolTable
{
    private readonly List<Declaration> _globalDeclarations = [];
    private readonly HashSet<string> _processedImports = [];

    /// <summary>
    /// Throws on name collision unless the existing entry is a forward declaration
    /// that the new one closes (or vice versa).
    /// </summary>
    public void AddDeclaration(Declaration declaration)
    {
        var duplicates = _globalDeclarations
            .Where(d => d.Name == declaration.Name && d.Namespaces.Any(ns1 => declaration.Namespaces.Any(ns1.Matches)))
            .ToList();

        foreach (var duplicate in duplicates)
        {
            if (!TryReconcile(duplicate, declaration))
            {
                throw new SemanticErrorException(
                    $"Duplicate declaration: {declaration.Kind} '{declaration.Name}' was already declared as {duplicate.Kind}",
                    declaration.Location);
            }
        }

        _globalDeclarations.Add(declaration);
    }

    /// <summary>Aliases first, then the global table.</summary>
    public Declaration? FindSymbol(string[] qualifiedName, Namespace[] currentNamespaces, IReadOnlyList<AliasDeclaration> aliases)
    {
        var alias = FindAlias(qualifiedName, currentNamespaces, aliases);
        if (alias != null) return alias;

        if (qualifiedName.Length == 1)
        {
            return _globalDeclarations.FirstOrDefault(d =>
                d.Name == qualifiedName[0] &&
                d.Namespaces.Any(ns1 => currentNamespaces.Any(ns1.Matches)));
        }

        var namespacePart = qualifiedName[..^1];
        var namePart = qualifiedName[^1];
        return _globalDeclarations.FirstOrDefault(d =>
            d.Name == namePart &&
            d.Namespaces.Any(ns => ns.Name.SequenceEqual(namespacePart)));
    }

    /// <summary>Returns true on first claim, false on cycle / diamond import.</summary>
    public bool ClaimImport(string canonicalPath) => _processedImports.Add(canonicalPath);

    private static AliasDeclaration? FindAlias(string[] qualifiedName, Namespace[] currentNamespaces, IReadOnlyList<AliasDeclaration> aliases)
    {
        if (qualifiedName.Length == 1)
        {
            return aliases.FirstOrDefault(a =>
                a.Name == qualifiedName[0] &&
                a.Namespaces.Any(ns1 => currentNamespaces.Any(ns1.Matches)));
        }

        var namespacePart = qualifiedName[..^1];
        var namePart = qualifiedName[^1];
        return aliases.FirstOrDefault(a =>
            a.Name == namePart &&
            a.Namespaces.Any(ns => ns.Name.SequenceEqual(namespacePart)));
    }

    // Forward + struct (in either order) reconciles when the type-parameter shape
    // matches. Identical re-declarations also pass — happens when the same import
    // is seen via multiple paths.
    private static bool TryReconcile(Declaration existing, Declaration newDeclaration)
    {
        if (existing is ForwardDeclaration forward && newDeclaration is StructDeclaration)
            return ParametersMatch(forward.TypeParameters, newDeclaration.TypeParameters);

        if (existing is StructDeclaration && newDeclaration is ForwardDeclaration forward2)
            return ParametersMatch(existing.TypeParameters, forward2.TypeParameters);

        return existing.Equals(newDeclaration);
    }

    private static bool ParametersMatch(TypeParam[] a, TypeParam[] b)
    {
        if (a.Length != b.Length) return false;
        for (int i = 0; i < a.Length; i++)
        {
            if (a[i].Constraint != b[i].Constraint) return false;
        }
        return true;
    }
}
