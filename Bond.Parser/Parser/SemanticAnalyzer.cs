using Bond.Parser.Syntax;

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
        // Process imports first
        foreach (var import in bond.Imports)
        {
            await ProcessImportAsync(import);
        }

        // Add all declarations to symbol table
        foreach (var declaration in bond.Declarations)
        {
            _symbolTable.AddDeclaration(declaration, bond.Namespaces);
            ValidateDeclaration(declaration, bond.Namespaces);
        }
    }

    private async Task ProcessImportAsync(Import import)
    {
        var (canonicalPath, content) = await _importResolver(_currentFile, import.FilePath);

        if (_symbolTable.IsImportProcessed(canonicalPath))
            return;

        _symbolTable.MarkImportProcessed(canonicalPath);
    }

    private void ValidateDeclaration(Declaration declaration, Namespace[] namespaces)
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
        CheckForDuplicates(structDecl.Fields.Select(f => f.Ordinal),
            $"Struct '{structDecl.Name}'", "field ordinal");
        CheckForDuplicates(structDecl.Fields.Select(f => f.Name),
            $"Struct '{structDecl.Name}'", "field name");

        foreach (var field in structDecl.Fields)
        {
            ValidateField(field, structDecl.Namespaces);
        }
    }

    private void ValidateEnum(EnumDeclaration enumDecl)
    {
        CheckForDuplicates(enumDecl.Constants.Select(c => c.Name),
            $"Enum '{enumDecl.Name}'", "constant name");
    }

    private void ValidateService(ServiceDeclaration serviceDecl)
    {
        CheckForDuplicates(serviceDecl.Methods.Select(m => m.Name),
            $"Service '{serviceDecl.Name}'", "method name");

        if (serviceDecl.BaseType != null)
        {
            if (serviceDecl.BaseType is BondType.TypeParameter)
                throw new InvalidOperationException(
                    $"Service '{serviceDecl.Name}' cannot inherit from type parameter");

            if (serviceDecl.BaseType.IsStruct())
                throw new InvalidOperationException(
                    $"Service '{serviceDecl.Name}' cannot inherit from struct");

            if (serviceDecl.BaseType is BondType.UnresolvedUserType unresolved)
            {
                var baseDecl = _symbolTable.FindSymbol(unresolved.QualifiedName, serviceDecl.Namespaces);
                if (baseDecl is StructDeclaration)
                    throw new InvalidOperationException(
                        $"Service '{serviceDecl.Name}' cannot inherit from struct '{string.Join(".", unresolved.QualifiedName)}'");
            }
        }

        foreach (var method in serviceDecl.Methods.OfType<EventMethod>())
        {
            if (method.InputType is MethodType.Streaming)
            {
                throw new InvalidOperationException(
                    $"Event method '{method.Name}' cannot have streaming input");
            }
        }
    }

    private static void CheckForDuplicates<T>(IEnumerable<T> items, string context, string itemType)
    {
        var duplicates = items.GroupBy(x => x).Where(g => g.Count() > 1).Select(g => g.Key).ToList();
        if (duplicates.Any())
        {
            throw new InvalidOperationException(
                $"{context} has duplicate {itemType}(s): {string.Join(", ", duplicates)}");
        }
    }

    /// <summary>
    /// Resolves type aliases to their underlying types
    /// </summary>
    private BondType ResolveAliases(BondType type, Namespace[] namespaces)
    {
        if (type is not BondType.UnresolvedUserType unresolved)
            return type;

        var decl = _symbolTable.FindSymbol(unresolved.QualifiedName, namespaces);

        // Recursively resolve aliases
        if (decl is AliasDeclaration alias)
            return ResolveAliases(alias.AliasedType, namespaces);

        return type;
    }

    private void ValidateField(Field field, Namespace[] namespaces)
    {
        var actualType = ResolveAliases(field.Type, namespaces);

        if (!TypeValidator.ValidateDefaultValue(actualType, field.DefaultValue))
            throw new InvalidOperationException(
                $"Field '{field.Name}' has invalid default value for type {field.Type}");

        bool isEnumField = field.Type.IsEnum();
        if (field.Type is BondType.UnresolvedUserType unresolvedEnum)
        {
            var decl = _symbolTable.FindSymbol(unresolvedEnum.QualifiedName, namespaces);
            if (decl is EnumDeclaration)
                isEnumField = true;
        }

        if (isEnumField && field.DefaultValue == null && field.Modifier != FieldModifier.Required)
            throw new InvalidOperationException(
                $"Enum field '{field.Name}' must have a default value");

        TypeValidator.ValidateStructField(field);

        if (field.Type is BondType.UnresolvedUserType unresolved && field.DefaultValue is Default.Nothing)
        {
            var decl = _symbolTable.FindSymbol(unresolved.QualifiedName, namespaces);
            if (decl is StructDeclaration)
                throw new InvalidOperationException(
                    $"Struct field '{field.Name}' cannot have default value of 'nothing'");
        }
    }
}
