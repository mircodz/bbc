using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Antlr4.Runtime;
using Bond.Parser.Grammar;
using Bond.Parser.Parser;

namespace Bond.Parser.Formatting;

public sealed record FormatOptions(int IndentSize = 4);

public sealed record FormatResult(string? FormattedText, List<ParseError> Errors)
{
    public bool Success => Errors.Count == 0 && FormattedText != null;
}

public static class BondFormatter
{
    public static FormatResult Format(string content, string filePath, FormatOptions? options = null)
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

            parser.bond();
            if (errorListener.Errors.Count > 0)
            {
                errors.AddRange(errorListener.Errors);
                return new FormatResult(null, errors);
            }

            tokenStream.Fill();
            var formatter = new TokenFormatter(tokenStream, options ?? new FormatOptions());
            var formatted = formatter.Format();
            return new FormatResult(formatted, errors);
        }
        catch (Exception ex)
        {
            errors.Add(new ParseError($"Unexpected error: {ex.Message}", filePath, 0, 0));
            return new FormatResult(null, errors);
        }
    }

    private sealed class TokenFormatter
    {
        private static readonly HashSet<int> NoSpaceBefore =
        [
            BondLexer.COMMA,
            BondLexer.SEMI,
            BondLexer.RPAREN,
            BondLexer.RBRACKET,
            BondLexer.RANGLE,
            BondLexer.DOT,
            BondLexer.COLON,
            BondLexer.RBRACE
        ];

        private static readonly HashSet<int> NoSpaceAfter =
        [
            BondLexer.LPAREN,
            BondLexer.LBRACKET,
            BondLexer.LANGLE,
            BondLexer.DOT,
            BondLexer.MINUS,
            BondLexer.PLUS
        ];

        private static readonly HashSet<int> TopLevelStartTokens =
        [
            BondLexer.IMPORT,
            BondLexer.NAMESPACE,
            BondLexer.USING,
            BondLexer.STRUCT,
            BondLexer.ENUM,
            BondLexer.SERVICE,
            BondLexer.VIEW_OF
        ];

        private readonly CommonTokenStream _tokenStream;
        private readonly FormatOptions _options;
        private readonly StringBuilder _builder = new();
        private readonly List<IToken> _defaultTokens;
        private bool _atLineStart = true;
        private bool _pendingSpace;
        private int _indentLevel;
        private bool _pendingTopLevelBlankLine;
        private int? _lastTopLevelKeyword;

        public TokenFormatter(CommonTokenStream tokenStream, FormatOptions options)
        {
            _tokenStream = tokenStream;
            _options = options;
            _defaultTokens = tokenStream.GetTokens()
                .Where(t => t.Channel == TokenConstants.DefaultChannel && t.Type != TokenConstants.EOF)
                .ToList();
        }

        public string Format()
        {
            for (int i = 0; i < _defaultTokens.Count; i++)
            {
                var token = _defaultTokens[i];
                var nextType = i + 1 < _defaultTokens.Count ? _defaultTokens[i + 1].Type : TokenConstants.EOF;

                ProcessHiddenTokens(token);
                WriteToken(token, nextType);
            }

            if (!_atLineStart)
            {
                WriteNewline();
            }

            return _builder.ToString();
        }

        private void ProcessHiddenTokens(IToken token)
        {
            var hidden = _tokenStream.GetHiddenTokensToLeft(token.TokenIndex);
            if (hidden == null || hidden.Count == 0)
            {
                return;
            }

            foreach (var ht in hidden)
            {
                if (ht.Type == BondLexer.WS)
                {
                    var newlines = CountNewlines(ht.Text);
                    if (newlines > 0)
                    {
                        WriteNewline();
                    }
                    continue;
                }

                if (ht.Type == BondLexer.LINE_COMMENT || ht.Type == BondLexer.COMMENT)
                {
                    if (_pendingTopLevelBlankLine && _indentLevel == 0)
                    {
                        _pendingTopLevelBlankLine = false;
                        EnsureBlankLine();
                    }

                    if (!_atLineStart)
                    {
                        WriteNewline();
                    }

                    WriteIndent();
                    _builder.Append(ht.Text.TrimEnd());
                    WriteNewline();
                }
            }
        }

        private void WriteToken(IToken token, int nextType)
        {
            ApplyTopLevelSpacingIfNeeded(token.Type);

            if (token.Type == BondLexer.RBRACE)
            {
                if (!_atLineStart)
                {
                    WriteNewline();
                }
                _indentLevel = Math.Max(0, _indentLevel - 1);
            }

            if (_atLineStart)
            {
                WriteIndent();
            }
            else if (_pendingSpace && !NoSpaceBefore.Contains(token.Type))
            {
                _builder.Append(' ');
            }

            _builder.Append(token.Text);
            _pendingSpace = ShouldSetPendingSpace(token.Type);

            if (token.Type == BondLexer.NAMESPACE && _indentLevel == 0)
            {
                _pendingTopLevelBlankLine = true;
            }

            if (token.Type == BondLexer.LBRACE)
            {
                WriteNewline();
                _indentLevel++;
                return;
            }

            if (token.Type == BondLexer.SEMI)
            {
                WriteNewline();
                return;
            }

            if (token.Type == BondLexer.RBRACE)
            {
                if (nextType == BondLexer.SEMI)
                {
                    _pendingSpace = false;
                    return;
                }
                if (_indentLevel == 0)
                {
                    _pendingTopLevelBlankLine = true;
                }
                WriteNewline();
                return;
            }

            if (token.Type == BondLexer.SEMI && _indentLevel == 0)
            {
                _pendingTopLevelBlankLine = true;
            }
        }

        private void ApplyTopLevelSpacingIfNeeded(int tokenType)
        {
            if (!_pendingTopLevelBlankLine)
            {
                return;
            }

            if (!TopLevelStartTokens.Contains(tokenType))
            {
                return;
            }

            var previous = _lastTopLevelKeyword;
            _lastTopLevelKeyword = tokenType;

            _pendingTopLevelBlankLine = false;

            if (previous == BondLexer.IMPORT && tokenType == BondLexer.IMPORT)
            {
                return;
            }

            EnsureBlankLine();
        }

        private bool ShouldSetPendingSpace(int type)
        {
            if (type == BondLexer.SEMI || type == BondLexer.LBRACE || type == BondLexer.RBRACE)
            {
                return false;
            }

            return !NoSpaceAfter.Contains(type);
        }

        private void WriteIndent()
        {
            if (!_atLineStart)
            {
                return;
            }

            if (_indentLevel > 0)
            {
                _builder.Append(' ', _indentLevel * _options.IndentSize);
            }

            _atLineStart = false;
        }

        private void WriteNewline()
        {
            if (_builder.Length == 0 || _builder[^1] != '\n')
            {
                _builder.Append('\n');
            }
            _atLineStart = true;
            _pendingSpace = false;
        }

        private void EnsureBlankLine()
        {
            if (!_atLineStart)
            {
                WriteNewline();
            }

            var trailingNewlines = CountTrailingNewlines();
            if (trailingNewlines >= 2)
            {
                return;
            }

            while (trailingNewlines < 2)
            {
                _builder.Append('\n');
                trailingNewlines++;
            }

            _atLineStart = true;
            _pendingSpace = false;
        }

        private int CountTrailingNewlines()
        {
            var count = 0;
            for (int i = _builder.Length - 1; i >= 0; i--)
            {
                if (_builder[i] != '\n')
                {
                    break;
                }
                count++;
            }
            return count;
        }

        private static int CountNewlines(string? text)
        {
            if (string.IsNullOrEmpty(text))
            {
                return 0;
            }

            var count = 0;
            foreach (var ch in text)
            {
                if (ch == '\n')
                {
                    count++;
                }
            }
            return count;
        }
    }
}
