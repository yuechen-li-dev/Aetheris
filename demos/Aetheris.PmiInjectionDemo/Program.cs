using System.Globalization;
using System.Text.Json;
using Aetheris.Kernel.Core.Step242;
using Aetheris.Kernel.Firmament;

const string inputFileName = "nist_ftc_11_asme1_ap242-e2.stp";
var options = DemoOptions.Parse(args);
var demoDir = AppContext.BaseDirectory;
var repoDemoDir = FindRepoDemoDir() ?? demoDir;
var assetInput = Path.Combine(repoDemoDir, "assets", inputFileName);
var outDir = Path.GetFullPath(options.OutDir ?? Path.Combine(repoDemoDir, "out"));

if (!File.Exists(assetInput)) return Fail($"Bundled FTC-11 asset not found: {assetInput}");
if (Directory.Exists(outDir) && !options.Keep) Directory.Delete(outDir, recursive: true);
Directory.CreateDirectory(outDir);

var inputStep = Path.Combine(outDir, inputFileName);
var canonicalStep = Path.Combine(outDir, Path.GetFileNameWithoutExtension(inputFileName) + ".canonical.step");
var overlayStepRelative = "./" + Path.GetFileName(canonicalStep);
var overlayPath = Path.Combine(outDir, "ftc11-pmi-overlay.firm");
var outputStep = Path.Combine(outDir, "ftc11-with-aetheris-pmi.step");
var reportPath = Path.Combine(outDir, "demo-report.json");

File.Copy(assetInput, inputStep, overwrite: true);
var inputText = File.ReadAllText(inputStep);
var inputImport = Step242Importer.ImportBody(inputText);
if (!inputImport.IsSuccess) return Fail("Input FTC-11 STEP import failed: " + string.Join("; ", inputImport.Diagnostics.Select(d => d.Message)));
var canonicalExport = Step242Exporter.ExportBody(inputImport.Value);
if (!canonicalExport.IsSuccess) return Fail("Canonical AP242 export failed: " + string.Join("; ", canonicalExport.Diagnostics.Select(d => d.Message)));
File.WriteAllText(canonicalStep, canonicalExport.Value);

if (options.FirmPath is { } firmPath)
{
    File.Copy(Path.GetFullPath(firmPath), overlayPath, overwrite: true);
}
else
{
    File.WriteAllText(overlayPath, BuildOverlay(overlayStepRelative, options.PmiLabel, options.PmiValue));
}

var build = FirmamentBuildAndExport.Run(overlayPath, outputStep);
if (!build.IsSuccess) return Fail("Firmament InlineStep PMI export failed: " + string.Join("; ", build.Diagnostics.Select(d => d.Message)));

var outputText = File.ReadAllText(outputStep);
var outputImport = Step242Importer.ImportBody(outputText);
if (!outputImport.IsSuccess) return Fail("Enriched AP242 reimport failed: " + string.Join("; ", outputImport.Diagnostics.Select(d => d.Message)));

var canonicalImport = Step242Importer.ImportBody(File.ReadAllText(canonicalStep));
var inputVolume = BoundingBoxVolume(inputImport.Value);
var canonicalVolume = canonicalImport.IsSuccess ? BoundingBoxVolume(canonicalImport.Value) : double.NaN;
var outputVolume = BoundingBoxVolume(outputImport.Value);
var geometryPreserved = NearlyEqual(inputVolume, outputVolume) && NearlyEqual(canonicalVolume, outputVolume);
var evidence = new[]
{
    $"SHAPE_DIMENSION_REPRESENTATION('diameter:ftc11.{options.PmiLabel}'",
    $"PROPERTY_DEFINITION('diameter:ftc11.{options.PmiLabel}'",
    "MEASURE_REPRESENTATION_ITEM('diameter'," + options.PmiValue.ToString("0.############", CultureInfo.InvariantCulture),
    "diameter"
}.Where(s => outputText.Contains(s, StringComparison.Ordinal)).ToArray();
var pmiEvidenceFound = evidence.Length >= 3;

var report = new DemoReport(inputStep, canonicalStep, overlayPath, outputStep, options.PmiLabel, options.PmiValue.ToString(CultureInfo.InvariantCulture), geometryPreserved, inputVolume, outputVolume, pmiEvidenceFound, evidence);
File.WriteAllText(reportPath, JsonSerializer.Serialize(report, new JsonSerializerOptions { WriteIndented = true, PropertyNamingPolicy = JsonNamingPolicy.CamelCase }));

Console.WriteLine("Aetheris PMI injection demo receipt");
Console.WriteLine($"  input STEP:        {inputStep}");
Console.WriteLine($"  canonical STEP:    {canonicalStep}");
Console.WriteLine($"  Firmament overlay: {overlayPath}");
Console.WriteLine($"  enriched AP242:    {outputStep}");
Console.WriteLine($"  report:            {reportPath}");
Console.WriteLine($"  geometry volume unchanged: {geometryPreserved} ({inputVolume:R} -> {outputVolume:R})");
Console.WriteLine($"  PMI evidence found: {pmiEvidenceFound} ({string.Join(", ", evidence)})");
return geometryPreserved && pmiEvidenceFound ? 0 : 1;

static string BuildOverlay(string canonicalStepPath, string label, double value) => $$"""
// Editable Aetheris/Firmament PMI overlay for the NIST FTC-11 demo.
// The InlineStep input below is the Aetheris-canonical AP242 file produced from
// the copied FTC-11 asset, not the raw vendor/public STEP file.
model AetherisPmiInjectionDemoFtc11 {
    units mm

    solid ftc11: InlineStep {
        path: "{{canonicalStepPath}}"
    }

    pmi {
        diameter {{label}} {
            // #304 is an imported canonical ADVANCED_FACE in the canonicalized FTC-11 demo asset.
            target: ftc11.face("#304")
            value: {{value.ToString("0.############", CultureInfo.InvariantCulture)}}mm
        }
    }
}
""";

static double BoundingBoxVolume(Aetheris.Kernel.Core.Brep.BrepBody body)
{
    var points = body.Topology.Vertices.Select(v => body.TryGetVertexPoint(v.Id, out var p) ? p : throw new InvalidOperationException($"Vertex {v.Id.Value} has no point.")).ToArray();
    var minX = points.Min(p => p.X); var maxX = points.Max(p => p.X);
    var minY = points.Min(p => p.Y); var maxY = points.Max(p => p.Y);
    var minZ = points.Min(p => p.Z); var maxZ = points.Max(p => p.Z);
    return (maxX - minX) * (maxY - minY) * (maxZ - minZ);
}
static bool NearlyEqual(double a, double b) => double.IsFinite(a) && double.IsFinite(b) && Math.Abs(a - b) <= Math.Max(1e-7, Math.Abs(a) * 1e-9);
static int Fail(string message) { Console.Error.WriteLine(message); return 1; }
static string? FindRepoDemoDir()
{
    var dir = new DirectoryInfo(Directory.GetCurrentDirectory());
    while (dir is not null)
    {
        var candidate = Path.Combine(dir.FullName, "demos", "Aetheris.PmiInjectionDemo");
        if (File.Exists(Path.Combine(candidate, "assets", inputFileName))) return candidate;
        dir = dir.Parent;
    }
    return null;
}

sealed record DemoReport(string InputStep, string CanonicalStep, string FirmamentOverlay, string OutputStep, string PmiLabel, string PmiValue, bool GeometryPreserved, double InputVolume, double OutputVolume, bool PmiEvidenceFound, string[] SemanticPmiEvidence);
sealed record DemoOptions(string? OutDir, double PmiValue, string PmiLabel, bool Keep, string? FirmPath)
{
    public static DemoOptions Parse(string[] args)
    {
        string? outDir = null; var value = 32.2d; var label = "demoInnerDiameter"; var keep = false; string? firm = null;
        for (var i = 0; i < args.Length; i++)
        {
            switch (args[i])
            {
                case "--out" when i + 1 < args.Length: outDir = args[++i]; break;
                case "--pmi-value" when i + 1 < args.Length: value = double.Parse(args[++i], CultureInfo.InvariantCulture); break;
                case "--pmi-label" when i + 1 < args.Length: label = args[++i]; break;
                case "--keep": keep = true; break;
                case "--firm" when i + 1 < args.Length: firm = args[++i]; break;
                default: throw new ArgumentException($"Unknown option '{args[i]}'.");
            }
        }
        return new(outDir, value, label, keep, firm);
    }
}
