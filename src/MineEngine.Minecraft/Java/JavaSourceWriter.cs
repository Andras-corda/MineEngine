using System.Text;

namespace MineEngine.Minecraft.Java;

/// <summary>Écriture d'un fichier source Java avec gestion de l'indentation.</summary>
public sealed class JavaSourceWriter
{
    private const string IndentUnit = "    ";

    private readonly StringBuilder _builder = new();
    private int _indentLevel;
    private int _lineCount;

    /// <summary>Numéro (à partir de 1) de la prochaine ligne écrite.</summary>
    public int NextLineNumber => _lineCount + 1;

    public JavaSourceWriter Line(string text = "")
    {
        if (text.Length > 0)
        {
            for (int i = 0; i < _indentLevel; i++)
            {
                _builder.Append(IndentUnit);
            }

            _builder.Append(text);
        }

        _builder.Append('\n');
        _lineCount++;
        return this;
    }

    /// <summary>Écrit "<paramref name="header"/> {" puis augmente l'indentation.</summary>
    public JavaSourceWriter OpenBlock(string header)
    {
        Line(header + " {");
        _indentLevel++;
        return this;
    }

    /// <summary>Diminue l'indentation puis écrit "}" suivi de <paramref name="suffix"/>.</summary>
    public JavaSourceWriter CloseBlock(string suffix = "")
    {
        if (_indentLevel == 0)
        {
            throw new InvalidOperationException("Aucun bloc ouvert.");
        }

        _indentLevel--;
        return Line("}" + suffix);
    }

    public JavaSourceWriter Indent()
    {
        _indentLevel++;
        return this;
    }

    public JavaSourceWriter Unindent()
    {
        _indentLevel = Math.Max(0, _indentLevel - 1);
        return this;
    }

    public override string ToString() => _builder.ToString();
}
