using System.Text.RegularExpressions;
using MineEngine.Core.Diagnostics;
using MineEngine.Minecraft.Generation;

namespace MineEngine.Build.Gradle;

/// <summary>
/// Repère les erreurs et avertissements de javac dans la sortie de Gradle
/// ("Fichier.java:25: error: ...", ou "erreur :" avec un JDK en français)
/// et les rattache, grâce à la <see cref="SourceMap"/>, à l'asset qui a produit la ligne.
/// </summary>
public sealed class CompilerErrorMapper
{
    private static readonly Regex JavacMessage = new(
        @"^(?<file>.+?\.java):(?<line>\d+):\s*(?<kind>error|erreur|warning|avertissement)\s*:\s*(?<message>.+)$",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    public IReadOnlyList<Diagnostic> Map(IEnumerable<string> outputLines, string workspaceDirectory, SourceMap sourceMap)
    {
        ArgumentNullException.ThrowIfNull(outputLines);
        ArgumentNullException.ThrowIfNull(sourceMap);
        string workspace = Path.GetFullPath(workspaceDirectory);

        var diagnostics = new List<Diagnostic>();
        foreach (string line in outputLines)
        {
            Match match = JavacMessage.Match(line.Trim());
            if (!match.Success)
            {
                continue;
            }

            string file = match.Groups["file"].Value;
            int lineNumber = int.Parse(match.Groups["line"].Value);
            string relative = ToRelative(file, workspace);
            DiagnosticSeverity severity = match.Groups["kind"].Value.StartsWith("e", StringComparison.OrdinalIgnoreCase)
                ? DiagnosticSeverity.Error
                : DiagnosticSeverity.Warning;

            SourceMapEntry? entry = sourceMap.Find(relative, lineNumber);
            string message = "Java : " + match.Groups["message"].Value.Trim();
            diagnostics.Add(entry is null
                ? new Diagnostic(severity, message, $"{relative}:{lineNumber}")
                : new Diagnostic(severity, message, entry.Label, entry.AssetId));
        }

        return diagnostics;
    }

    private static string ToRelative(string file, string workspace)
    {
        try
        {
            string full = Path.GetFullPath(file);
            return full.StartsWith(workspace, StringComparison.OrdinalIgnoreCase)
                ? Path.GetRelativePath(workspace, full).Replace('\\', '/')
                : file.Replace('\\', '/');
        }
        catch (ArgumentException)
        {
            return file.Replace('\\', '/');
        }
    }
}
