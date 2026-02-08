using System;
using System.Collections.Generic;
using System.Linq;
using Bond.Parser.Syntax;

namespace Bond.Parser.Parser;

/// <summary>
/// Tracks all declarations across files for symbol resolution
/// </summary>
public class SymbolTable
{
    private readonly List<Declaration> _declarations = [];
    private readonly HashSet<string> _processedImports = [];

    /// <summary>
    /// Adds a declaration to the symbol table with duplicate checking
    /// </summary>
    public void AddDeclaration(Declaration declaration, Namespace[] currentNamespaces)
    {
        // Find duplicates in the same namespace
        var duplicates = _declarations
            .Where(d => d.Name == declaration.Name && d.Namespaces.Any(ns1 => currentNamespaces.Any(ns2 => NamespacesMatch(ns1, ns2))))
            .ToList();

        foreach (var duplicate in duplicates)
        {
            // Try to reconcile forward declarations with definitions
            if (!TryReconcile(duplicate, declaration))
            {
                throw new InvalidOperationException(
                    $"Duplicate declaration: {declaration.Kind} '{declaration.Name}' was already declared as {duplicate.Kind}");
            }
        }

        _declarations.Add(declaration);
    }

    /// <summary>
    /// Finds a symbol by qualified name in the given namespaces
    /// </summary>
    public Declaration? FindSymbol(string[] qualifiedName, Namespace[] currentNamespaces)
    {
        if (qualifiedName.Length == 1)
        {
            // Unqualified name - search in current namespaces
            return _declarations.FirstOrDefault(d =>
                d.Name == qualifiedName[0] &&
                d.Namespaces.Any(ns1 => currentNamespaces.Any(ns2 => NamespacesMatch(ns1, ns2))));
        }
        else
        {
            // Qualified name - match namespace and name
            var namespacePart = qualifiedName[..^1];
            var namePart = qualifiedName[^1];

            return _declarations.FirstOrDefault(d =>
                d.Name == namePart &&
                d.Namespaces.Any(ns => ns.Name.SequenceEqual(namespacePart)));
        }
    }

    /// <summary>
    /// Finds a struct declaration
    /// </summary>
    public StructDeclaration? FindStruct(string[] qualifiedName, Namespace[] currentNamespaces)
    {
        var symbol = FindSymbol(qualifiedName, currentNamespaces);
        return symbol as StructDeclaration;
    }

    /// <summary>
    /// Checks if an import has been processed
    /// </summary>
    public bool IsImportProcessed(string canonicalPath)
    {
        return _processedImports.Contains(canonicalPath);
    }

    /// <summary>
    /// Marks an import as processed
    /// </summary>
    public void MarkImportProcessed(string canonicalPath)
    {
        _processedImports.Add(canonicalPath);
    }

    /// <summary>
    /// Gets all declarations
    /// </summary>
    public IReadOnlyList<Declaration> Declarations => _declarations.AsReadOnly();

    /// <summary>
    /// Clears all declarations from the symbol table
    /// </summary>
    public void Clear()
    {
        _declarations.Clear();
        // Don't clear processed imports as those are still valid
    }

    private static bool NamespacesMatch(Namespace ns1, Namespace ns2)
    {
        if (!ns1.Name.SequenceEqual(ns2.Name))
        {
            return false;
        }

        // If both specify a language, they must match
        if (ns1.LanguageQualifier.HasValue && ns2.LanguageQualifier.HasValue)
        {
            return ns1.LanguageQualifier == ns2.LanguageQualifier;
        }

        // If only one specifies a language, they still match (language-agnostic)
        return true;
    }

    private static bool TryReconcile(Declaration existing, Declaration newDeclaration)
    {
        // Forward declaration can be reconciled with struct definition
        if (existing is ForwardDeclaration forward && newDeclaration is StructDeclaration)
        {
            return ParametersMatch(forward.TypeParameters, newDeclaration.TypeParameters);
        }

        if (existing is StructDeclaration && newDeclaration is ForwardDeclaration forward2)
        {
            return ParametersMatch(existing.TypeParameters, forward2.TypeParameters);
        }

        // Allow identical duplicate definitions (happens when parsing same import multiple times)
        return existing.Equals(newDeclaration);
    }

    private static bool ParametersMatch(TypeParam[] params1, TypeParam[] params2)
    {
        if (params1.Length != params2.Length)
        {
            return false;
        }

        for (int i = 0; i < params1.Length; i++)
        {
            if (params1[i].Constraint != params2[i].Constraint)
            {
                return false;
            }
        }

        return true;
    }
}
