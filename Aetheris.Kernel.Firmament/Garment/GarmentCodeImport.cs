using System.Globalization;
using System.Numerics;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Aetheris.Kernel.Firmament.Materializer;

namespace Aetheris.Kernel.Firmament.Garment;

public sealed record GarmentCodePanelMapping(string UpstreamName, string PanelName, bool BoundaryReversed, IReadOnlyList<string> Edges);
public sealed record GarmentCodeImportResult(string? Source, string InputHash,
    IReadOnlyList<GarmentCodePanelMapping> Panels, IReadOnlyList<GarmentDiagnostic> Diagnostics)
{
    public bool IsSuccess => Source is not null && Diagnostics.Count == 0;
}

/// <summary>Converts resolved GarmentCode sewing patterns into ordinary editable Firmament.
/// Curve coordinates and stitch conventions follow GarmentCode (MIT, Maria Korosteleva),
/// pygarment/pattern/utils.py and pygarment/meshgen/boxmeshgen.py. See THIRD_PARTY_NOTICES.md.
/// No upstream Python, triangulator or simulator is needed at runtime.</summary>
public static class GarmentCodeImport
{
    public static GarmentCodeImportResult Import(string json, string identity = "<memory>")
    {
        string hash = Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(json)));
        try
        {
            if (json.Length > 16 * 1024 * 1024)
            {
                Fail("garmentcode-input-budget", "Pattern JSON exceeds the 16 MiB import budget.");
            }
            return ImportCore(json, hash, identity);
        }
        catch (Exception exception) when (exception is JsonException or InvalidDataException
            or KeyNotFoundException or InvalidOperationException or FormatException or OverflowException
            or IndexOutOfRangeException or ArgumentException)
        {
            string code = exception is InvalidDataException ? exception.Message.Split(':')[0] : "garmentcode-json-invalid";
            return new(null, hash, [], [new(code, exception.Message, identity)]);
        }
    }

    private static GarmentCodeImportResult ImportCore(string json, string hash, string identity)
    {
        using var document = JsonDocument.Parse(json, new JsonDocumentOptions { MaxDepth = 64 });
        var root = document.RootElement;
        if ((root.TryGetProperty("parameters", out var parameters) && parameters.EnumerateObject().Any())
            || (root.TryGetProperty("parameter_order", out var order) && order.GetArrayLength() != 0))
        {
            Fail("garmentcode-parameters-unresolved", "Evaluate the upstream design program before importing its resolved pattern.");
        }
        var properties = root.GetProperty("properties");
        if (properties.GetProperty("curvature_coords").GetString() != "relative"
            || properties.GetProperty("normalize_panel_translation").GetBoolean())
        {
            Fail("garmentcode-normalization-required", "Export resolved relative curves and explicit panel translations first.");
        }
        double units = Number(properties.GetProperty("units_in_meter"));
        if (units <= 0)
        {
            Fail("garmentcode-units-invalid", "units_in_meter must be positive.");
        }
        double millimeters = 1000 / units;
        var pattern = root.GetProperty("pattern");
        var panels = new List<ImportedPanel>();
        var names = new HashSet<string>(StringComparer.Ordinal);
        var text = new StringBuilder();
        text.AppendLine("schema Garment");
        text.AppendLine("// Imported from GarmentCode; MIT, copyright (c) 2024 Maria Korosteleva.");
        text.AppendLine("// Keep the source specification and its applicable license with this derivative.");
        text.AppendLine("// Input SHA-256: " + hash);
        text.AppendLine("// Local XY remains the flat rest pattern. Placement maps upstream Y-up to Aetheris Z-up.");
        foreach (var entry in pattern.GetProperty("panels").EnumerateObject())
        {
            string name = Identifier(entry.Name);
            if (!names.Add(name))
            {
                Fail("garmentcode-name-collision", entry.Name);
            }
            var vertices = entry.Value.GetProperty("vertices").EnumerateArray()
                .Select(value => Point(value, millimeters)).ToArray();
            if (vertices.Length is < 3 or > 4096)
            {
                Fail("garmentcode-vertex-budget", "A panel requires between 3 and 4096 cut vertices.");
            }
            var edges = new List<ImportedEdge>();
            foreach (var edge in entry.Value.GetProperty("edges").EnumerateArray())
            {
                var endpoints = edge.GetProperty("endpoints").EnumerateArray().Select(value => value.GetInt32()).ToArray();
                if (endpoints.Length != 2 || endpoints.Any(index => index < 0 || index >= vertices.Length))
                {
                    Fail("garmentcode-endpoint-invalid", entry.Name);
                }
                string edgeName = "edge" + edges.Count;
                if (edge.TryGetProperty("label", out var label) && !string.IsNullOrEmpty(label.GetString()))
                {
                    edgeName += "_" + Identifier(label.GetString()!);
                }
                edges.Add(new(edges.Count, edgeName, endpoints[0], endpoints[1], Curve(edge, vertices[endpoints[0]], vertices[endpoints[1]], millimeters)));
            }
            if (edges.Count < 3 || edges.Where((edge, index) => edge.To != edges[(index + 1) % edges.Count].From).Any())
            {
                Fail("garmentcode-loop-invalid", entry.Name + " must contain one ordered closed edge loop.");
            }
            bool reversed = edges.Sum(edge => ResolvedProfile2DValidator.SignedAreaContribution(edge.Curve)) < 0;
            if (reversed)
            {
                edges = edges.AsEnumerable().Reverse().Select(edge => edge with
                {
                    From = edge.To,
                    To = edge.From,
                    Curve = Reverse(edge.Curve),
                }).ToList();
            }
            var translation = Triple(entry.Value.GetProperty("translation"));
            var rotation = Triple(entry.Value.GetProperty("rotation"));
            var panel = new ImportedPanel(entry.Name, name, vertices, edges, translation, rotation, reversed);
            panels.Add(panel);
            EmitFeature(text, panel);
        }
        if (panels.Count is < 1 or > 128)
        {
            Fail("garmentcode-panel-budget", "Import between 1 and 128 panels.");
        }
        text.AppendLine("Garment ImportedGarment {");
        text.AppendLine("  meshSize: 60mm;");
        text.AppendLine("  // Aetheris cotton defaults; upstream JSON does not specify fabric physics.");
        text.AppendLine("  Fabric cotton {");
        text.AppendLine("    thickness: 2mm;");
        text.AppendLine("    arealDensity: 0.2;");
        text.AppendLine("    warpCompliance: 0.00000001;");
        text.AppendLine("    weftCompliance: 0.00000001;");
        text.AppendLine("    diagonalCompliance: 0.00000004;");
        text.AppendLine("  }");
        foreach (var panel in panels)
        {
            text.AppendLine($"  Panel {panel.Name} {{");
            text.AppendLine($"    profile: {panel.Name}Cut;");
            var origin = panel.Translation.Select(value => value * millimeters).ToArray();
            text.AppendLine($"    origin: [{Length(origin[0])}, {Length(origin[2])}, {Length(origin[1])}];");
            text.AppendLine("    u: " + Direction(Rotate(Vector3.UnitX, panel.Rotation)) + ";");
            text.AppendLine("    v: " + Direction(Rotate(Vector3.UnitY, panel.Rotation)) + ";");
            text.AppendLine("    grain: [0, 1];");
            text.AppendLine("  }");
        }
        int seamIndex = 0;
        foreach (var pair in pattern.GetProperty("stitches").EnumerateArray())
        {
            var values = pair.EnumerateArray().ToArray();
            if (values.Length is < 2 or > 3 || (values.Length == 3 && values[2].GetString() != "right_wrong"))
            {
                Fail("garmentcode-stitch-invalid", "Expected two edge references and an optional right_wrong flag.");
            }
            var a = Reference(values[0], panels);
            var b = Reference(values[1], panels);
            double lengthA = GarmentPatternInspection.CurveLength(a.Edge.Curve);
            double lengthB = GarmentPatternInspection.CurveLength(b.Edge.Curve);
            double mismatch = Math.Abs(lengthA - lengthB) / Math.Max(lengthA, lengthB);
            if (mismatch > .25)
            {
                Fail("garmentcode-ease-unsupported", "Seam " + seamIndex + " exceeds the admitted 25% ease budget.");
            }
            text.AppendLine($"  Interface<Stitch> seam{seamIndex++} {{");
            text.AppendLine($"    a: {a.Panel.Name}.edges.{a.Edge.Name};");
            text.AppendLine($"    b: {b.Panel.Name}.edges.{b.Edge.Name};");
            bool reversedSeam = (values.Length != 3) ^ a.Panel.BoundaryReversed ^ b.Panel.BoundaryReversed;
            text.AppendLine("    orientation: " + (reversedSeam ? "Reversed" : "Same") + ";");
            text.AppendLine("    // Measured mismatch in the upstream pattern, explicitly retained as ease.");
            text.AppendLine("    ease: " + Scalar(mismatch) + ";");
            text.AppendLine("  }");
        }
        text.AppendLine("}");
        string source = text.ToString();
        var parsed = GarmentAuthoring.Parse(source, identity);
        if (!parsed.IsSuccess)
        {
            return new(null, hash, [], parsed.Diagnostics);
        }
        return new(source, hash, panels.Select(panel => new GarmentCodePanelMapping(panel.UpstreamName,
            panel.Name, panel.BoundaryReversed, panel.Edges.OrderBy(edge => edge.Index).Select(edge => edge.Name).ToArray())).ToArray(), []);
    }

    private static void EmitFeature(StringBuilder text, ImportedPanel panel)
    {
        text.AppendLine();
        text.AppendLine($"Feature {panel.Name}Pattern() -> Profile {{");
        for (int i = 0; i < panel.Vertices.Length; i++)
        {
            EmitPoint(text, "vertex" + i, panel.Vertices[i]);
        }
        foreach (var edge in panel.Edges)
        {
            string guide = edge.Name + "Guide";
            switch (edge.Curve)
            {
                case LineArcLineSegment2D:
                    text.AppendLine($"  Line2 {guide} {{ from: vertex{edge.From}; to: vertex{edge.To} }}");
                    break;
                case LineArcCubicBezier2D cubic:
                    EmitPoint(text, edge.Name + "Control1", cubic.Control1);
                    EmitPoint(text, edge.Name + "Control2", cubic.Control2);
                    text.AppendLine($"  CubicBezier2 {guide} {{ from: vertex{edge.From}; control1: {edge.Name}Control1; control2: {edge.Name}Control2; to: vertex{edge.To} }}");
                    break;
                case LineArcCircularArc2D arc:
                    EmitPoint(text, edge.Name + "Center", arc.Center);
                    text.AppendLine($"  Circle2 {guide} {{ center: {edge.Name}Center; radius: {Length(arc.Radius)} }}");
                    break;
            }
        }
        text.AppendLine("  return Profile {");
        text.AppendLine("    Loop Outer {");
        foreach (var edge in panel.Edges)
        {
            string sweep = "";
            if (edge.Curve is LineArcCircularArc2D arc)
            {
                sweep = "; sweep: " + (arc.SweepAngleRadians > 0 ? "CounterClockwise" : "Clockwise");
            }
            text.AppendLine($"      Segment {edge.Name} {{ trace: {edge.Name}Guide; from: vertex{edge.From}; to: vertex{edge.To}{sweep} }}");
        }
        text.AppendLine("    }");
        text.AppendLine("  }");
        text.AppendLine("}");
        text.AppendLine($"Feature {panel.Name}Cut = {panel.Name}Pattern();");
    }

    private static LineArcProfileCurve2D Curve(JsonElement edge, (double X, double Y) start,
        (double X, double Y) end, double scale)
    {
        if (!edge.TryGetProperty("curvature", out var curvature))
        {
            return new LineArcLineSegment2D(start, end);
        }
        string? type = curvature.ValueKind == JsonValueKind.Array ? "quadratic" : curvature.GetProperty("type").GetString();
        var parameters = curvature.ValueKind == JsonValueKind.Array ? curvature : curvature.GetProperty("params");
        if (type == "quadratic")
        {
            var control = Relative(start, end, curvature.ValueKind == JsonValueKind.Array ? parameters : parameters[0]);
            return new LineArcCubicBezier2D(start, Lerp(start, control, 2d / 3), Lerp(end, control, 2d / 3), end);
        }
        if (type == "cubic" && parameters.GetArrayLength() == 2)
        {
            return new LineArcCubicBezier2D(start, Relative(start, end, parameters[0]), Relative(start, end, parameters[1]), end);
        }
        if (type == "circle" && parameters.GetArrayLength() == 3)
        {
            double radius = Number(parameters[0]) * scale;
            bool large = Flag(parameters[1]);
            bool ccw = Flag(parameters[2]);
            double dx = end.X - start.X;
            double dy = end.Y - start.Y;
            double chord = Math.Sqrt(dx * dx + dy * dy);
            if (chord <= 1e-8 || radius <= 0 || radius < chord / 2 - 1e-7)
            {
                Fail("garmentcode-circle-invalid", "Circular edge requires a positive radius >= half its chord.");
            }
            double distance = Math.Sqrt(Math.Max(0, radius * radius - chord * chord / 4));
            double sign = ccw != large ? 1 : -1;
            var center = (X: (start.X + end.X) / 2 - sign * distance * dy / chord,
                Y: (start.Y + end.Y) / 2 + sign * distance * dx / chord);
            double angle = Math.Atan2(start.Y - center.Y, start.X - center.X);
            double sweep = Math.Atan2(end.Y - center.Y, end.X - center.X) - angle;
            if (ccw && sweep <= 0)
            {
                sweep += 2 * Math.PI;
            }
            if (!ccw && sweep >= 0)
            {
                sweep -= 2 * Math.PI;
            }
            return new LineArcCircularArc2D(center, radius, angle, sweep);
        }
        Fail("garmentcode-curve-unsupported", "Admitted curves: line, quadratic, cubic, circle; found " + type);
        return null!;
    }

    private static (ImportedPanel Panel, ImportedEdge Edge) Reference(JsonElement reference, IReadOnlyList<ImportedPanel> panels)
    {
        string? name = reference.GetProperty("panel").GetString();
        int index = reference.GetProperty("edge").GetInt32();
        var panel = panels.SingleOrDefault(panel => panel.UpstreamName == name);
        if (panel is null || index < 0 || index >= panel.Edges.Count)
        {
            Fail("garmentcode-stitch-reference", name + "/" + index);
        }
        return (panel!, panel!.Edges.Single(edge => edge.Index == index));
    }

    private static LineArcProfileCurve2D Reverse(LineArcProfileCurve2D curve) => curve switch
    {
        LineArcLineSegment2D line => new LineArcLineSegment2D(line.End, line.Start),
        LineArcCubicBezier2D cubic => new LineArcCubicBezier2D(cubic.End, cubic.Control2, cubic.Control1, cubic.Start),
        LineArcCircularArc2D arc => new LineArcCircularArc2D(arc.Center, arc.Radius,
            arc.StartAngleRadians + arc.SweepAngleRadians, -arc.SweepAngleRadians),
        _ => throw new NotSupportedException("Unsupported imported curve."),
    };

    private static Vector3 Rotate(Vector3 value, double[] degrees)
    {
        // GarmentCode uses Rz * Ry * Rx on column vectors; System.Numerics uses row vectors.
        value = Vector3.Transform(value, Matrix4x4.CreateRotationX((float)(degrees[0] * Math.PI / 180)));
        value = Vector3.Transform(value, Matrix4x4.CreateRotationY((float)(degrees[1] * Math.PI / 180)));
        return Vector3.Transform(value, Matrix4x4.CreateRotationZ((float)(degrees[2] * Math.PI / 180)));
    }

    private static string Direction(Vector3 value) => $"[{Scalar(value.X)}, {Scalar(value.Z)}, {Scalar(value.Y)}]";
    private static void EmitPoint(StringBuilder text, string name, (double X, double Y) point) =>
        text.AppendLine($"  Point2 {name} {{ position: [{Length(point.X)}, {Length(point.Y)}] }}");
    private static string Length(double value) => Scalar(value) + "mm";
    private static string Scalar(double value) => value.ToString("0.############", CultureInfo.InvariantCulture);
    private static double Number(JsonElement value)
    {
        double result = value.GetDouble();
        if (!double.IsFinite(result))
        {
            Fail("garmentcode-number-invalid", "All coordinates must be finite.");
        }
        return result;
    }
    private static double[] Triple(JsonElement value)
    {
        var result = value.EnumerateArray().Select(Number).ToArray();
        if (result.Length != 3)
        {
            Fail("garmentcode-frame-invalid", "Expected three translation or Euler components.");
        }
        return result;
    }
    private static bool Flag(JsonElement value)
    {
        int result = value.GetInt32();
        if (result is not (0 or 1))
        {
            Fail("garmentcode-circle-flag-invalid", "Arc flags must be 0 or 1.");
        }
        return result == 1;
    }
    private static (double X, double Y) Point(JsonElement value, double scale)
    {
        if (value.GetArrayLength() != 2)
        {
            Fail("garmentcode-point-invalid", "Expected two coordinates.");
        }
        return (Number(value[0]) * scale, Number(value[1]) * scale);
    }
    private static (double X, double Y) Relative((double X, double Y) start, (double X, double Y) end, JsonElement value)
    {
        var point = Point(value, 1);
        double dx = end.X - start.X;
        double dy = end.Y - start.Y;
        return (start.X + point.X * dx - point.Y * dy, start.Y + point.X * dy + point.Y * dx);
    }
    private static (double X, double Y) Lerp((double X, double Y) a, (double X, double Y) b, double t) =>
        (a.X + (b.X - a.X) * t, a.Y + (b.Y - a.Y) * t);
    private static string Identifier(string name)
    {
        string result = Regex.Replace(name, "[^A-Za-z0-9_]", "_");
        if (result.Length == 0 || char.IsDigit(result[0]))
        {
            result = "panel_" + result;
        }
        return result;
    }
    private static void Fail(string code, string detail) => throw new InvalidDataException(code + ": " + detail);
    private sealed record ImportedEdge(int Index, string Name, int From, int To, LineArcProfileCurve2D Curve);
    private sealed record ImportedPanel(string UpstreamName, string Name, (double X, double Y)[] Vertices,
        IReadOnlyList<ImportedEdge> Edges, double[] Translation, double[] Rotation, bool BoundaryReversed);
}
