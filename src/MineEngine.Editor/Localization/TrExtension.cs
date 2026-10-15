using System.Windows.Markup;

namespace MineEngine.Editor.Localization;

/// <summary>
/// Texte traduit dans le XAML : Text="{l:Tr 'Ouvrir le projet'}". Le texte est écrit en français
/// et remplacé par sa traduction dans la langue de l'interface (voir <see cref="Tr"/>).
/// </summary>
[MarkupExtensionReturnType(typeof(string))]
public sealed class TrExtension : MarkupExtension
{
    public TrExtension()
    {
    }

    public TrExtension(string text)
    {
        Text = text;
    }

    [ConstructorArgument("text")]
    public string Text { get; set; } = string.Empty;

    public override object ProvideValue(IServiceProvider serviceProvider) => Tr.T(Text);
}
