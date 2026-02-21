using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Bond.Parser.Parser;
using Bond.Parser.Formatting;
using Bond.Parser.Compatibility;
using Bond.Parser.Json;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace Bond.Parser.CLI;

public static class Program
{
    static async Task<int> Main(string[] args)
    {
        if (args.Length == 0 || args.Contains("--help") || args.Contains("-h"))
        {
            ShowHelp();
            return args.Length == 0 ? 1 : 0;
        }

        var command = args[0].ToLower();

        return command switch
        {
            "breaking" => await RunBreakingCommand(args[1..]),
            "parse" => await RunParseCommand(args[1..]),
            "fmt" => await RunFormatCommand(args[1..]),
            "format" => await RunFormatCommand(args[1..]),
            _ => await RunParseCommand(args) // Default to parse for backward compatibility
        };
    }

    static void ShowHelp()
    {
        Console.WriteLine("Bond Schema Compiler - Parses and validates Bond IDL files");
        Console.WriteLine();
        Console.WriteLine("Usage:");
        Console.WriteLine("  bbc parse <file.bond> [options]");
        Console.WriteLine("  bbc breaking <file.bond> --against <reference> [options]");
        Console.WriteLine();
        Console.WriteLine("Commands:");
        Console.WriteLine("  parse       Parse and validate a Bond schema file");
        Console.WriteLine("  breaking    Check for breaking changes against a reference schema");
        Console.WriteLine("  format      Format a Bond schema file");
        Console.WriteLine();
        Console.WriteLine("Parse Options:");
        Console.WriteLine("  -v, --verbose              Show detailed AST output");
        Console.WriteLine("  --json                     Output AST as JSON (Bond schema format)");
        Console.WriteLine("  --ignore-imports           Parse without resolving imports or types");
        Console.WriteLine();
        Console.WriteLine("Breaking Options:");
        Console.WriteLine("  --against <reference>      Reference schema to compare against (file path or .git#branch=name)");
        Console.WriteLine("  --error-format <format>    Output format: text, json (default: text)");
        Console.WriteLine("  --ignore-imports           Compare without resolving imports or types");
        Console.WriteLine();
        Console.WriteLine("Format Options:");
        Console.WriteLine("  --check                    Exit non-zero if formatting is needed");
        Console.WriteLine();
        Console.WriteLine("Examples:");
        Console.WriteLine("  bbc parse schema.bond");
        Console.WriteLine("  bbc breaking schema.bond --against schema_v1.bond");
        Console.WriteLine("  bbc breaking schema.bond --against .git#branch=main --error-format=json");
        Console.WriteLine("  bbc format schema.bond");
        Console.WriteLine();
        Console.WriteLine("Global Options:");
        Console.WriteLine("  -h, --help                 Show this help message");
    }

    static async Task<int> RunParseCommand(string[] args)
    {
        if (args.Length == 0)
        {
            WriteError("Error: No file specified");
            ShowHelp();
            return 1;
        }

        var filePath = args[0];
        var verbose = args.Contains("--verbose") || args.Contains("-v");
        var jsonOutput = args.Contains("--json");
        var ignoreImports = args.Contains("--ignore-imports");

        var parseOptions = new ParseOptions(IgnoreImports: ignoreImports);
        var result = await ParserFacade.ParseFileAsync(filePath, options: parseOptions);

        if (!result.Success)
        {
            Console.Error.WriteLine($"parse failed: {filePath}");
            foreach (var error in result.Errors)
            {
                Console.Error.WriteLine($"{error.Line}:{error.Column}: {error.Message}");
                if (error.FilePath != null)
                {
                    Console.Error.WriteLine($"  in {error.FilePath}");
                }
            }
            return 1;
        }

        if (result.Ast != null)
        {
            if (jsonOutput)
            {
                PrintJson(result.Ast);
            }
            else
            {
                PrintSummary(result.Ast, filePath, verbose);
            }
        }

        return 0;
    }

    static async Task<int> RunBreakingCommand(string[] args)
    {
        if (args.Length == 0)
        {
            WriteError("Error: No file specified");
            ShowHelp();
            return 1;
        }

        var filePath = args[0];
        var againstIndex = Array.FindIndex(args, a => a == "--against");
        var formatIndex = Array.FindIndex(args, a => a.StartsWith("--error-format"));
        var verbose = args.Contains("-v") || args.Contains("--verbose");
        var ignoreImports = args.Contains("--ignore-imports");

        if (againstIndex < 0 || againstIndex + 1 >= args.Length)
        {
            WriteError("Error: --against flag is required for breaking command");
            ShowHelp();
            return 1;
        }

        var against = args[againstIndex + 1];
        var errorFormat = "text";

        if (formatIndex >= 0)
        {
            if (args[formatIndex].Contains('='))
            {
                errorFormat = args[formatIndex].Split('=')[1];
            }
            else if (formatIndex + 1 < args.Length)
            {
                errorFormat = args[formatIndex + 1];
            }
        }

        var reference = await ResolveReference(against, filePath);
        if (reference == null)
        {
            WriteError($"Error: Could not resolve reference: {against}");
            return 1;
        }

        return await CheckBreaking(reference, filePath, errorFormat, verbose, ignoreImports);
    }

    static async Task<int> RunFormatCommand(string[] args)
    {
        if (args.Length == 0)
        {
            WriteError("Error: No file specified");
            ShowHelp();
            return 1;
        }

        var filePath = args[0];
        var check = args.Contains("--check");

        if (!File.Exists(filePath))
        {
            WriteError($"Error: File not found: {filePath}");
            return 1;
        }

        var content = await File.ReadAllTextAsync(filePath);
        var result = BondFormatter.Format(content, Path.GetFullPath(filePath));

        if (!result.Success)
        {
            Console.Error.WriteLine($"format failed: {filePath}");
            foreach (var error in result.Errors)
            {
                Console.Error.WriteLine($"{error.Line}:{error.Column}: {error.Message}");
                if (error.FilePath != null)
                {
                    Console.Error.WriteLine($"  in {error.FilePath}");
                }
            }
            return 1;
        }

        if (result.FormattedText == null)
        {
            WriteError("Error: Format produced no output");
            return 1;
        }

        if (check)
        {
            if (!string.Equals(content, result.FormattedText, StringComparison.Ordinal))
            {
                Console.Error.WriteLine($"{filePath} would be reformatted");
                return 1;
            }
            return 0;
        }

        if (!string.Equals(content, result.FormattedText, StringComparison.Ordinal))
        {
            await File.WriteAllTextAsync(filePath, result.FormattedText);
        }

        return 0;
    }

    private sealed record ResolvedReference(
        string FilePath,
        string? Content,
        ImportResolver? ImportResolver);

    static async Task<ResolvedReference?> ResolveReference(string reference, string currentFilePath)
    {
        if (reference.StartsWith(".git#"))
        {
            return await ResolveGitReference(reference, currentFilePath);
        }

        if (File.Exists(reference))
        {
            return new ResolvedReference(Path.GetFullPath(reference), null, null);
        }

        return null;
    }

    static async Task<ResolvedReference?> ResolveGitReference(string gitRef, string currentFilePath)
    {
        var parts = gitRef.Split('#');
        if (parts.Length != 2)
        {
            return null;
        }

        var refParts = parts[1].Split('=');
        if (refParts.Length != 2)
        {
            return null;
        }

        var refName = refParts[1];

        try
        {
            var gitRoot = await RunGitCommand("rev-parse --show-toplevel");
            if (gitRoot == null)
            {
                return null;
            }

            var fullPath = Path.GetFullPath(currentFilePath);
            var gitRelativePath = Path.GetRelativePath(gitRoot, fullPath).Replace('\\', '/');
            if (gitRelativePath.StartsWith("..") || Path.IsPathRooted(gitRelativePath))
            {
                return null;
            }

            var content = await RunGitCommand($"show {refName}:{gitRelativePath}", gitRoot);
            if (content == null)
            {
                return null;
            }

            var virtualPath = Path.GetFullPath(Path.Combine(gitRoot, gitRelativePath));
            var importResolver = CreateGitAwareImportResolver(gitRoot, refName);
            return new ResolvedReference(virtualPath, content, importResolver);
        }
        catch
        {
            return null;
        }
    }

    static async Task<string?> RunGitCommand(string arguments, string? workingDirectory = null)
    {
        var process = new System.Diagnostics.Process
        {
            StartInfo = new System.Diagnostics.ProcessStartInfo
            {
                FileName = "git",
                Arguments = arguments,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                WorkingDirectory = workingDirectory ?? Environment.CurrentDirectory,
                CreateNoWindow = true
            }
        };

        process.Start();
        var output = await process.StandardOutput.ReadToEndAsync();
        await process.WaitForExitAsync();

        return process.ExitCode == 0 ? output.Trim() : null;
    }

    static async Task<int> CheckBreaking(ResolvedReference oldSchema, string newFilePath, string errorFormat, bool verbose, bool ignoreImports)
    {
        var parseOptions = new ParseOptions(IgnoreImports: ignoreImports);
        ParseResult oldResult;
        if (oldSchema.Content != null)
        {
            oldResult = await ParserFacade.ParseContentAsync(
                oldSchema.Content,
                oldSchema.FilePath,
                oldSchema.ImportResolver,
                parseOptions);
        }
        else
        {
            oldResult = await ParserFacade.ParseFileAsync(
                oldSchema.FilePath,
                oldSchema.ImportResolver,
                CancellationToken.None,
                parseOptions);
        }
        if (!oldResult.Success)
        {
            return OutputParseError(errorFormat, oldResult.Errors, "Failed to parse reference schema", oldSchema.FilePath);
        }

        var newResult = await ParserFacade.ParseFileAsync(newFilePath, options: parseOptions);
        if (!newResult.Success)
        {
            return OutputParseError(errorFormat, newResult.Errors, "Failed to parse current schema", newFilePath);
        }

        var checker = new CompatibilityChecker();
        var changes = checker.CheckCompatibility(oldResult.Ast!, newResult.Ast!);
        var breaking = changes.Where(c => c.Category is ChangeCategory.BreakingWire or ChangeCategory.BreakingText).ToList();

        if (errorFormat == "json")
        {
            OutputJsonBreaking(changes, breaking.Any());
            return breaking.Any() ? 1 : 0;
        }

        if (breaking.Any())
        {
            foreach (var change in breaking)
            {
                Console.Error.WriteLine($"{change.Location}: {change.Description}");
            }
            return 1;
        }

        if (verbose)
        {
            foreach (var change in changes)
            {
                Console.WriteLine($"{change.Category}: {change.Location}: {change.Description}");
            }
        }
        return 0;
    }

    static ImportResolver CreateGitAwareImportResolver(string gitRoot, string refName)
    {
        return async (currentFile, importPath) =>
        {
            var currentDir = Path.GetDirectoryName(currentFile) ?? gitRoot;
            var absolutePath = Path.GetFullPath(Path.Combine(currentDir, importPath));

            var relativePath = Path.GetRelativePath(gitRoot, absolutePath).Replace('\\', '/');
            var inRepo = !relativePath.StartsWith("..") && !Path.IsPathRooted(relativePath);
            if (inRepo)
            {
                var contentFromGit = await RunGitCommand($"show {refName}:{relativePath}", gitRoot);
                if (contentFromGit != null)
                {
                    return (absolutePath, contentFromGit);
                }
            }

            if (File.Exists(absolutePath))
            {
                var content = await File.ReadAllTextAsync(absolutePath);
                return (absolutePath, content);
            }

            throw new FileNotFoundException($"Imported file not found: {importPath}", absolutePath);
        };
    }

    static int OutputParseError(string errorFormat, IReadOnlyList<ParseError> errors, string message, string filePath)
    {
        if (errorFormat == "json")
        {
            OutputJsonErrors("parse_error", errors, message);
        }
        else
        {
            WriteError($"{message}: {filePath}");
            foreach (var error in errors)
            {
                Console.WriteLine($"  {error.Message}");
            }
        }
        return 1;
    }

    static void WriteError(string message)
    {
        Console.ForegroundColor = ConsoleColor.Red;
        Console.Error.WriteLine(message);
        Console.ResetColor();
    }

    static void OutputJson(object output)
    {
        Console.WriteLine(JsonSerializer.Serialize(output, new JsonSerializerOptions { WriteIndented = true }));
    }

    static void OutputJsonErrors(string errorType, IReadOnlyList<ParseError> errors, string message)
    {
        OutputJson(new
        {
            error = errorType,
            message,
            errors = errors.Select(e => new
            {
                line = e.Line,
                column = e.Column,
                message = e.Message,
                file = e.FilePath
            })
        });
    }

    static void OutputJsonBreaking(List<SchemaChange> changes, bool hasBreaking)
    {
        static string CategoryToString(ChangeCategory cat) => cat switch
        {
            ChangeCategory.BreakingWire => "breaking_wire",
            ChangeCategory.BreakingText => "breaking_text",
            _                           => "compatible",
        };

        OutputJson(new
        {
            changes = changes.Select(c => new
            {
                type           = CategoryToString(c.Category),
                location       = c.Location,
                description    = c.Description,
                recommendation = c.Recommendation
            }).ToArray()
        });
    }

    static void PrintJson(Bond.Parser.Syntax.Bond ast)
    {
        var options = BondJsonSerializerOptions.GetOptions();
        var json = JsonSerializer.Serialize(ast, options);
        Console.WriteLine(json);
    }

    static void PrintSummary(Bond.Parser.Syntax.Bond ast, string filePath, bool verbose)
    {
        Console.WriteLine($"parse: {filePath}");

        foreach (var ns in ast.Namespaces)
        {
            Console.WriteLine(ns);
        }

        foreach (var import in ast.Imports)
        {
            Console.WriteLine($"import {import.FilePath}");
        }

        foreach (var decl in ast.Declarations)
        {
            Console.WriteLine($"{decl.Kind.ToLower()} {decl.Name}");

            if (verbose)
            {
                PrintDeclarationDetails(decl);
            }
        }
    }

    static void PrintDeclarationDetails(Syntax.Declaration decl)
    {
        switch (decl)
        {
            case Syntax.StructDeclaration structDecl:
                foreach (var field in structDecl.Fields)
                {
                    Console.WriteLine($"  {field.Ordinal}: {field.Modifier} {field.Type} {field.Name}");
                }
                break;

            case Syntax.EnumDeclaration enumDecl:
                foreach (var constant in enumDecl.Constants)
                {
                    Console.WriteLine($"  {constant}");
                }
                break;

            case Syntax.ServiceDeclaration serviceDecl:
                foreach (var method in serviceDecl.Methods)
                {
                    Console.WriteLine($"  {method}");
                }
                break;

            case Syntax.AliasDeclaration aliasDecl:
                Console.WriteLine($"  = {aliasDecl.AliasedType}");
                break;
        }
    }
}
