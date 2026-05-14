using System;
using Bond.Parser.Syntax;

namespace Bond.Parser.Parser;

/// <summary>
/// Thrown by the parser pipeline (SymbolTable, SemanticAnalyzer, TypeResolver)
/// for any semantic error that can be attributed to a specific source location.
/// ParserFacade catches it and converts to a ParseError; callers do not need to
/// catch it directly.
/// </summary>
public sealed class SemanticErrorException : Exception
{
    public SourceLocation Location { get; }

    public SemanticErrorException(string message, SourceLocation location)
        : base(message)
    {
        Location = location;
    }
}
