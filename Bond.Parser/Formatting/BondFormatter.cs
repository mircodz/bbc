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
            BondLexer.LANGLE,
            BondLexer.LPAREN,
            BondLexer.DOT,
            BondLexer.COLON,
            BondLexer.RBRACE
        ];

        private static readonly HashSet<int> NoSpaceAfter =
        [
            BondLexer.LPAREN,
            BondLexer.LBRACKET,
            BondLexer.RBRACKET,
            BondLexer.LANGLE,
            BondLexer.DOT,
            BondLexer.MINUS,
            BondLexer.PLUS,
            BondLexer.L
        ];

        private static readonly HashSet<int> TopLevelStartTokens =
        [
            BondLexer.IMPORT,
            BondLexer.NAMESPACE,
            BondLexer.USING,
            BondLexer.STRUCT,
            BondLexer.ENUM,
            BondLexer.SERVICE,
            BondLexer.VIEW_OF,
            BondLexer.LBRACKET
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
        private bool _inTypeHeader;
        private int? _pendingBlockKind;
        private readonly Stack<int?> _blockStack = new();
        private IToken? _lastToken;
        private bool _skipNextRBrace;
        private bool _skipNextSemi;

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

                if (_skipNextRBrace && token.Type == BondLexer.RBRACE)
                {
                    _skipNextRBrace = false;
                    if (nextType == BondLexer.SEMI)
                    {
                        _skipNextSemi = true;
                    }
                    _lastToken = token;
                    continue;
                }
                if (_skipNextSemi && token.Type == BondLexer.SEMI)
                {
                    ProcessHiddenTokens(token);
                    _skipNextSemi = false;
                    _lastToken = token;
                    continue;
                }

                ProcessHiddenTokens(token);
                WriteToken(token, nextType);
            }

            if (!_atLineStart)
            {
                WriteNewline();
            }

            var text = _builder.ToString();
            if (text.EndsWith('\n'))
            {
                text = text[..^1];
            }
            return text;
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
                    continue;
                }

                if (ht.Type == BondLexer.LINE_COMMENT || ht.Type == BondLexer.COMMENT)
                {
                    var inlineBefore = ht.Type == BondLexer.COMMENT && ht.Line == token.Line;
                    var inlineAfter = _lastToken != null && ht.Line == _lastToken.Line;

                    if (inlineBefore || inlineAfter)
                    {
                        if (_atLineStart)
                        {
                            WriteIndent();
                        }
                        else
                        {
                            _builder.Append(' ');
                        }

                        _builder.Append(ht.Text.TrimEnd());

                        if (ht.Type == BondLexer.LINE_COMMENT)
                        {
                            WriteNewline();
                        }
                        else
                        {
                            _pendingSpace = true;
                        }

                        continue;
                    }

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

            int? closingBlockKind = null;
            if (StartsTypeHeader(token.Type))
            {
                _inTypeHeader = _indentLevel == 0;
            }

            var declaredBlockKind = GetBlockKind(token.Type);
            if (declaredBlockKind.HasValue)
            {
                _pendingBlockKind = declaredBlockKind;
            }

            if (token.Type == BondLexer.RBRACE)
            {
                if (!_atLineStart)
                {
                    WriteNewline();
                }
                _indentLevel = Math.Max(0, _indentLevel - 1);
                closingBlockKind = _blockStack.Count > 0 ? _blockStack.Pop() : null;
            }

            if (_atLineStart)
            {
                WriteIndent();
            }
            else if (_pendingSpace && (!NoSpaceBefore.Contains(token.Type) || (token.Type == BondLexer.COLON && _inTypeHeader)))
            {
                _builder.Append(' ');
            }

            _builder.Append(token.Text);
            _pendingSpace = ShouldSetPendingSpace(token.Type);

            if (token.Type == BondLexer.RBRACKET)
            {
                WriteNewline();
                UpdateTopLevelKeyword(token.Type);
                _lastToken = token;
                return;
            }
            if ((token.Type == BondLexer.COMMA || token.Type == BondLexer.SEMI) && IsInsideEnumBlock())
            {
                WriteNewline();
            }
            if (token.Type == BondLexer.NAMESPACE && _indentLevel == 0)
            {
                _pendingTopLevelBlankLine = true;
            }

            if (token.Type == BondLexer.LBRACE)
            {
                var blockKind = _pendingBlockKind;
                _pendingBlockKind = null;
                if (blockKind == BondLexer.STRUCT && IsEmptyBlock(token, nextType))
                {
                    _builder.Append('}');
                    _skipNextRBrace = true;
                    WriteNewline();
                    _pendingTopLevelBlankLine |= _indentLevel == 0;
                    _inTypeHeader = false;
                    _lastToken = token;
                    return;
                }

                WriteNewline();
                _blockStack.Push(blockKind);
                _indentLevel++;
                _inTypeHeader = false;
                UpdateTopLevelKeyword(token.Type);
                _lastToken = token;
                return;
            }

            if (token.Type == BondLexer.SEMI)
            {
                if (_indentLevel == 0)
                {
                    _pendingTopLevelBlankLine = true;
                }
                WriteNewline();
                _inTypeHeader = false;
                UpdateTopLevelKeyword(token.Type);
                _lastToken = token;
                return;
            }

            if (token.Type == BondLexer.RBRACE)
            {
                if (nextType == BondLexer.SEMI && (closingBlockKind == BondLexer.STRUCT || closingBlockKind == BondLexer.ENUM))
                {
                    _pendingSpace = false;
                    _skipNextSemi = true;
                }
                if (_indentLevel == 0)
                {
                    _pendingTopLevelBlankLine = true;
                }
                WriteNewline();
                _inTypeHeader = false;
                UpdateTopLevelKeyword(token.Type);
                _lastToken = token;
                return;
            }

            UpdateTopLevelKeyword(token.Type);
            _lastToken = token;
        }

        private void ApplyTopLevelSpacingIfNeeded(int tokenType)
        {
            if (!_pendingTopLevelBlankLine)
            {
                if (_lastTopLevelKeyword == BondLexer.IMPORT
                    && tokenType != BondLexer.IMPORT
                    && TopLevelStartTokens.Contains(tokenType))
                {
                    EnsureBlankLine();
                }
                return;
            }

            if (!TopLevelStartTokens.Contains(tokenType))
            {
                return;
            }

            var previous = _lastTopLevelKeyword;
            _pendingTopLevelBlankLine = false;

            if (previous == BondLexer.IMPORT && tokenType == BondLexer.IMPORT)
            {
                return;
            }
            if (previous == BondLexer.USING && tokenType == BondLexer.USING)
            {
                return;
            }

            EnsureBlankLine();
        }

        private void UpdateTopLevelKeyword(int tokenType)
        {
            if (_indentLevel != 0)
            {
                return;
            }

            if (TopLevelStartTokens.Contains(tokenType))
            {
                _lastTopLevelKeyword = tokenType;
            }
        }

        private bool ShouldSetPendingSpace(int type)
        {
            if (type == BondLexer.SEMI || type == BondLexer.LBRACE || type == BondLexer.RBRACE)
            {
                return false;
            }

            return !NoSpaceAfter.Contains(type);
        }

        private bool IsEmptyBlock(IToken token, int nextType)
        {
            if (nextType != BondLexer.RBRACE)
            {
                return false;
            }

            var hidden = _tokenStream.GetHiddenTokensToRight(token.TokenIndex);
            if (hidden == null)
            {
                return true;
            }

            foreach (var ht in hidden)
            {
                if (ht.Type == BondLexer.LINE_COMMENT || ht.Type == BondLexer.COMMENT)
                {
                    return false;
                }
            }

            return true;
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

        private static bool StartsTypeHeader(int tokenType)
        {
            return tokenType is BondLexer.STRUCT or BondLexer.SERVICE;
        }

        private static int? GetBlockKind(int tokenType)
        {
            return tokenType switch
            {
                BondLexer.STRUCT => BondLexer.STRUCT,
                BondLexer.ENUM => BondLexer.ENUM,
                BondLexer.SERVICE => BondLexer.SERVICE,
                BondLexer.VIEW_OF => BondLexer.VIEW_OF,
                _ => null
            };
        }

        private bool IsInsideEnumBlock()
        {
            return _blockStack.Count > 0 && _blockStack.Peek() == BondLexer.ENUM;
        }
    }
}
