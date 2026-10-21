using System.Globalization;
using MineEngine.Assets;
using MineEngine.Assets.Puppeteer;
using MineEngine.Core.Assets;

namespace MineEngine.Editor.ViewModels.Puppeteer;

/// <summary>Texts shown on the Puppeteer nodes: title of an attached piece and summary of the settings.</summary>
public static class BtText
{
    /// <summary>"Key is set", "Not: Key is set" for an inverted condition.</summary>
    public static string Title(BtNodeInfo info, Func<string, string> value) =>
        info.IsCondition && value(BtCatalog.Inverse) == "true" ? Tr.F("Not: {0}", info.Title) : info.Title;

    /// <summary>One line per setting ("Target: target"), without the inversion and the abort mode, shown apart.</summary>
    public static IReadOnlyList<string> Summary(BtNodeInfo info, Func<string, string> value, IReadOnlyList<BlackboardKey> keys, AssetRegistry assets)
    {
        var lines = new List<string>();
        foreach (BtParameterInfo parameter in info.Parameters.Where(p => p.Name is not (BtCatalog.Inverse or BtCatalog.Abort or BtCatalog.Interval)))
        {
            lines.Add(Tr.F("{0}: {1}", parameter.Label, Display(parameter, value(parameter.Name), keys, assets)));
        }

        if (info.Category == BtCategory.Service && BtCatalog.Find(info.Kind)?.FindParameter(BtCatalog.Interval) is { } interval)
        {
            lines.Add(Tr.F("Every {0} s", Display(interval, value(BtCatalog.Interval), keys, assets)));
        }

        return lines;
    }

    /// <summary>Readable abort mode of a condition, or null when there is none.</summary>
    public static string? AbortLabel(BtNodeInfo info, Func<string, string> value) =>
        info.IsCondition && Enum.TryParse(value(BtCatalog.Abort), out BtAbortMode mode) && mode != BtAbortMode.None
            ? mode switch
            {
                BtAbortMode.Self => Tr.T("Interrupts: self"),
                BtAbortMode.LowerPriority => Tr.T("Interrupts: lower priority"),
                _ => Tr.T("Interrupts: both"),
            }
            : null;

    public static string Display(BtParameterInfo parameter, string text, IReadOnlyList<BlackboardKey> keys, AssetRegistry assets)
    {
        switch (parameter.Type)
        {
            case BtParameterType.Number or BtParameterType.Integer:
                return parameter.ParseNumber(text) is { } number ? number.ToString("0.##", CultureInfo.CurrentCulture) : "?";
            case BtParameterType.Boolean:
                return text == "true" ? Tr.T("yes") : Tr.T("no");
            case BtParameterType.Choice:
                return parameter.Choices.FirstOrDefault(c => c.Value == text)?.Label ?? "?";
            case BtParameterType.Key:
                if (string.IsNullOrEmpty(text))
                {
                    return parameter.Optional ? Tr.T("(none)") : "—";
                }

                return Guid.TryParse(text, out Guid id) && keys.FirstOrDefault(k => k.Id == id) is { } key ? key.Name : Tr.T("(deleted key)");
            case BtParameterType.Goal:
                return Guid.TryParse(text, out Guid goal) && assets.Find(goal) is BehaviorTreeAsset tree
                    ? (string.IsNullOrWhiteSpace(tree.DisplayName) ? tree.ResourceId.Value : tree.DisplayName)
                    : "—";
            default:
                if (!ContentReference.TryParseStorageText(text, out ContentReference? reference))
                {
                    return "—";
                }

                return reference!.Kind == ContentReferenceKind.Asset
                    ? assets.Find(reference.AssetId)?.ResourceId.Value ?? Tr.T("(deleted)")
                    : reference.ToStorageText();
        }
    }

    /// <summary>Number entered by the user (comma or dot); null when it is invalid.</summary>
    public static double? ParseUserNumber(string? text)
    {
        string normalized = (text ?? string.Empty).Trim().Replace(',', '.');
        return double.TryParse(normalized, NumberStyles.Float, CultureInfo.InvariantCulture, out double value) && double.IsFinite(value) ? value : null;
    }

    /// <summary>Number shown in an input field (culture of the user).</summary>
    public static string FormatUserNumber(BtParameterInfo parameter, string text) =>
        parameter.ParseNumber(text) is { } number ? number.ToString("0.###", CultureInfo.CurrentCulture) : text;
}
