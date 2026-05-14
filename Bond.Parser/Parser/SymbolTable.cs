using System;
using System.Collections.Generic;
using System.Linq;
using Bond.Parser.Syntax;

namespace Bond.Parser.Parser;

/// <summary>
/// Holds globally-visible declarations (structs, enums, services, forward decls)
/// across the current file and its transitive imports, plus a set of import paths
/// already processed. File-scoped declarations like aliases live outside this table —
/// SemanticAnalyzer owns them per-file and passes them into FindSymbol.
/// </summary>
public class SymbolTable
{
    private readonly List<Declaration> _globalDeclarations = [];
    private readonly HashSet<string> _processedImports = [];

    /// <summary>
    /// Adds a declaration. Throws on a name collision unless the existing entry
    /// is a forward declaration that the new one closes (or vice versa).
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
                throw new InvalidOperationException($"Duplicate declaration: {declaration.Kind} '{declaration.Name}' was already declared as {duplicate.Kind}");
            }
        }

        _globalDeclarations.Add(declaration);
    }

    /// <summary>
    /// Looks up a declaration by qualified name. Per-file aliases are searched
    /// first; then the global table.
    /// </summary>
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

    /// <summary>
    /// Records that an import path has been processed. Returns true if newly
    /// claimed, false if a previous call already claimed it (cycle / diamond).
    /// </summary>
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
