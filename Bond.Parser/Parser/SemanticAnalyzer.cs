using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Antlr4.Runtime;
using Bond.Parser.Syntax;
using Bond.Parser.Grammar;

namespace Bond.Parser.Parser;

/// <summary>
/// Drives the semantic phase of parsing for a single Bond file:
///   1. Process imports recursively, populating the shared SymbolTable.
///   2. Register this file's declarations (structs/enums/services to the table,
///      aliases to a per-file list).
///   3. Resolve every UnresolvedUserType reference to a UserDefined wrapper.
///   4. Validate the resolved AST.
/// Validation operates on the resolved AST so checks are pure pattern matches —
/// no side trips to the symbol table.
/// </summary>
public class SemanticAnalyzer
{
    private readonly SymbolTable _symbolTable;
    private readonly ImportResolver _importResolver;
    private readonly string _currentFile;
    private readonly List<AliasDeclaration> _aliases = [];

    public SemanticAnalyzer(SymbolTable symbolTable, ImportResolver importResolver, string currentFile)
    {
        _symbolTable = symbolTable;
        _importResolver = importResolver;
        _currentFile = currentFile;
    }

    public async Task<Syntax.Bond> AnalyzeAsync(Syntax.Bond bond)
    {
        foreach (var import in bond.Imports)
        {
            await ProcessImportAsync(import);
        }

        foreach (var declaration in bond.Declarations)
        {
            RegisterDeclaration(declaration);
        }

        var resolved = TypeResolver.Resolve(bond, _symbolTable, _aliases);

        foreach (var declaration in resolved.Declarations)
        {
            ValidateDeclaration(declaration);
        }

        return resolved;
    }

    private void RegisterDeclaration(Declaration declaration)
    {
        if (declaration is AliasDeclaration alias)
        {
            RegisterAlias(alias);
            return;
        }
        _symbolTable.AddDeclaration(declaration);
    }

    private void RegisterAlias(AliasDeclaration alias)
    {
        var duplicate = _aliases.FirstOrDefault(existing =>
            existing.Name == alias.Name &&
            existing.Namespaces.Any(ns => alias.Namespaces.Any(ns.Matches)));

        if (duplicate is not null)
        {
            throw new SemanticErrorException($"Duplicate declaration: alias '{alias.Name}' was already declared", alias.Location);
        }
        _aliases.Add(alias);
    }

    private async Task ProcessImportAsync(Import import)
    {
        var (canonicalPath, content) = await _importResolver(_currentFile, import.FilePath);

        if (!_symbolTable.ClaimImport(canonicalPath))
        {
            return;
        }

        var importAst = ParseContent(content, canonicalPath);
        var analyzer = new SemanticAnalyzer(_symbolTable, _importResolver, canonicalPath);
        // Resolved AST is discarded — symbols are now in the table for lookups.
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

    private static void ValidateDeclaration(Declaration declaration)
    {
        switch (declaration)
        {
            case StructDeclaration structDecl: ValidateStruct(structDecl); break;
            case EnumDeclaration enumDecl: ValidateEnum(enumDecl); break;
            case ServiceDeclaration serviceDecl: ValidateService(serviceDecl); break;
        }
    }

    private static void ValidateStruct(StructDeclaration structDecl)
    {
        CheckForDuplicates(structDecl.Fields.Select(f => f.Ordinal), $"Struct '{structDecl.Name}'", "field ordinal", structDecl.Location);
        CheckForDuplicates(structDecl.Fields.Select(f => f.Name), $"Struct '{structDecl.Name}'", "field name", structDecl.Location);

        foreach (var field in structDecl.Fields)
        {
            ValidateField(field);
        }
    }

    private static void ValidateEnum(EnumDeclaration enumDecl)
    {
        CheckForDuplicates(enumDecl.Constants.Select(c => c.Name), $"Enum '{enumDecl.Name}'", "constant name", enumDecl.Location);
    }

    private static void ValidateService(ServiceDeclaration serviceDecl)
    {
        CheckForDuplicates(serviceDecl.Methods.Select(m => m.Name), $"Service '{serviceDecl.Name}'", "method name", serviceDecl.Location);

        if (serviceDecl.BaseType is BondType.TypeParameter)
        {
            throw new SemanticErrorException($"Service '{serviceDecl.Name}' cannot inherit from type parameter", serviceDecl.Location);
        }

        if (serviceDecl.BaseType is not null && UnwrapAlias(serviceDecl.BaseType).IsStruct())
        {
            throw new SemanticErrorException($"Service '{serviceDecl.Name}' cannot inherit from struct", serviceDecl.Location);
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
        var duplicates = items.GroupBy(x => x).Where(g => g.Count() > 1).Select(g => g.Key).ToList();
        if (duplicates.Count > 0)
        {
            throw new SemanticErrorException($"{context} has duplicate {itemType}(s): {string.Join(", ", duplicates)}", location);
        }
    }

    private static void ValidateField(Field field)
    {
        var unwrapped = UnwrapAlias(field.Type);

        if (unwrapped is BondType.Set set && !UnwrapAlias(set.KeyType).IsValidKeyType())
        {
            throw new SemanticErrorException($"Field '{field.Name}' has invalid set key type {set.KeyType}", field.Location);
        }
        if (unwrapped is BondType.Map map && !UnwrapAlias(map.KeyType).IsValidKeyType())
        {
            throw new SemanticErrorException($"Field '{field.Name}' has invalid map key type {map.KeyType}", field.Location);
        }

        if (!TypeValidator.ValidateDefaultValue(unwrapped, field.DefaultValue))
        {
            throw new SemanticErrorException($"Field '{field.Name}' has invalid default value for type {field.Type}", field.Location);
        }

        if (unwrapped.IsEnum() && field.DefaultValue == null && field.Modifier != FieldModifier.Required)
        {
            throw new SemanticErrorException($"Enum field '{field.Name}' must have a default value", field.Location);
        }

        // Structs cannot have default 'nothing' even when wrapped in Maybe.
        if (field.DefaultValue is Default.Nothing && UnwrapAlias(UnwrapMaybe(field.Type)).IsStruct())
        {
            throw new SemanticErrorException($"Struct field '{field.Name}' cannot have default value of 'nothing'", field.Location);
        }
    }

    private static BondType UnwrapMaybe(BondType type) =>
        type is BondType.Maybe maybe ? maybe.ElementType : type;

    /// <summary>
    /// Walks chains of resolved aliases (`UserDefined { Declaration: AliasDeclaration }`)
    /// down to the underlying concrete type. Validation works on the unwrapped form so
    /// `using Latency = int32` and `int32` behave identically.
    /// </summary>
    private static BondType UnwrapAlias(BondType type) =>
        type is BondType.UserDefined { Declaration: AliasDeclaration alias }
            ? UnwrapAlias(alias.AliasedType)
            : type;
}
