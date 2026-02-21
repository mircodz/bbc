using System;
using Bond.Parser.Syntax;

namespace Bond.Parser.Parser;

/// <summary>
/// Thrown by SemanticAnalyzer and TypeResolver when a semantic error can be attributed
/// to a specific source location.
/// </summary>
internal sealed class SemanticErrorException : Exception
{
    public SourceLocation Location { get; }

    public SemanticErrorException(string message, SourceLocation location)
        : base(message)
    {
        Location = location;
    }
}
