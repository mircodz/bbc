using Antlr4.Runtime;

namespace Bond.Parser.Parser;

/// <summary>
/// Error listener for collecting ANTLR parse errors
/// </summary>
public sealed class ErrorListener(string? path = null) : IAntlrErrorListener<IToken>
{
    private readonly List<ParseError> _errors = [];

    public IReadOnlyList<ParseError> Errors => _errors;

    public void SyntaxError(
        TextWriter output,
        IRecognizer recognizer,
        IToken offendingSymbol,
        int line,
        int charPositionInLine,
        string msg,
        RecognitionException e)
    {
        _errors.Add(new ParseError(msg, path, line, charPositionInLine));
    }
}
