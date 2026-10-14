using System.Text.RegularExpressions;
using System.Windows.Controls;
using System.Windows.Media;
using ICSharpCode.AvalonEdit.CodeCompletion;
using ICSharpCode.AvalonEdit.Document;
using ICSharpCode.AvalonEdit.Editing;
using MineEngine.Minecraft.Java;

namespace MineEngine.Editor.Views.Documents;

/// <summary>
/// Une ligne de la liste d'auto-complétion. Accepter un type ajoute son import s'il manque ;
/// un extrait sur plusieurs lignes reprend l'indentation de la ligne courante.
/// </summary>
public sealed partial class JavaCompletionItem : ICompletionData
{
    private readonly JavaCompletionEntry _entry;

    public JavaCompletionItem(JavaCompletionEntry entry)
    {
        _entry = entry;
    }

    public ImageSource? Image => null;

    public string Text => _entry.Label;

    /// <summary>Glyphe selon la sorte d'entrée, puis le nom.</summary>
    public object Content => new StackPanel
    {
        Orientation = Orientation.Horizontal,
        Children =
        {
            new TextBlock
            {
                Text = _entry.Kind switch
                {
                    JavaCompletionKind.Type => "",
                    JavaCompletionKind.Snippet => "",
                    _ => "",
                },
                FontFamily = new FontFamily("Segoe Fluent Icons, Segoe MDL2 Assets"),
                FontSize = 11,
                Width = 18,
                VerticalAlignment = System.Windows.VerticalAlignment.Center,
                Opacity = 0.7,
            },
            new TextBlock { Text = _entry.Label },
        },
    };

    public object Description => _entry.Description;

    public double Priority => _entry.Kind switch
    {
        JavaCompletionKind.Snippet => 2,
        JavaCompletionKind.Type => 1,
        _ => 0,
    };

    public void Complete(TextArea textArea, ISegment completionSegment, EventArgs insertionRequestEventArgs)
    {
        TextDocument document = textArea.Document;
        DocumentLine line = document.GetLineByOffset(completionSegment.Offset);
        string lineText = document.GetText(line);
        string indent = lineText[..(lineText.Length - lineText.TrimStart().Length)];
        string text = _entry.InsertText.Replace("\n", "\n" + indent, StringComparison.Ordinal);

        using (document.RunUpdate())
        {
            document.Replace(completionSegment, text);
            if (_entry.Import is { } import)
            {
                AddImport(document, import);
            }
        }
    }

    /// <summary>Ajoute "import x.y.Z;" après les imports existants (ou après le package), sauf s'il est inutile.</summary>
    private static void AddImport(TextDocument document, string import)
    {
        string text = document.Text;
        string package = import[..import.LastIndexOf('.')];
        Match ownPackage = PackageLine().Match(text);
        if (package == "java.lang"
            || (ownPackage.Success && ownPackage.Groups[1].Value == package)
            || Regex.IsMatch(text, $@"^\s*import\s+{Regex.Escape(import)}\s*;", RegexOptions.Multiline)
            || Regex.IsMatch(text, $@"^\s*import\s+{Regex.Escape(package)}\.\*\s*;", RegexOptions.Multiline))
        {
            return;
        }

        MatchCollection imports = ImportLine().Matches(text);
        if (imports.Count > 0)
        {
            Match last = imports[^1];
            document.Insert(last.Index + last.Length, $"\nimport {import};");
        }
        else if (ownPackage.Success)
        {
            document.Insert(ownPackage.Index + ownPackage.Length, $"\n\nimport {import};");
        }
        else
        {
            document.Insert(0, $"import {import};\n\n");
        }
    }

    [GeneratedRegex(@"^\s*package\s+([\w.]+)\s*;", RegexOptions.Multiline)]
    private static partial Regex PackageLine();

    [GeneratedRegex(@"^\s*import\s+[\w.*]+\s*;", RegexOptions.Multiline)]
    private static partial Regex ImportLine();
}
