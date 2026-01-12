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
            ValidateField(field);
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

    private void ValidateField(Field field)
    {
        if (!TypeValidator.ValidateDefaultValue(field.Type, field.DefaultValue))
        {
            throw new InvalidOperationException(
                $"Field '{field.Name}' has invalid default value for type {field.Type}");
        }

        if (field.Type.IsEnum() && field.DefaultValue == null)
        {
            TypeValidator.ValidateEnumField(field);
        }
    }
}
