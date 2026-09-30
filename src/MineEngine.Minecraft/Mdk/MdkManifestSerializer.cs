using System.Globalization;
using System.Text.Json.Nodes;
using MineEngine.Core.Json;

namespace MineEngine.Minecraft.Mdk;

/// <summary>Fichier mdk.json enregistré dans le dossier de chaque MDK installé.</summary>
public sealed class MdkManifestSerializer
{
    public const string FileName = "mdk.json";

    public string Serialize(MdkDescriptor mdk)
    {
        var root = new JsonObject
        {
            ["id"] = mdk.Id,
            ["loader"] = mdk.Loader.ToId(),
            ["minecraftVersion"] = mdk.MinecraftVersion.Id,
            ["loaderVersion"] = mdk.LoaderVersion,
            ["javaVersion"] = mdk.JavaVersion,
            ["gradleVersion"] = mdk.GradleVersion,
            ["source"] = mdk.Source,
            ["installedAt"] = mdk.InstalledAt.ToString("O", CultureInfo.InvariantCulture),
        };
        return JsonFormatting.ToText(root);
    }

    public MdkDescriptor Deserialize(string json, string directory)
    {
        JsonObject root = JsonFormatting.ParseObject(json, FileName);

        string id = root["id"]?.GetValue<string>()
            ?? throw new InvalidDataException($"{FileName} : la propriété 'id' est obligatoire.");
        string versionText = root["minecraftVersion"]?.GetValue<string>() ?? string.Empty;
        if (!MinecraftVersion.TryParse(versionText, out MinecraftVersion? minecraftVersion))
        {
            throw new InvalidDataException($"{FileName} : version de Minecraft '{versionText}' invalide.");
        }

        DateTime installedAt = DateTime.TryParse(
            root["installedAt"]?.GetValue<string>(), CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out DateTime parsed)
            ? parsed
            : DateTime.MinValue;

        return new MdkDescriptor(
            id,
            ModLoaderKindExtensions.FromId(root["loader"]?.GetValue<string>()),
            minecraftVersion!,
            root["loaderVersion"]?.GetValue<string>() ?? string.Empty,
            root["javaVersion"]?.GetValue<int>() ?? minecraftVersion!.RequiredJavaVersion,
            root["gradleVersion"]?.GetValue<string>(),
            directory,
            root["source"]?.GetValue<string>() ?? string.Empty,
            installedAt);
    }
}
