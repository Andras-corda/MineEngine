using System.Globalization;
using MineEngine.Assets.Models;

namespace MineEngine.Minecraft.Java;

/// <summary>
/// Java entity model of a Blockbench project (what Blockbench exports as "Modded Entity"): one
/// ModelPart per group (bone), cubes in box UV, and a simple animation found from the names of
/// the bones: a "head" follows the look of the mob, "leg" and "arm" bones swing when it walks.
/// Blockbench has Y up and its origin on the ground; Java models have Y down and the ground at 24.
/// </summary>
public sealed class EntityModelJava
{
    private static readonly HashSet<string> Keywords =
    [
        "abstract", "boolean", "break", "byte", "case", "catch", "char", "class", "const", "continue", "default", "do", "double",
        "else", "enum", "extends", "final", "finally", "float", "for", "goto", "if", "implements", "import", "instanceof", "int",
        "interface", "long", "native", "new", "package", "private", "protected", "public", "return", "short", "static", "super",
        "switch", "this", "throw", "throws", "transient", "try", "void", "volatile", "while", "root", "mesh",
    ];

    private readonly BlockbenchModel _model;
    private readonly List<Bone> _bones = [];
    private readonly HashSet<string> _names = new(StringComparer.Ordinal);

    public EntityModelJava(BlockbenchModel model)
    {
        _model = model ?? throw new ArgumentNullException(nameof(model));
        foreach (BlockbenchGroup group in model.Groups)
        {
            AddBone(group, null);
        }
    }

    /// <summary>Imports of the model class.</summary>
    public static IReadOnlyList<string> Imports =>
    [
        "com.mojang.blaze3d.vertex.PoseStack",
        "com.mojang.blaze3d.vertex.VertexConsumer",
        "net.minecraft.client.model.EntityModel",
        "net.minecraft.client.model.geom.ModelLayerLocation",
        "net.minecraft.client.model.geom.ModelPart",
        "net.minecraft.client.model.geom.PartPose",
        "net.minecraft.client.model.geom.builders.CubeDeformation",
        "net.minecraft.client.model.geom.builders.CubeListBuilder",
        "net.minecraft.client.model.geom.builders.LayerDefinition",
        "net.minecraft.client.model.geom.builders.MeshDefinition",
        "net.minecraft.client.model.geom.builders.PartDefinition",
        "net.minecraft.resources.ResourceLocation",
        "net.minecraft.util.Mth",
        "net.minecraft.world.entity.Entity",
    ];

    /// <summary>Writes the body of the class (after the header).</summary>
    /// <param name="layerLocation">Java expression of the ResourceLocation of the model layer.</param>
    /// <param name="packedColor">True from 1.21: renderToBuffer takes one packed color instead of four floats.</param>
    public void WriteClass(JavaSourceWriter w, string className, string layerLocation, bool packedColor)
    {
        w.OpenBlock($"public class {className}<T extends Entity> extends EntityModel<T>")
            .Line($"public static final ModelLayerLocation LAYER_LOCATION = new ModelLayerLocation({layerLocation}, \"main\");")
            .Line()
            .Line("private final ModelPart root;");
        foreach (Bone bone in _bones)
        {
            w.Line($"private final ModelPart {bone.Name};");
        }

        w.Line().OpenBlock($"public {className}(ModelPart root)").Line("this.root = root;");
        foreach (Bone bone in _bones)
        {
            string parent = bone.Parent is null ? "root" : $"this.{bone.Parent.Name}";
            w.Line($"this.{bone.Name} = {parent}.getChild(\"{bone.Name}\");");
        }

        w.CloseBlock();

        WriteBodyLayer(w);
        WriteAnimation(w);

        w.Line().Line("@Override");
        if (packedColor)
        {
            w.OpenBlock("public void renderToBuffer(PoseStack poseStack, VertexConsumer vertexConsumer, int packedLight, int packedOverlay, int color)")
                .Line("this.root.render(poseStack, vertexConsumer, packedLight, packedOverlay, color);");
        }
        else
        {
            w.OpenBlock("public void renderToBuffer(PoseStack poseStack, VertexConsumer vertexConsumer, int packedLight, int packedOverlay, float red, float green, float blue, float alpha)")
                .Line("this.root.render(poseStack, vertexConsumer, packedLight, packedOverlay, red, green, blue, alpha);");
        }

        w.CloseBlock().CloseBlock();
    }

    private void WriteBodyLayer(JavaSourceWriter w)
    {
        w.Line().OpenBlock("public static LayerDefinition createBodyLayer()")
            .Line("MeshDefinition mesh = new MeshDefinition();")
            .Line("PartDefinition root = mesh.getRoot();");

        foreach (Bone bone in _bones)
        {
            BlockbenchGroup group = bone.Group;

            // The pivot of a top-level bone is placed from the ground (24); a child bone from its parent.
            double[] parentOrigin = bone.Parent?.Group.Origin ?? [0, 24, 0];
            double[] offset = bone.Parent is null
                ? [-group.Origin[0], 24 - group.Origin[1], group.Origin[2]]
                : Relative(group.Origin, parentOrigin);
            string cubes = string.Concat(group.Cubes.Where(c => !c.IsRotated).Select(c => Box(c, group.Origin)));
            string parent = bone.Parent is null ? "root" : bone.Parent.Name;
            w.Line($"PartDefinition {bone.Name} = {parent}.addOrReplaceChild(\"{bone.Name}\", CubeListBuilder.create(){cubes}, {Pose(offset, group.Rotation)});");

            // A Java part cannot turn one cube: a turned cube gets its own part, pivoting around the cube origin.
            int rotated = 0;
            foreach (BlockbenchCube cube in group.Cubes.Where(c => c.IsRotated))
            {
                string name = $"{bone.Name}_r{++rotated}";
                w.Line($"{bone.Name}.addOrReplaceChild(\"{name}\", CubeListBuilder.create(){Box(cube, cube.Origin)}, {Pose(Relative(cube.Origin, group.Origin), cube.Rotation)});");
            }
        }

        w.Line($"return LayerDefinition.create(mesh, {_model.TextureWidth}, {_model.TextureHeight});")
            .CloseBlock();
    }

    private void WriteAnimation(JavaSourceWriter w)
    {
        w.Line().Line("@Override")
            .OpenBlock("public void setupAnim(T entity, float limbSwing, float limbSwingAmount, float ageInTicks, float netHeadYaw, float headPitch)");

        if (_bones.FirstOrDefault(b => b.Name.Contains("head", StringComparison.OrdinalIgnoreCase)) is { } head)
        {
            w.Line($"this.{head.Name}.yRot = {Angle(-head.Group.Rotation[1])} + netHeadYaw * Mth.DEG_TO_RAD;")
                .Line($"this.{head.Name}.xRot = {Angle(-head.Group.Rotation[0])} + headPitch * Mth.DEG_TO_RAD;");
        }

        // Legs swing in pairs (front left with back right), arms against the leg on their side.
        Bone[] legs = [.. _bones.Where(b => b.Name.Contains("leg", StringComparison.OrdinalIgnoreCase))];
        for (int i = 0; i < legs.Length; i++)
        {
            bool forward = legs.Length == 4 ? i is 0 or 3 : i % 2 == 0;
            w.Line($"this.{legs[i].Name}.xRot = {Angle(-legs[i].Group.Rotation[0])} + Mth.cos(limbSwing * 0.6662f{(forward ? string.Empty : " + Mth.PI")}) * 1.4f * limbSwingAmount;");
        }

        Bone[] arms = [.. _bones.Where(b => b.Name.Contains("arm", StringComparison.OrdinalIgnoreCase))];
        for (int i = 0; i < arms.Length; i++)
        {
            w.Line($"this.{arms[i].Name}.xRot = {Angle(-arms[i].Group.Rotation[0])} + Mth.cos(limbSwing * 0.6662f{(i % 2 == 0 ? " + Mth.PI" : string.Empty)}) * limbSwingAmount;");
        }

        w.CloseBlock();
    }

    private void AddBone(BlockbenchGroup group, Bone? parent)
    {
        var bone = new Bone(UniqueName(group.Name), group, parent);
        _bones.Add(bone);
        foreach (BlockbenchGroup child in group.Children)
        {
            AddBone(child, bone);
        }
    }

    private string UniqueName(string text)
    {
        string type = JavaNames.ToTypeName(text, "Part");
        string name = char.ToLowerInvariant(type[0]) + type[1..];
        string candidate = Keywords.Contains(name) ? name + "Part" : name;
        for (int i = 2; !_names.Add(candidate); i++)
        {
            candidate = name + i.ToString(CultureInfo.InvariantCulture);
        }

        return candidate;
    }

    /// <summary>Position of a Blockbench point seen from a pivot, in Java model axes (X and Y flipped).</summary>
    private static double[] Relative(double[] point, double[] pivot) => [pivot[0] - point[0], pivot[1] - point[1], point[2] - pivot[2]];

    /// <summary>A box seen from a pivot: Java gives its corner of smallest coordinates, which is Blockbench's "to" on X and Y.</summary>
    private static string Box(BlockbenchCube cube, double[] pivot)
    {
        double[] size = cube.Size;
        string corner = $"{F(pivot[0] - cube.To[0])}, {F(pivot[1] - cube.To[1])}, {F(cube.From[2] - pivot[2])}";
        string mirror = cube.Mirror ? ".mirror()" : string.Empty;
        string unmirror = cube.Mirror ? ".mirror(false)" : string.Empty;
        return $".texOffs({cube.UvOffset[0]}, {cube.UvOffset[1]}){mirror}.addBox({corner}, {F(size[0])}, {F(size[1])}, {F(size[2])}, new CubeDeformation({F(cube.Inflate)})){unmirror}";
    }

    private static string Pose(double[] offset, double[] rotation)
    {
        string position = $"{F(offset[0])}, {F(offset[1])}, {F(offset[2])}";
        return rotation.Any(r => Math.Abs(r) > 1e-6)
            ? $"PartPose.offsetAndRotation({position}, {Angle(-rotation[0])}, {Angle(-rotation[1])}, {Angle(rotation[2])})"
            : $"PartPose.offset({position})";
    }

    private static string Angle(double degrees) => F(degrees * Math.PI / 180);

    // Math.Round keeps the sign of -0, which would be written "-0.0f".
    private static string F(double value) => JavaNames.FloatLiteral((float)Math.Round(value, 5) + 0f);

    private sealed record Bone(string Name, BlockbenchGroup Group, Bone? Parent);
}
