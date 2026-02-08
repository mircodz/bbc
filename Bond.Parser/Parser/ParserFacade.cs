using Antlr4.Runtime;
using Bond.Parser.Grammar;

namespace Bond.Parser.Parser;

/// <summary>
/// Parse error information
/// </summary>
public record ParseError(
    string Message,
    string? FilePath,
    int Line,
    int Column
);

/// <summary>
/// Result of parsing a Bond file
/// </summary>
public record ParseResult(
    Syntax.Bond? Ast,
    List<ParseError> Errors
)
{
    public bool Success => Errors.Count == 0 && Ast != null;
}

/// <summary>
/// Main facade for parsing Bond files
/// </summary>
public static class ParserFacade
{
    /// <summary>
    /// Parses a Bond file from a file path
    /// </summary>
    public static async Task<ParseResult> ParseFileAsync(
        string filePath,
        ImportResolver? importResolver = null,
        CancellationToken cancellationToken = default)
    {
        if (!File.Exists(filePath))
        {
            return new ParseResult(null, [new ParseError($"File not found: {filePath}", filePath, 0, 0)]);
        }

        var content = await File.ReadAllTextAsync(filePath, cancellationToken);
        var absolutePath = Path.GetFullPath(filePath);

        return await ParseContentAsync(content, absolutePath, importResolver ?? DefaultImportResolver.Resolve);
    }

    /// <summary>
    /// Parses Bond content from a string without file path context
    /// </summary>
    public static Task<ParseResult> ParseStringAsync(string content, ImportResolver? importResolver = null)
    {
        return ParseContentAsync(content, "<inline>", importResolver ?? DefaultImportResolver.Resolve);
    }

    /// <summary>
    /// Parses Bond content from a string
    /// </summary>
    private static async Task<ParseResult> ParseContentAsync(
        string content,
        string filePath,
        ImportResolver importResolver)
    {
        var errors = new List<ParseError>();

        try
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
                return new ParseResult(null, errorListener.Errors.ToList());
            }

            // Build AST
            var astBuilder = new AstBuilder();
            var ast = (Syntax.Bond)astBuilder.Visit(parseTree)!;

            // Perform semantic analysis
            var symbolTable = new SymbolTable();
            var analyzer = new SemanticAnalyzer(symbolTable, importResolver, filePath);

            try
            {
                await analyzer.AnalyzeAsync(ast);
            }
            catch (Exception ex)
            {
                errors.Add(new ParseError(ex.Message, filePath, 0, 0));
                return new ParseResult(ast, errors);
            }

            // Resolve types
            try
            {
                var typeResolver = new TypeResolver(symbolTable);
                ast = typeResolver.ResolveTypes(ast);
            }
            catch (Exception ex)
            {
                errors.Add(new ParseError($"Type resolution failed: {ex.Message}", filePath, 0, 0));
                return new ParseResult(ast, errors);
            }

            return new ParseResult(ast, errors);
        }
        catch (Exception ex)
        {
            errors.Add(new ParseError($"Unexpected error: {ex.Message}", filePath, 0, 0));
            return new ParseResult(null, errors);
        }
    }
}
