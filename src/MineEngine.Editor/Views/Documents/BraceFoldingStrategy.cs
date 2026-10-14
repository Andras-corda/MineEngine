using ICSharpCode.AvalonEdit.Document;
using ICSharpCode.AvalonEdit.Folding;

namespace MineEngine.Editor.Views.Documents;

/// <summary>
/// Blocs repliables d'un fichier Java ou JSON : chaque paire d'accolades (ou de crochets
/// en JSON) qui s'étend sur plusieurs lignes, ainsi que les commentaires /* */ longs.
/// Les accolades placées dans des chaînes ou des commentaires sont ignorées.
/// </summary>
public sealed class BraceFoldingStrategy
{
    private readonly char[] _openers;

    public BraceFoldingStrategy(bool includeBrackets)
    {
        _openers = includeBrackets ? ['{', '['] : ['{'];
    }

    public void UpdateFoldings(FoldingManager manager, TextDocument document) =>
        manager.UpdateFoldings(CreateFoldings(document), -1);

    public IEnumerable<NewFolding> CreateFoldings(TextDocument document)
    {
        var foldings = new List<NewFolding>();
        var open = new Stack<int>();
        string text = document.Text;

        for (int i = 0; i < text.Length; i++)
        {
            char c = text[i];
            char next = i + 1 < text.Length ? text[i + 1] : '\0';
            switch (c)
            {
                case '/' when next == '/':
                    i = EndOfLine(text, i);
                    break;
                case '/' when next == '*':
                    int end = text.IndexOf("*/", i + 2, StringComparison.Ordinal);
                    end = end < 0 ? text.Length : end + 2;
                    AddIfMultiline(document, foldings, i, end, "/* ... */");
                    i = end - 1;
                    break;
                case '"' or '\'':
                    i = EndOfLiteral(text, i, c);
                    break;
                default:
                    if (Array.IndexOf(_openers, c) >= 0)
                    {
                        open.Push(i);
                    }
                    else if ((c == '}' || (c == ']' && _openers.Length > 1)) && open.Count > 0)
                    {
                        int start = open.Pop();
                        AddIfMultiline(document, foldings, start, i + 1, text[start] == '{' ? "{ ... }" : "[ ... ]");
                    }

                    break;
            }
        }

        foldings.Sort((a, b) => a.StartOffset.CompareTo(b.StartOffset));
        return foldings;
    }

    private static void AddIfMultiline(TextDocument document, List<NewFolding> foldings, int start, int end, string name)
    {
        if (document.GetLineByOffset(start).LineNumber < document.GetLineByOffset(Math.Min(end, document.TextLength)).LineNumber)
        {
            foldings.Add(new NewFolding(start, end) { Name = name });
        }
    }

    private static int EndOfLine(string text, int index)
    {
        int end = text.IndexOf('\n', index);
        return end < 0 ? text.Length : end;
    }

    private static int EndOfLiteral(string text, int start, char quote)
    {
        for (int i = start + 1; i < text.Length; i++)
        {
            if (text[i] == '\\')
            {
                i++;
            }
            else if (text[i] == quote || text[i] == '\n')
            {
                return i;
            }
        }

        return text.Length;
    }
}
