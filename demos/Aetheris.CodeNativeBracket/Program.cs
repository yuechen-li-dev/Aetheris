using System.Globalization;
using System.Reflection;
using System.Security.Cryptography;
using System.Text.Json;
using Aetheris.Kernel.Core.Brep;
using Aetheris.Kernel.Core.Brep.Tessellation;
using Aetheris.Kernel.Core.Brep.Verification;
using Aetheris.Kernel.Core.Step242;
using Aetheris.Kernel.Firmament;
using Aetheris.Kernel.Firmament.Assembly;
using Aetheris.Kernel.Firmament.Drawing;
using Aetheris.Kernel.Firmament.Export;

namespace Aetheris.CodeNativeBracket;

public static class Program
{
    private static int Main(string[] args)
    {
        try
        {
            if (args.Length > 1 || args.Any(arg => arg.StartsWith('-')))
                throw new ArgumentException("Usage: Aetheris.CodeNativeBracket [output-directory]");
            var output = Path.GetFullPath(args.FirstOrDefault() ?? "artifacts/local/demos/code-native-bracket");
            MakeBracket(new(), Path.Combine(output, "nominal"));
            MakeBracket(new(Width: 100, Height: 75, LegWidth: 25, LegHeight: 24, Thickness: 15, HoleX: 70, HoleY: 12, ShaftDiameter: 8, EntryDiameter: 14), Path.Combine(output, "variant"));
            Console.WriteLine($"Rehearsal artifacts: {output}");
            return 0;
        }
        catch (Exception exception) { Console.Error.WriteLine(exception.Message); return 1; }
    }

    public static void MakeBracket(BracketDimensions dimensions, string output)
    {
        var maxRadius = (dimensions.EntryDiameter + .1) / 2;
        var lean = (dimensions.Thickness + .1) * Math.Tan(.5 * Math.PI / 180d);
        var minimumWeb = Math.Min(Math.Min(dimensions.HoleY - .1 - maxRadius, dimensions.LegHeight - .1 - dimensions.HoleY - .1 - maxRadius),
            Math.Min(dimensions.HoleX - .1 - maxRadius, dimensions.Width - .1 - dimensions.HoleX - .1 - maxRadius)) - lean;
        var deepestEntry = (dimensions.EntryDiameter + .1 - (dimensions.ShaftDiameter - .1)) / (2 * Math.Tan(89.5 * Math.PI / 360d));
        if (minimumWeb <= 0 || dimensions.Thickness - .1 <= deepestEntry || dimensions.ShaftDiameter <= .1)
            throw new InvalidOperationException("Bracket drawing limits permit breakout or no shaft. Revise dimensions before exporting.");
        Directory.CreateDirectory(output);
        var source = dimensions.Source(false);
        var path = Path.Combine(output, "bracket.firmament");
        File.WriteAllText(path, source);
        var compilation = FirmamentBuildAndExport.CompileSource(source);
        if (!compilation.IsSuccess) throw new InvalidOperationException(string.Join("\n", compilation.Diagnostics.Select(d => d.Message)));
        var product = compilation.Value;
        var imported = Step242Importer.ImportBody(product.StepText);
        if (!imported.IsSuccess) throw new InvalidOperationException("Rehearsal STEP reimport failed.");
        var body = imported.Value;
        if (!BrepExportPreflight.Validate(body).IsValid) throw new InvalidOperationException("STEP failed export preflight.");
        var mass = BrepMassProperties.Evaluate(body);
        if (!mass.IsEnclosed || !mass.IsOrientationConsistent || Math.Abs(mass.AbsoluteVolume - dimensions.Volume) > Math.Min(mass.ErrorBound ?? 0, dimensions.Volume * 2e-5))
            throw new InvalidOperationException("Reimported geometry failed independent bounded volume/enclosure verification.");
        var tessellation = BrepDisplayTessellator.Tessellate(body);
        if (!tessellation.IsSuccess) throw new InvalidOperationException("GLB tessellation failed.");
        var positions = new List<double>(); var normals = new List<double>(); var indices = new List<int>();
        foreach (var face in tessellation.Value.FacePatches)
        {
            var offset = positions.Count / 3;
            positions.AddRange(face.Positions.SelectMany(p => new[] { p.X, p.Y, p.Z }));
            normals.AddRange(face.Normals.SelectMany(p => new[] { p.X, p.Y, p.Z }));
            indices.AddRange(face.TriangleIndices.Select(i => i + offset));
        }
        var display = new AssemblyDisplayMeshDocument("aetheris/display-mesh/1", "Bracket", "mm",
            [new("bracket-definition", "bracket", positions.ToArray(), normals.ToArray(), indices.ToArray())],
            [new("bracket", "Bracket", null, "bracket-definition", [1,0,0,0,0,1,0,0,0,0,1,0,0,0,0,1])]);
        foreach (var artifact in ProductArtifactExporter.Export(new("Bracket", product.StepText, display), ["step", "glb"], output))
            File.WriteAllBytes(Path.Combine(output, artifact.FileName), artifact.Bytes);

        // Authored views use the existing compiler projection and title block.
        // This small family supplies its own dimensions, not a general recognizer.
        var drawingPath = Path.Combine(output, "bracket-drawing.firmament");
        File.WriteAllText(drawingPath, dimensions.Source(true));
        var drawing = FirmamentDrawingCompiler.Compile(drawingPath, Path.Combine(output, "drawing-base"),
            new(new("Codex (AI)", "2026-10-21"), new(), new()));
        if (!drawing.IsSuccess) throw new InvalidOperationException(string.Join("\n", drawing.Diagnostics));
        var holeRims = body.Topology.Edges.Where(e => body.TryGetEdgeCurveGeometry(e.Id, out var curve) && curve?.Circle3 is { } circle
                && Math.Abs(circle.Center.X - dimensions.HoleX) < 1e-7 && Math.Abs(circle.Center.Y - dimensions.HoleY) < 1e-7)
            .Select(e => "edge:" + e.Id.Value).ToHashSet(StringComparer.Ordinal);
        var defined = BracketDrawing.Complete(drawing.Artifacts!.Drawing, dimensions, body.Topology.Faces.Select(f => f.Id.Value).ToArray(), holeRims, product.Features!.Single().FeatureId);
        File.WriteAllText(Path.Combine(output, "Bracket.svg"), DrawingSvgRenderer.Render(defined));
        DrawingVectorPdfWriter.Write(defined, Path.Combine(output, "Bracket.pdf"));
        DrawingPptxWriter.WriteDrawing(defined, Path.Combine(output, "Bracket.pptx"), false);
        File.WriteAllText(Path.Combine(output, "Bracket.drawing.json"), JsonSerializer.Serialize(defined, JsonOptions));
        var hashes = Directory.GetFiles(output).Where(p => Path.GetExtension(p) is ".step" or ".glb" or ".pdf" or ".svg")
            .ToDictionary(p => Path.GetFileName(p)!, p => Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(p))).ToLowerInvariant());
        File.WriteAllText(Path.Combine(output, "qualification.json"), JsonSerializer.Serialize(new
        {
            dimensions, exactConstructionVolume = dimensions.Volume,
            independentMass = new { mass.AbsoluteVolume, mass.EvaluationMethod, mass.ErrorBound, mass.IsEnclosed, mass.IsOrientationConsistent },
            pmi = Step242SemanticPmiInspector.Inspect(product.StepText),
            bodyFaces = body.Topology.Faces.Count(), triangleCount = indices.Count / 3,
            nominalDrawingDefinition = "Authored L-bracket dimensions; independently reconstruct from PDF before demo publication.",
            processCapabilityVerified = false, hashes,
            toleranceScreen = new { minimumWebMillimetres = minimumWeb, maximumEntryDepthMillimetres = deepestEntry,
                scope = "Independent Cartesian/size limits and axis lean on ideal orthogonal stock; stock form and machine/process capability unqualified." }
        }, JsonOptions));
    }

    internal static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };
}

public sealed record BracketDimensions(double Width = 80, double Height = 60, double LegWidth = 20, double LegHeight = 20,
    double Thickness = 12, double HoleX = 60, double HoleY = 10, double ShaftDiameter = 6, double EntryDiameter = 12)
{
    public double Volume => (Width * LegHeight + LegWidth * (Height - LegHeight)) * Thickness
        - Math.PI * ShaftDiameter * ShaftDiameter / 4d * Thickness
        - Math.PI * (EntryDiameter - ShaftDiameter) / 2d *
            (EntryDiameter * EntryDiameter + EntryDiameter * ShaftDiameter - 2 * ShaftDiameter * ShaftDiameter) / 12d;

    public string Source(bool includeDrawing)
    {
        using var reader = new StreamReader(Assembly.GetExecutingAssembly().GetManifestResourceStream("bracket-library.firmament")!);
        var library = reader.ReadToEnd();
        var source = "Model CodeNativeBracket {\nunits: mm\n" + library + FormattableString.Invariant($$"""

            Static dimensions: BracketDimensions = BracketDimensions {
                width: {{Width}}mm; height: {{Height}}mm; legWidth: {{LegWidth}}mm; legHeight: {{LegHeight}}mm
                thickness: {{Thickness}}mm; holeX: {{HoleX}}mm; holeY: {{HoleY}}mm
                shaftDiameter: {{ShaftDiameter}}mm; entryDiameter: {{EntryDiameter}}mm
            }
            Struct Bracket = BracketTemplate<spec: dimensions>
            """);
        if (includeDrawing) source += "\n" + """
            Drawing BracketProduction {
                source: CodeNativeBracket
                title: "PARAMETRIC MACHINED L BRACKET"
                partNumber: "CND-BRACKET"
                revision: 1.0.0
                material: "6061-T6 aluminium"
                orientation: Landscape
                View TOP { direction: +Z; hiddenLines: Wireframe; pmi: [] }
                View EDGE { direction: -Y; hiddenLines: VisibleAndHidden; pmi: [] }
                View ISO { direction: [1, -1, 1]; projection: Isometric; hiddenLines: VisibleOnly; pmi: [] }
            }
            """;
        return source + "\n}\n";
    }
    public static string F(double value) => value.ToString("0.###", CultureInfo.InvariantCulture);
}
