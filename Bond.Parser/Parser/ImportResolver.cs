namespace Bond.Parser.Parser;

/// <summary>
/// Delegate for resolving and loading imported Bond files
/// </summary>
/// <param name="currentFile">Path of the file containing the import statement</param>
/// <param name="importPath">Relative path specified in the import statement</param>
/// <returns>Tuple of (canonical absolute path, file content)</returns>
public delegate Task<(string canonicalPath, string content)> ImportResolver(
    string currentFile,
    string importPath
);

/// <summary>
/// Default import resolver implementation
/// </summary>
public static class DefaultImportResolver
{
    public static async Task<(string canonicalPath, string content)> Resolve(
        string currentFile,
        string importPath)
    {
        // Resolve relative to the directory of the current file
        var currentDir = Path.GetDirectoryName(currentFile) ?? Directory.GetCurrentDirectory();
        var absolutePath = Path.GetFullPath(Path.Combine(currentDir, importPath));

        if (!File.Exists(absolutePath))
        {
            throw new FileNotFoundException($"Imported file not found: {importPath}", absolutePath);
        }

        var content = await File.ReadAllTextAsync(absolutePath);
        return (absolutePath, content);
    }
}
