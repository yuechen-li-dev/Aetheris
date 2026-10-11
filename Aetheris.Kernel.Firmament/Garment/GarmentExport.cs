using System.Globalization;
using System.Numerics;
using System.Security;
using System.Text;
using System.Text.Json;
using Aetheris.Cloth3D;
using Aetheris.Humanoid;

namespace Aetheris.Kernel.Firmament.Garment;

public static class GarmentExport
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        IncludeFields = true,
    };

    public static string Artifact(CompiledGarment garment, ClothSnapshot3D? settled = null)
    {
        if (settled is not null)
        {
            ClothState3D.ValidateSnapshot(garment.Cloth, settled);
        }
        return JsonSerializer.Serialize(new
        {
            schema = "aetheris.garment.v1",
            units = "m",
            upAxis = "Z",
            sourceHash = garment.Source.SourceHash,
            sourceIdentity = garment.Source.SourceIdentity,
            figureFits = garment.Source.FigureFits,
            posing = garment.Source.Posing,
            transition = garment.Source.Transition,
            contentKey = garment.Cloth.ContentKey,
            definition = garment.Cloth.Definition,
            panels = garment.Panels,
            seams = garment.Source.Stitches,
            settled,
        }, JsonOptions);
    }

    public static string Obj(CompiledGarment garment, ClothSnapshot3D? settled = null)
    {
        if (settled is not null)
        {
            ClothState3D.ValidateSnapshot(garment.Cloth, settled);
        }
        var positions = settled?.Positions ?? garment.Cloth.Definition.InitialPositions;
        var result = new StringBuilder("# Aetheris garment; metres; Z up; source=" + garment.Source.SourceHash + "\n");
        foreach (var point in positions)
        {
            result.AppendLine(FormattableString.Invariant($"v {point.X:R} {point.Y:R} {point.Z:R}"));
        }
        foreach (var point in garment.Cloth.Definition.Coordinates)
        {
            result.AppendLine(FormattableString.Invariant($"vt {point.X:R} {point.Y:R}"));
        }
        foreach (var panel in garment.Panels)
        {
            result.AppendLine("g " + panel.Identity.Replace('.', '_'));
            for (int face = panel.IndexStart; face < panel.IndexStart + panel.IndexCount; face += 3)
            {
                int a = garment.Cloth.Definition.Indices[face] + 1;
                int b = garment.Cloth.Definition.Indices[face + 1] + 1;
                int c = garment.Cloth.Definition.Indices[face + 2] + 1;
                result.AppendLine($"f {a}/{a} {b}/{b} {c}/{c}");
            }
        }
        return result.ToString();
    }

    public static string BodyObj(HumanoidGameplayBody body,
        IReadOnlyList<Aetheris.Kernel.Core.Math.Point3D>? positions = null)
    {
        var result = new StringBuilder("# Antonia gameplay body; metres; Z up. Preserve its source attribution.\n");
        foreach (var point in positions ?? body.Surface.Vertices.Select(vertex => vertex.Position).ToArray())
        {
            result.AppendLine(FormattableString.Invariant($"v {point.X / 1000:R} {point.Y / 1000:R} {point.Z / 1000:R}"));
        }
        foreach (var face in body.Surface.Faces)
        {
            result.AppendLine($"f {face.A + 1} {face.B + 1} {face.C + 1}");
        }
        return result.ToString();
    }

    public static string PatternsSvg(CompiledGarment garment)
    {
        return GarmentPatternPreview.Svg(garment.Source);
    }

    public static string Usd(CompiledGarment garment, ClothSnapshot3D? settled = null)
    {
        if (settled is not null)
        {
            ClothState3D.ValidateSnapshot(garment.Cloth, settled);
        }
        var positions = settled?.Positions ?? garment.Cloth.Definition.InitialPositions;
        var result = new StringBuilder("#usda 1.0\n(\n    upAxis = \"Z\"\n    metersPerUnit = 1\n)\n");
        result.AppendLine("def Xform \"Garment\" {\n    custom string sourceHash = \"" + garment.Source.SourceHash + "\"");
        foreach (var panel in garment.Panels)
        {
            result.AppendLine("    def Mesh \"" + panel.Identity.Replace('.', '_') + "\" {");
            result.AppendLine("        uniform token subdivisionScheme = \"none\"\n        uniform bool doubleSided = true");
            result.AppendLine("        point3f[] points = [" + string.Join(", ", positions.Skip(panel.VertexStart).Take(panel.VertexCount).Select(Point)) + "]");
            result.AppendLine("        int[] faceVertexCounts = [" + string.Join(", ", Enumerable.Repeat(3, panel.IndexCount / 3)) + "]");
            result.AppendLine("        int[] faceVertexIndices = [" + string.Join(", ", garment.Cloth.Definition.Indices.Skip(panel.IndexStart).Take(panel.IndexCount).Select(index => index - panel.VertexStart)) + "]");
            result.AppendLine("        color3f[] primvars:displayColor = [(0.1, 0.22, 0.38)]\n    }");
        }
        result.AppendLine("}");
        return result.ToString();
    }

    private static string Point(Vector3 point) => FormattableString.Invariant($"({point.X:R}, {point.Y:R}, {point.Z:R})");
    private static string Escape(string value) => SecurityElement.Escape(value) ?? "";
}
