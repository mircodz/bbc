using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Antlr4.Runtime;
using Bond.Parser.Syntax;
using Bond.Parser.Grammar;

namespace Bond.Parser.Parser;

/// <summary>
/// Performs semantic analysis on the AST
/// </summary>
public class SemanticAnalyzer
{
    private readonly SymbolTable _symbolTable;
    private readonly ImportResolver _importResolver;
    private readonly string _currentFile;

    public SemanticAnalyzer(SymbolTable symbolTable, ImportResolver importResolver, string currentFile)
    {
        _symbolTable = symbolTable;
        _importResolver = importResolver;
        _currentFile = currentFile;
    }

    /// <summary>
    /// Analyzes a Bond AST
    /// </summary>
    public async Task AnalyzeAsync(Syntax.Bond bond)
    {
        _symbolTable.PushAliasScope();
        try
        {
            foreach (var import in bond.Imports)
            {
                await ProcessImportAsync(import);
            }

            foreach (var declaration in bond.Declarations)
            {
                _symbolTable.AddDeclaration(declaration);
            }

            // Validate after all symbols are registered so forward references resolve.
            foreach (var declaration in bond.Declarations)
            {
                ValidateDeclaration(declaration);
            }
        }
        finally
        {
            _symbolTable.PopAliasScope();
        }
    }

    private async Task ProcessImportAsync(Import import)
    {
        var (canonicalPath, content) = await _importResolver(_currentFile, import.FilePath);

        if (_symbolTable.IsImportProcessed(canonicalPath))
        {
            return;
        }

        _symbolTable.MarkImportProcessed(canonicalPath);
        var importAst = ParseContent(content, canonicalPath);

        // Recursively analyze imports in the imported file, reusing the same symbol table
        var analyzer = new SemanticAnalyzer(_symbolTable, _importResolver, canonicalPath);
        await analyzer.AnalyzeAsync(importAst);
    }

    private static Syntax.Bond ParseContent(string content, string filePath)
    {
        var inputStream = new AntlrInputStream(content);
        var lexer = new BondLexer(inputStream);
        var tokenStream = new CommonTokenStream(lexer);
        var parser = new BondParser(tokenStream);

        var errorListener = new ErrorListener(filePath);
        parser.RemoveErrorListeners();
        parser.AddErrorListener(errorListener);

        var parseTree = parser.bond();
        if (errorListener.Errors.Count > 0)
        {
            var first = errorListener.Errors.First();
            throw new InvalidOperationException($"{first.Message} (imported from {filePath}:{first.Line}:{first.Column})");
        }

        var astBuilder = new AstBuilder();
        return (Syntax.Bond)astBuilder.Visit(parseTree)!;
    }

    private void ValidateDeclaration(Declaration declaration)
    {
        switch (declaration)
        {
            case StructDeclaration structDecl:
                ValidateStruct(structDecl);
                break;
            case EnumDeclaration enumDecl:
                ValidateEnum(enumDecl);
                break;
            case ServiceDeclaration serviceDecl:
                ValidateService(serviceDecl);
                break;
        }
    }

    private void ValidateStruct(StructDeclaration structDecl)
    {
        CheckForDuplicates(structDecl.Fields.Select(f => f.Ordinal), $"Struct '{structDecl.Name}'", "field ordinal", structDecl.Location);
        CheckForDuplicates(structDecl.Fields.Select(f => f.Name), $"Struct '{structDecl.Name}'", "field name", structDecl.Location);

        foreach (var field in structDecl.Fields)
        {
            ValidateField(field, structDecl.Namespaces);
        }
    }

    private void ValidateEnum(EnumDeclaration enumDecl)
    {
        CheckForDuplicates(enumDecl.Constants.Select(c => c.Name), $"Enum '{enumDecl.Name}'", "constant name", enumDecl.Location);
    }

    private void ValidateService(ServiceDeclaration serviceDecl)
    {
        CheckForDuplicates(serviceDecl.Methods.Select(m => m.Name), $"Service '{serviceDecl.Name}'", "method name", serviceDecl.Location);

        if (serviceDecl.BaseType != null)
        {
            if (serviceDecl.BaseType is BondType.TypeParameter)
            {
                throw new SemanticErrorException($"Service '{serviceDecl.Name}' cannot inherit from type parameter", serviceDecl.Location);
            }

            if (serviceDecl.BaseType.IsStruct())
            {
                throw new SemanticErrorException($"Service '{serviceDecl.Name}' cannot inherit from struct", serviceDecl.Location);
            }

            if (serviceDecl.BaseType is BondType.UnresolvedUserType unresolved)
            {
                var baseDecl = _symbolTable.FindSymbol(unresolved.QualifiedName, serviceDecl.Namespaces);
                if (baseDecl is StructDeclaration)
                {
                    throw new SemanticErrorException($"Service '{serviceDecl.Name}' cannot inherit from struct '{string.Join(".", unresolved.QualifiedName)}'", serviceDecl.Location);
                }
            }
        }

        foreach (var method in serviceDecl.Methods.OfType<EventMethod>())
        {
            if (method.InputType is MethodType.Streaming)
            {
                throw new SemanticErrorException($"Event method '{method.Name}' cannot have streaming input", serviceDecl.Location);
            }
        }
    }

    private static void CheckForDuplicates<T>(IEnumerable<T> items, string context, string itemType, SourceLocation location)
    {
        var duplicates = items
            .GroupBy(x => x)
            .Where(g => g.Count() > 1)
            .Select(g => g.Key)
            .ToList();

        if (duplicates.Any())
        {
            throw new SemanticErrorException($"{context} has duplicate {itemType}(s): {string.Join(", ", duplicates)}", location);
        }
    }

    /// <summary>
    /// Resolves type aliases to their underlying types, including nested container types
    /// </summary>
    private BondType ResolveAliases(BondType type, Namespace[] namespaces)
    {
        return ResolveAliases(type, namespaces, new HashSet<Declaration>());
    }

    private BondType ResolveAliases(BondType type, Namespace[] namespaces, HashSet<Declaration> visiting)
    {
        return type switch
        {
            BondType.UnresolvedUserType unresolved => ResolveAliasType(unresolved, namespaces, visiting),
            BondType.List list => new BondType.List(ResolveAliases(list.ElementType, namespaces, visiting)),
            BondType.Vector vector => new BondType.Vector(ResolveAliases(vector.ElementType, namespaces, visiting)),
            BondType.Set set => new BondType.Set(ResolveAliases(set.KeyType, namespaces, visiting)),
            BondType.Map map => new BondType.Map(ResolveAliases(map.KeyType, namespaces, visiting), ResolveAliases(map.ValueType, namespaces, visiting)),
            BondType.Nullable nullable => new BondType.Nullable(ResolveAliases(nullable.ElementType, namespaces, visiting)),
            BondType.Maybe maybe => new BondType.Maybe(ResolveAliases(maybe.ElementType, namespaces, visiting)),
            BondType.Bonded bonded => new BondType.Bonded(ResolveAliases(bonded.StructType, namespaces, visiting)),
            _ => type
        };
    }

    private BondType ResolveAliasType(BondType.UnresolvedUserType unresolved, Namespace[] namespaces, HashSet<Declaration> visiting)
    {
        var decl = _symbolTable.FindSymbol(unresolved.QualifiedName, namespaces);
        if (decl is not AliasDeclaration alias)
        {
            return unresolved;
        }

        if (!visiting.Add(alias))
        {
            return unresolved;
        }

        var resolved = ResolveAliases(alias.AliasedType, namespaces, visiting);
        visiting.Remove(alias);
        return resolved;
    }

    private void ValidateField(Field field, Namespace[] namespaces)
    {
        var actualType = ResolveAliases(field.Type, namespaces);

        // Validate map/set key types
        if (actualType is BondType.Set set && !TypeValidator.IsValidKeyType(set.KeyType))
        {
            throw new SemanticErrorException($"Field '{field.Name}' has invalid set key type {set.KeyType}", field.Location);
        }
        if (actualType is BondType.Map map && !TypeValidator.IsValidKeyType(map.KeyType))
        {
            throw new SemanticErrorException($"Field '{field.Name}' has invalid map key type {map.KeyType}", field.Location);
        }

        if (!TypeValidator.ValidateDefaultValue(actualType, field.DefaultValue))
        {
            throw new SemanticErrorException($"Field '{field.Name}' has invalid default value for type {field.Type}", field.Location);
        }

        bool isEnumField = actualType.IsEnum();
        if (!isEnumField && field.Type is BondType.UnresolvedUserType unresolvedEnum)
        {
            var decl = _symbolTable.FindSymbol(unresolvedEnum.QualifiedName, namespaces);
            if (decl is EnumDeclaration)
                isEnumField = true;
        }

        if (isEnumField && field.DefaultValue == null && field.Modifier != FieldModifier.Required)
        {
            throw new SemanticErrorException($"Enum field '{field.Name}' must have a default value", field.Location);
        }

        // Structs cannot have default 'nothing' even when wrapped in Maybe
        if (field.DefaultValue is Default.Nothing)
        {
            var underlying = UnwrapMaybe(field.Type);
            if (IsStructType(underlying, namespaces))
            {
                throw new SemanticErrorException($"Struct field '{field.Name}' cannot have default value of 'nothing'", field.Location);
            }
        }
    }

    private BondType UnwrapMaybe(BondType type) => type is BondType.Maybe maybe ? maybe.ElementType : type;

    private bool IsStructType(BondType type, Namespace[] namespaces)
    {
        BondType resolved = ResolveAliases(type, namespaces);

        return resolved switch
        {
            BondType.UserDefined { Declaration: StructDeclaration or ForwardDeclaration } => true,
            BondType.UnresolvedUserType unresolved => _symbolTable.FindSymbol(unresolved.QualifiedName, namespaces) is StructDeclaration,
            _ => false
        };
    }
}
