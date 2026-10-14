using System.Text;

namespace MineEngine.Minecraft.Java;

/// <summary>
/// Mise en forme du code Java dans l'éditeur (bouton Formater) : chaque ligne est
/// réindentée selon les accolades, les lignes qui continuent une parenthèse ouverte
/// sont décalées de deux niveaux, le contenu des "case" d'un switch d'un niveau ;
/// les espaces de fin de ligne et les lignes vides en trop disparaissent. Le texte
/// des chaînes, des caractères et des commentaires n'est jamais modifié.
/// </summary>
public sealed class JavaFormatter
{
    private readonly string _indent;

    public JavaFormatter(int indentSize = 4)
    {
        _indent = new string(' ', Math.Clamp(indentSize, 1, 8));
    }

    public string Format(string source)
    {
        ArgumentNullException.ThrowIfNull(source);
        string[] lines = source.Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n');
        var output = new StringBuilder(source.Length + 64);
        var scanner = new Scanner();
        var caseBodies = new Stack<bool>();
        caseBodies.Push(false);
        int blankRun = 0;

        foreach (string rawLine in lines)
        {
            string line = rawLine.Trim();
            bool inBlockComment = scanner.InBlockComment;
            bool inTextBlock = scanner.InTextBlock;

            if (inTextBlock)
            {
                // Contenu d'un bloc de texte """ : recopié tel quel.
                output.Append(rawLine.TrimEnd()).Append('\n');
                scanner.Scan(rawLine);
                blankRun = 0;
                continue;
            }

            if (line.Length == 0)
            {
                if (++blankRun <= 1 && output.Length > 0)
                {
                    output.Append('\n');
                }

                continue;
            }

            blankRun = 0;
            int depth = scanner.BraceDepth;
            bool closesFirst = !inBlockComment && line.StartsWith('}');
            bool isCaseLabel = !inBlockComment && IsCaseLabel(line);
            if (closesFirst)
            {
                depth--;
                if (caseBodies.Count > 1)
                {
                    caseBodies.Pop();
                }
            }

            int level = Math.Max(0, depth);
            if (caseBodies.Peek() && !closesFirst && !isCaseLabel)
            {
                level++;
            }

            if (!inBlockComment && scanner.ParenDepth > 0)
            {
                level += 2;
            }

            string indent = string.Concat(Enumerable.Repeat(_indent, level));
            output.Append(indent);
            output.Append(inBlockComment && line.StartsWith('*') ? " " + line : line);
            output.Append('\n');

            int depthBefore = scanner.BraceDepth;
            scanner.Scan(line);
            if (closesFirst)
            {
                // L'accolade fermante a déjà été comptée ci-dessus pour la pile des switch.
                depthBefore--;
            }

            for (int opened = depthBefore; opened < scanner.BraceDepth; opened++)
            {
                caseBodies.Push(false);
            }

            for (int closed = scanner.BraceDepth; closed < depthBefore && caseBodies.Count > 1; closed++)
            {
                caseBodies.Pop();
            }

            if (isCaseLabel)
            {
                caseBodies.Pop();
                caseBodies.Push(true);
            }
        }

        string result = output.ToString().TrimEnd('\n');
        return result.Length == 0 ? string.Empty : result + "\n";
    }

    private static bool IsCaseLabel(string line) =>
        (line.StartsWith("case ", StringComparison.Ordinal) || line.StartsWith("default:", StringComparison.Ordinal)
                                                             || line.StartsWith("default :", StringComparison.Ordinal))
        && line.TrimEnd().EndsWith(':') && !line.Contains("->", StringComparison.Ordinal);

    /// <summary>Compte les accolades et parenthèses en ignorant chaînes, caractères et commentaires.</summary>
    private sealed class Scanner
    {
        public int BraceDepth { get; private set; }

        public int ParenDepth { get; private set; }

        public bool InBlockComment { get; private set; }

        public bool InTextBlock { get; private set; }

        public void Scan(string line)
        {
            for (int i = 0; i < line.Length; i++)
            {
                char c = line[i];
                char next = i + 1 < line.Length ? line[i + 1] : '\0';

                if (InBlockComment)
                {
                    if (c == '*' && next == '/')
                    {
                        InBlockComment = false;
                        i++;
                    }

                    continue;
                }

                if (InTextBlock)
                {
                    if (string.CompareOrdinal(line, i, "\"\"\"", 0, 3) == 0)
                    {
                        InTextBlock = false;
                        i += 2;
                    }

                    continue;
                }

                switch (c)
                {
                    case '/' when next == '/':
                        return;
                    case '/' when next == '*':
                        InBlockComment = true;
                        i++;
                        break;
                    case '"' when string.CompareOrdinal(line, i, "\"\"\"", 0, 3) == 0:
                        InTextBlock = true;
                        i += 2;
                        break;
                    case '"' or '\'':
                        i = SkipLiteral(line, i, c);
                        break;
                    case '{':
                        BraceDepth++;
                        break;
                    case '}':
                        BraceDepth = Math.Max(0, BraceDepth - 1);
                        break;
                    case '(':
                        ParenDepth++;
                        break;
                    case ')':
                        ParenDepth = Math.Max(0, ParenDepth - 1);
                        break;
                }
            }
        }

        private static int SkipLiteral(string line, int start, char quote)
        {
            for (int i = start + 1; i < line.Length; i++)
            {
                if (line[i] == '\\')
                {
                    i++;
                }
                else if (line[i] == quote)
                {
                    return i;
                }
            }

            return line.Length;
        }
    }
}
