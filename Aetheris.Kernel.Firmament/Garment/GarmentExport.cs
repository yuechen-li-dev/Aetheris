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

    public static string BodyObj(HumanoidGameplayBody body)
    {
        var result = new StringBuilder("# Antonia gameplay body; metres; Z up. Preserve its source attribution.\n");
        foreach (var vertex in body.Surface.Vertices)
        {
            var point = vertex.Position;
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
        var result = new StringBuilder("<svg xmlns=\"http://www.w3.org/2000/svg\" width=\"1200\" height=\"900\" viewBox=\"0 0 1200 900\">\n");
        result.AppendLine("<rect width=\"1200\" height=\"900\" fill=\"#f5f3ee\"/><g font-family=\"sans-serif\" fill=\"#24344c\">");
        result.AppendLine("<text x=\"24\" y=\"36\" font-size=\"24\">" + Escape(garment.Source.Name) + " — material patterns (mm)</text>");
        result.AppendLine("<text x=\"24\" y=\"64\" font-size=\"14\">Blue: open edge · orange: stitch · green: explicit support. Cut boundary; no seam allowance added.</text>");
        int panelNumber = 0;
        foreach (var panel in garment.Panels)
        {
            var points = garment.Cloth.Definition.Positions.Skip(panel.VertexStart).Take(panel.VertexCount).ToArray();
            float minX = points.Min(point => point.X);
            float minY = points.Min(point => point.Y);
            float width = points.Max(point => point.X) - minX;
            float height = points.Max(point => point.Y) - minY;
            float scale = MathF.Min(490 / width, 300 / height);
            float originX = 40 + panelNumber % 2 * 590;
            float originY = 140 + panelNumber / 2 * 370;
            panelNumber++;
            result.AppendLine(FormattableString.Invariant($"<text x=\"{originX}\" y=\"{originY - 30}\" font-size=\"18\">{Escape(panel.Identity)}</text>"));
            foreach (var edge in panel.Edges)
            {
                string reference = panel.Identity + ".edges." + edge.Key;
                var seam = garment.Source.Stitches.FirstOrDefault(stitch => stitch.A == reference || stitch.B == reference
                    || stitch.A == panel.Name + ".edges." + edge.Key || stitch.B == panel.Name + ".edges." + edge.Key);
                bool pinned = garment.Source.Panels.Single(source => source.Name == panel.Name).Pins.Contains(edge.Key);
                string color = pinned ? "#16835b" : seam is null ? "#3274a0" : "#c76925";
                var line = edge.Value.Select(index => garment.Cloth.Definition.Positions[index]).ToArray();
                string coordinates = string.Join(" ", line.Select(point => FormattableString.Invariant($"{originX + (point.X - minX) * scale},{originY + (height - point.Y + minY) * scale}")));
                result.AppendLine($"<polyline points=\"{coordinates}\" fill=\"none\" stroke=\"{color}\" stroke-width=\"2\"/>");
                var middle = line[line.Length / 2];
                string label = edge.Key + (seam is null ? "" : " / " + seam.Name);
                result.AppendLine(FormattableString.Invariant($"<text x=\"{originX + (middle.X - minX) * scale + 3}\" y=\"{originY + (height - middle.Y + minY) * scale - 4}\" font-size=\"10\">{Escape(label)}</text>"));
            }
        }
        result.AppendLine("</g></svg>");
        return result.ToString();
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
