using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace MineEngine.Assets.Models;

/// <summary>Face of a Blockbench cube: its UV rectangle (in pixels of the model texture) and its texture index.</summary>
public sealed record BlockbenchFace(string Direction, double[] Uv, int? Texture, int Rotation);

/// <summary>A cube of a Blockbench model (coordinates in Blockbench units, Y up).</summary>
public sealed record BlockbenchCube(
    string Name,
    double[] From,
    double[] To,
    double[] Origin,
    double[] Rotation,
    double Inflate,
    bool BoxUv,
    int[] UvOffset,
    bool Mirror,
    IReadOnlyList<BlockbenchFace> Faces)
{
    public bool IsRotated => Rotation.Any(r => Math.Abs(r) > 1e-6);

    public double[] Size => [To[0] - From[0], To[1] - From[1], To[2] - From[2]];
}

/// <summary>A group (bone) of a Blockbench model: a pivot, a rotation, cubes and child groups.</summary>
public sealed record BlockbenchGroup(string Name, double[] Origin, double[] Rotation, IReadOnlyList<BlockbenchCube> Cubes, IReadOnlyList<BlockbenchGroup> Children)
{
    public IEnumerable<BlockbenchCube> AllCubes => Cubes.Concat(Children.SelectMany(c => c.AllCubes));
}

/// <summary>A texture of a Blockbench model; <see cref="Png"/> holds the image when it is embedded in the file.</summary>
public sealed record BlockbenchTexture(int Index, string Name, byte[]? Png, int UvWidth, int UvHeight)
{
    /// <summary>Name without the extension, usable as a texture identifier.</summary>
    public string Stem => Path.GetFileNameWithoutExtension(Name.Length > 0 ? Name : $"texture_{Index}");
}

/// <summary>
/// A Blockbench project (.bbmodel) read for Mine Engine: its cubes organized in groups (bones),
/// its textures (embedded images included) and its texture resolution. It converts to a
/// Minecraft Java JSON model (blocks and items) and gives the bones of a Java entity model (mobs).
/// </summary>
public sealed class BlockbenchModel
{
    private static readonly string[] Directions = ["north", "east", "south", "west", "up", "down"];
    private static readonly double[] JavaAngles = [-45, -22.5, 0, 22.5, 45];

    private BlockbenchModel(
        string name, string format, int width, int height, IReadOnlyList<BlockbenchGroup> groups,
        IReadOnlyList<BlockbenchTexture> textures, JsonObject? display, IReadOnlyList<string> skipped)
    {
        Name = name;
        Format = format;
        TextureWidth = width;
        TextureHeight = height;
        Groups = groups;
        Textures = textures;
        Display = display;
        SkippedElements = skipped;
    }

    public string Name { get; }

    /// <summary>Blockbench project type: "java_block", "modded_entity", "free", "bedrock"...</summary>
    public string Format { get; }

    public int TextureWidth { get; }

    public int TextureHeight { get; }

    /// <summary>Top-level groups; cubes outside any group are in a group named "bb_main" at the origin.</summary>
    public IReadOnlyList<BlockbenchGroup> Groups { get; }

    public IReadOnlyList<BlockbenchTexture> Textures { get; }

    /// <summary>Positions in hand, on the head, in the inventory... (Java block and item models only).</summary>
    public JsonObject? Display { get; }

    /// <summary>Elements that are not cubes (meshes, locators...) and are left out.</summary>
    public IReadOnlyList<string> SkippedElements { get; }

    public IEnumerable<BlockbenchCube> Cubes => Groups.SelectMany(g => g.AllCubes);

    public bool IsBlockFormat => Format == "java_block";

    /// <summary>True when every cube is mapped with box UV, which Java entity models require.</summary>
    public bool UsesBoxUv => Cubes.All(c => c.BoxUv);

    public static BlockbenchModel Load(string file) => Parse(File.ReadAllText(file), Path.GetFileNameWithoutExtension(file));

    public static BlockbenchModel Parse(string json, string fallbackName = "model")
    {
        JsonObject root = JsonNode.Parse(json) as JsonObject ?? throw new InvalidDataException(Tr.T("The Blockbench file is not a JSON object."));
        JsonObject meta = root["meta"] as JsonObject ?? throw new InvalidDataException(Tr.T("The Blockbench file has no \"meta\" section."));
        string format = Text(meta["model_format"]) ?? "free";
        bool defaultBoxUv = Bool(meta["box_uv"]) ?? !format.Equals("java_block", StringComparison.Ordinal);

        var textures = new List<BlockbenchTexture>();
        var textureKeys = new Dictionary<string, int>(StringComparer.Ordinal);
        if (root["textures"] is JsonArray textureArray)
        {
            foreach (JsonObject texture in textureArray.OfType<JsonObject>())
            {
                int index = textures.Count;
                string name = Text(texture["name"]) ?? $"texture_{index}";
                textures.Add(new BlockbenchTexture(
                    index, name, DecodeDataUri(Text(texture["source"])),
                    (int)(Number(texture["uv_width"]) ?? Number(texture["width"]) ?? 0),
                    (int)(Number(texture["uv_height"]) ?? Number(texture["height"]) ?? 0)));
                foreach (string? key in new[] { Text(texture["id"]), Text(texture["uuid"]) })
                {
                    if (key is not null)
                    {
                        textureKeys.TryAdd(key, index);
                    }
                }
            }
        }

        JsonObject? resolution = root["resolution"] as JsonObject;
        int width = (int)(Number(resolution?["width"]) ?? textures.FirstOrDefault()?.UvWidth ?? 16);
        int height = (int)(Number(resolution?["height"]) ?? textures.FirstOrDefault()?.UvHeight ?? 16);

        var cubes = new Dictionary<string, BlockbenchCube>(StringComparer.Ordinal);
        var skipped = new List<string>();
        if (root["elements"] is JsonArray elements)
        {
            foreach (JsonObject element in elements.OfType<JsonObject>())
            {
                string name = Text(element["name"]) ?? "cube";
                string type = Text(element["type"]) ?? "cube";
                if (type != "cube")
                {
                    skipped.Add(name);
                    continue;
                }

                if (Bool(element["export"]) == false)
                {
                    continue;
                }

                string uuid = Text(element["uuid"]) ?? Guid.NewGuid().ToString();
                cubes[uuid] = ReadCube(element, name, defaultBoxUv, textureKeys);
            }
        }

        var groups = new List<BlockbenchGroup>();
        var looseCubes = new List<BlockbenchCube>();
        if (root["outliner"] is JsonArray outliner)
        {
            foreach (JsonNode? node in outliner)
            {
                if (node is JsonObject group)
                {
                    if (ReadGroup(group, cubes) is { } read)
                    {
                        groups.Add(read);
                    }
                }
                else if (Text(node) is { } uuid && cubes.TryGetValue(uuid, out BlockbenchCube? cube))
                {
                    looseCubes.Add(cube);
                }
            }
        }
        else
        {
            // Old files without outliner: every cube at the root.
            looseCubes.AddRange(cubes.Values);
        }

        if (looseCubes.Count > 0)
        {
            groups.Insert(0, new BlockbenchGroup("bb_main", [0, 0, 0], [0, 0, 0], looseCubes, []));
        }

        string modelName = Text(root["name"]) is { Length: > 0 } n ? n : fallbackName;
        return new BlockbenchModel(modelName, format, Math.Max(1, width), Math.Max(1, height), groups, textures, root["display"]?.DeepClone() as JsonObject, skipped);
    }

    /// <summary>
    /// Minecraft Java JSON model of the cubes, for a block or an item. A Java model has no groups
    /// and turns each cube around a single axis by a multiple of 22.5 degrees: other rotations are
    /// rounded and reported. <paramref name="textureReference"/> gives the texture of each index.
    /// </summary>
    public JsonObject ToJavaModel(Func<BlockbenchTexture, string> textureReference, ICollection<string>? problems = null)
    {
        ArgumentNullException.ThrowIfNull(textureReference);

        // Models made for an entity are centered on the origin: they are moved to the middle of the block.
        double[] shift = IsBlockFormat ? [0, 0, 0] : [8, 0, 8];
        var elements = new JsonArray();
        foreach (BlockbenchCube cube in Cubes)
        {
            var element = new JsonObject
            {
                ["name"] = cube.Name,
                ["from"] = Vector(cube.From.Select((v, i) => Clamp(v + shift[i] - cube.Inflate, problems, cube.Name))),
                ["to"] = Vector(cube.To.Select((v, i) => Clamp(v + shift[i] + cube.Inflate, problems, cube.Name))),
            };

            if (cube.IsRotated && JavaRotation(cube, problems) is { } rotation)
            {
                rotation["origin"] = Vector(cube.Origin.Select((v, i) => v + shift[i]));
                element["rotation"] = rotation;
            }

            var faces = new JsonObject();
            foreach (BlockbenchFace face in FacesOf(cube))
            {
                if (face.Texture is not { } index || index < 0 || index >= Textures.Count)
                {
                    continue;
                }

                BlockbenchTexture texture = Textures[index];
                double uScale = 16.0 / (texture.UvWidth > 0 ? texture.UvWidth : TextureWidth);
                double vScale = 16.0 / (texture.UvHeight > 0 ? texture.UvHeight : TextureHeight);
                var javaFace = new JsonObject
                {
                    ["uv"] = Vector([face.Uv[0] * uScale, face.Uv[1] * vScale, face.Uv[2] * uScale, face.Uv[3] * vScale]),
                    ["texture"] = "#" + index.ToString(CultureInfo.InvariantCulture),
                };
                if (face.Rotation != 0)
                {
                    javaFace["rotation"] = face.Rotation;
                }

                faces[face.Direction] = javaFace;
            }

            element["faces"] = faces;
            elements.Add(element);
        }

        if (!IsBlockFormat && Groups.Any(HasRotatedGroup))
        {
            problems?.Add(Tr.T("Rotations of groups are lost in a block or item model (Java models only turn cubes)."));
        }

        var textures = new JsonObject();
        foreach (BlockbenchTexture texture in Textures)
        {
            textures[texture.Index.ToString(CultureInfo.InvariantCulture)] = textureReference(texture);
        }

        if (Textures.Count > 0)
        {
            textures["particle"] = textureReference(Textures[0]);
        }

        var model = new JsonObject
        {
            ["texture_size"] = Vector([TextureWidth, TextureHeight]),
            ["textures"] = textures,
            ["elements"] = elements,
        };
        if (Display is not null)
        {
            model["display"] = Display.DeepClone();
        }

        return model;
    }

    /// <summary>Faces of a cube; with box UV and no stored face, the UVs are laid out like Minecraft does.</summary>
    public static IReadOnlyList<BlockbenchFace> FacesOf(BlockbenchCube cube)
    {
        if (cube.Faces.Count > 0 || !cube.BoxUv)
        {
            return cube.Faces;
        }

        double w = Math.Floor(cube.Size[0]), h = Math.Floor(cube.Size[1]), d = Math.Floor(cube.Size[2]);
        double u = cube.UvOffset[0], v = cube.UvOffset[1];
        return
        [
            new("north", [u + d, v + d, u + d + w, v + d + h], 0, 0),
            new("east", [u, v + d, u + d, v + d + h], 0, 0),
            new("south", [u + 2 * d + w, v + d, u + 2 * d + 2 * w, v + d + h], 0, 0),
            new("west", [u + d + w, v + d, u + 2 * d + w, v + d + h], 0, 0),
            new("up", [u + d + w, v + d, u + d, v], 0, 0),
            new("down", [u + d + 2 * w, v, u + d + w, v + d], 0, 0),
        ];
    }

    private static bool HasRotatedGroup(BlockbenchGroup group) =>
        group.Rotation.Any(r => Math.Abs(r) > 1e-6) || group.Children.Any(HasRotatedGroup);

    private static JsonObject? JavaRotation(BlockbenchCube cube, ICollection<string>? problems)
    {
        int[] axes = [.. Enumerable.Range(0, 3).Where(i => Math.Abs(cube.Rotation[i]) > 1e-6)];
        int axis = axes.OrderByDescending(i => Math.Abs(cube.Rotation[i])).First();
        double angle = JavaAngles.MinBy(a => Math.Abs(a - cube.Rotation[axis]));
        if (axes.Length > 1 || Math.Abs(angle - cube.Rotation[axis]) > 1e-6)
        {
            problems?.Add(Tr.F("Cube \"{0}\": a Java model turns a cube around one axis by -45, -22.5, 22.5 or 45 degrees; its rotation was rounded.", cube.Name));
        }

        return angle == 0 ? null : new JsonObject { ["angle"] = angle, ["axis"] = "xyz"[axis].ToString() };
    }

    private static double Clamp(double value, ICollection<string>? problems, string cube)
    {
        if (value is >= -16 and <= 32)
        {
            return value;
        }

        problems?.Add(Tr.F("Cube \"{0}\" goes beyond the limits of a Java model (-16 to 32): it was cut.", cube));
        return Math.Clamp(value, -16, 32);
    }

    private static BlockbenchCube ReadCube(JsonObject element, string name, bool defaultBoxUv, Dictionary<string, int> textureKeys)
    {
        var faces = new List<BlockbenchFace>();
        if (element["faces"] is JsonObject faceObject)
        {
            foreach (string direction in Directions)
            {
                if (faceObject[direction] is JsonObject face && Doubles(face["uv"], 4) is { } uv)
                {
                    int? texture = face["texture"] switch
                    {
                        null => null,
                        JsonValue value when value.TryGetValue(out int index) => index,
                        JsonValue value when value.TryGetValue(out string? key) && key is not null && textureKeys.TryGetValue(key, out int index) => index,
                        _ => null,
                    };
                    faces.Add(new BlockbenchFace(direction, uv, texture, (int)(Number(face["rotation"]) ?? 0)));
                }
            }
        }

        double[] uvOffset = Doubles(element["uv_offset"], 2) ?? [0, 0];
        return new BlockbenchCube(
            name,
            Doubles(element["from"], 3) ?? [0, 0, 0],
            Doubles(element["to"], 3) ?? [0, 0, 0],
            Doubles(element["origin"], 3) ?? [0, 0, 0],
            Doubles(element["rotation"], 3) ?? [0, 0, 0],
            Number(element["inflate"]) ?? 0,
            Bool(element["box_uv"]) ?? defaultBoxUv,
            [(int)uvOffset[0], (int)uvOffset[1]],
            Bool(element["mirror_uv"]) ?? false,
            faces);
    }

    private static BlockbenchGroup? ReadGroup(JsonObject group, Dictionary<string, BlockbenchCube> cubes)
    {
        if (Bool(group["export"]) == false)
        {
            return null;
        }

        var groupCubes = new List<BlockbenchCube>();
        var children = new List<BlockbenchGroup>();
        if (group["children"] is JsonArray items)
        {
            foreach (JsonNode? child in items)
            {
                if (child is JsonObject childGroup)
                {
                    if (ReadGroup(childGroup, cubes) is { } read)
                    {
                        children.Add(read);
                    }
                }
                else if (Text(child) is { } uuid && cubes.TryGetValue(uuid, out BlockbenchCube? cube))
                {
                    groupCubes.Add(cube);
                }
            }
        }

        return new BlockbenchGroup(
            Text(group["name"]) ?? "group",
            Doubles(group["origin"], 3) ?? [0, 0, 0],
            Doubles(group["rotation"], 3) ?? [0, 0, 0],
            groupCubes,
            children);
    }

    private static byte[]? DecodeDataUri(string? source)
    {
        const string Prefix = "data:image/png;base64,";
        if (source is null || !source.StartsWith(Prefix, StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        try
        {
            return Convert.FromBase64String(source[Prefix.Length..]);
        }
        catch (FormatException)
        {
            return null;
        }
    }

    private static JsonArray Vector(IEnumerable<double> values) => [.. values.Select(v => (JsonNode)JsonValue.Create(Math.Round(v, 4)))];

    private static string? Text(JsonNode? node) => node is JsonValue value && value.TryGetValue(out string? text) ? text : null;

    private static bool? Bool(JsonNode? node) => node is JsonValue value && value.TryGetValue(out bool flag) ? flag : null;

    private static double? Number(JsonNode? node) =>
        node is JsonValue value && value.GetValueKind() == JsonValueKind.Number ? value.GetValue<double>() : null;

    private static double[]? Doubles(JsonNode? node, int count)
    {
        if (node is not JsonArray array || array.Count < count)
        {
            return null;
        }

        var values = new double[count];
        for (int i = 0; i < count; i++)
        {
            if (Number(array[i]) is not { } number)
            {
                return null;
            }

            values[i] = number;
        }

        return values;
    }
}
