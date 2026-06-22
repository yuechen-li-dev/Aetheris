using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;
using Aetheris.Kernel.Core.Step242;
using Aetheris.Kernel.Firmament;

const string inputFileName = "nist_ftc_11_asme1_ap242-e2.stp";
const string canonicalFileName = "nist_ftc_11_asme1_ap242-e2.canonical.step";
const string generatedOverlayFileName = "ftc11-pmi-overlay.firm";
const string outputFileName = "ftc11-with-aetheris-pmi.step";
const string reportFileName = "demo-report.json";
const string volumeUnsupportedStatus = "unsupported-curved-trimmed-shell";
const string volumeUnsupportedMessage = "Exact volume integration is currently unsupported for this FTC-11 curved trimmed-shell case; this demo verifies STEP import/reimport and semantic PMI evidence instead of exact FTC-11 volume equality.";

try
{
    var options = DemoOptions.Parse(args);
    var demoDir = AppContext.BaseDirectory;
    var repoDemoDir = FindRepoDemoDir() ?? demoDir;
    var assetInput = Path.Combine(repoDemoDir, "assets", inputFileName);
    var outDir = Path.GetFullPath(options.OutDir ?? Path.Combine(repoDemoDir, "out"));

    if (!File.Exists(assetInput)) return Fail($"Bundled FTC-11 asset not found: {assetInput}");
    if (options.FirmPath is { } requestedFirm && !File.Exists(requestedFirm)) return Fail($"Firmament overlay not found: {Path.GetFullPath(requestedFirm)}");

    Directory.CreateDirectory(outDir);

    var inputStep = Path.Combine(outDir, inputFileName);
    var canonicalStep = Path.Combine(outDir, canonicalFileName);
    var overlayStepRelative = "./" + Path.GetFileName(canonicalStep);
    var overlayPath = Path.Combine(outDir, generatedOverlayFileName);
    var outputStep = Path.Combine(outDir, outputFileName);
    var reportPath = Path.Combine(outDir, reportFileName);
    var externalFirmPath = options.FirmPath is null ? null : Path.GetFullPath(options.FirmPath);

    if (!options.Keep) CleanGeneratedFiles(inputStep, canonicalStep, overlayPath, outputStep, reportPath, externalFirmPath);

    File.Copy(assetInput, inputStep, overwrite: true);
    var inputText = File.ReadAllText(inputStep);
    var inputImport = Step242Importer.ImportBody(inputText);
    if (!inputImport.IsSuccess) return Fail("Input FTC-11 STEP import failed: " + string.Join("; ", inputImport.Diagnostics.Select(d => d.Message)));

    var canonicalExport = Step242Exporter.ExportBody(inputImport.Value);
    if (!canonicalExport.IsSuccess) return Fail("Canonical AP242 export failed: " + string.Join("; ", canonicalExport.Diagnostics.Select(d => d.Message)));
    File.WriteAllText(canonicalStep, canonicalExport.Value);

    var canonicalImport = Step242Importer.ImportBody(File.ReadAllText(canonicalStep));
    if (!canonicalImport.IsSuccess) return Fail("Canonical AP242 import failed: " + string.Join("; ", canonicalImport.Diagnostics.Select(d => d.Message)));

    var overlayUsed = overlayPath;
    if (externalFirmPath is { } firmPath)
    {
        if (!SamePath(firmPath, overlayPath)) File.Copy(firmPath, overlayPath, overwrite: true);
    }
    else
    {
        File.WriteAllText(overlayPath, BuildOverlay(overlayStepRelative, options.PmiLabel, options.PmiValue));
    }

    var build = FirmamentBuildAndExport.Run(overlayUsed, outputStep);
    if (!build.IsSuccess) return Fail("Firmament InlineStep PMI export failed: " + string.Join("; ", build.Diagnostics.Select(d => d.Message)));

    var outputText = File.ReadAllText(outputStep);
    var outputImport = Step242Importer.ImportBody(outputText);
    if (!outputImport.IsSuccess) return Fail("Enriched AP242 reimport failed: " + string.Join("; ", outputImport.Diagnostics.Select(d => d.Message)));

    var expectedEvidence = ExpectedEvidence(options.PmiLabel, options.PmiValue);
    var evidence = expectedEvidence.Where(s => outputText.Contains(s, StringComparison.Ordinal)).ToArray();
    var pmiEvidenceFound = evidence.Length == expectedEvidence.Length;
    if (!pmiEvidenceFound)
    {
        return Fail("PMI evidence missing from enriched AP242. Expected evidence strings: " + string.Join(" | ", expectedEvidence));
    }

    var report = new DemoReport(
        InputNistStep: inputStep,
        CanonicalStep: canonicalStep,
        FirmamentOverlay: overlayPath,
        FirmamentOverlaySource: externalFirmPath,
        OutputStep: outputStep,
        PmiLabel: options.PmiLabel,
        PmiValue: options.PmiValue.ToString("0.############", CultureInfo.InvariantCulture),
        InputStepImported: inputImport.IsSuccess,
        CanonicalStepImported: canonicalImport.IsSuccess,
        OutputStepImported: outputImport.IsSuccess,
        GeometryRoundTripOk: inputImport.IsSuccess && canonicalImport.IsSuccess && outputImport.IsSuccess,
        VolumeCheckSupported: false,
        VolumeCheckStatus: volumeUnsupportedStatus,
        VolumeCheckMessage: volumeUnsupportedMessage,
        InputVolume: null,
        OutputVolume: null,
        VolumeDelta: null,
        VolumeWithinTolerance: null,
        PmiEvidenceFound: pmiEvidenceFound,
        PmiEvidence: evidence,
        ExpectedPmiEvidence: expectedEvidence);
    File.WriteAllText(reportPath, JsonSerializer.Serialize(report, new JsonSerializerOptions { WriteIndented = true, PropertyNamingPolicy = JsonNamingPolicy.CamelCase }));

    PrintReceipt(ToDisplayPath(repoDemoDir, assetInput), ToDisplayPath(repoDemoDir, canonicalStep), ToDisplayPath(repoDemoDir, overlayPath), ToDisplayPath(repoDemoDir, outputStep), ToDisplayPath(repoDemoDir, reportPath));
    return 0;
}
catch (ArgumentException ex)
{
    return Fail(ex.Message);
}

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

static string[] ExpectedEvidence(string label, double value) =>
[
    $"SHAPE_DIMENSION_REPRESENTATION('diameter:ftc11.{label}'",
    $"PROPERTY_DEFINITION('diameter:ftc11.{label}'",
    "MEASURE_REPRESENTATION_ITEM('diameter'," + value.ToString("0.############", CultureInfo.InvariantCulture)
];

static void CleanGeneratedFiles(string inputStep, string canonicalStep, string overlayPath, string outputStep, string reportPath, string? externalFirmPath)
{
    foreach (var path in new[] { inputStep, canonicalStep, overlayPath, outputStep, reportPath })
    {
        if (externalFirmPath is not null && SamePath(path, externalFirmPath)) continue;
        if (File.Exists(path)) File.Delete(path);
    }
}

static void PrintReceipt(string inputStep, string canonicalStep, string overlayPath, string outputStep, string reportPath)
{
    Console.WriteLine("Aetheris FTC-11 AP242 PMI Injection Demo");
    Console.WriteLine("────────────────────────────────────────");
    Console.WriteLine("Input NIST STEP:");
    Console.WriteLine($"  {inputStep}");
    Console.WriteLine();
    Console.WriteLine("Canonical AP242:");
    Console.WriteLine($"  {canonicalStep}");
    Console.WriteLine();
    Console.WriteLine("Firmament overlay:");
    Console.WriteLine($"  {overlayPath}");
    Console.WriteLine();
    Console.WriteLine("Enriched AP242:");
    Console.WriteLine($"  {outputStep}");
    Console.WriteLine();
    Console.WriteLine("Checks:");
    Console.WriteLine("  input import: ok");
    Console.WriteLine("  canonical import: ok");
    Console.WriteLine("  enriched AP242 reimport: ok");
    Console.WriteLine("  PMI evidence: ok");
    Console.WriteLine("  exact volume: unsupported for this curved trimmed-shell FTC-11 case");
    Console.WriteLine();
    Console.WriteLine("Report:");
    Console.WriteLine($"  {reportPath}");
}

static int Fail(string message) { Console.Error.WriteLine(message); return 1; }
static bool SamePath(string left, string right) => string.Equals(Path.GetFullPath(left).TrimEnd(Path.DirectorySeparatorChar), Path.GetFullPath(right).TrimEnd(Path.DirectorySeparatorChar), StringComparison.OrdinalIgnoreCase);
static string ToDisplayPath(string repoDemoDir, string path) => Path.GetRelativePath(repoDemoDir, path).Replace(Path.DirectorySeparatorChar, '/');
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

sealed record DemoReport(string InputNistStep, string CanonicalStep, string FirmamentOverlay, string? FirmamentOverlaySource, string OutputStep, string PmiLabel, string PmiValue, bool InputStepImported, bool CanonicalStepImported, bool OutputStepImported, bool GeometryRoundTripOk, bool VolumeCheckSupported, string VolumeCheckStatus, string VolumeCheckMessage, double? InputVolume, double? OutputVolume, double? VolumeDelta, bool? VolumeWithinTolerance, bool PmiEvidenceFound, string[] PmiEvidence, string[] ExpectedPmiEvidence);
sealed record DemoOptions(string? OutDir, double PmiValue, string PmiLabel, bool Keep, string? FirmPath)
{
    private static readonly Regex Identifier = new("^[A-Za-z_][A-Za-z0-9_]*$", RegexOptions.CultureInvariant);

    public static DemoOptions Parse(string[] args)
    {
        string? outDir = null; var value = 32.2d; var label = "demoInnerDiameter"; var keep = false; string? firm = null;
        for (var i = 0; i < args.Length; i++)
        {
            switch (args[i])
            {
                case "--out" when i + 1 < args.Length: outDir = args[++i]; break;
                case "--pmi-value" when i + 1 < args.Length:
                    if (!double.TryParse(args[++i], NumberStyles.Float, CultureInfo.InvariantCulture, out value)) throw new ArgumentException("Invalid --pmi-value. Use a positive finite number in invariant-culture format, for example 33.0.");
                    break;
                case "--pmi-label" when i + 1 < args.Length: label = args[++i]; break;
                case "--keep": keep = true; break;
                case "--firm" when i + 1 < args.Length: firm = args[++i]; break;
                default: throw new ArgumentException($"Unknown or incomplete option '{args[i]}'.");
            }
        }

        if (!double.IsFinite(value) || value <= 0d) throw new ArgumentException("Invalid --pmi-value. Value must be a positive finite number.");
        if (string.IsNullOrWhiteSpace(label)) throw new ArgumentException("Invalid --pmi-label. Label must be a non-empty Firmament identifier.");
        if (!Identifier.IsMatch(label)) throw new ArgumentException("Invalid --pmi-label. Label must be a simple Firmament identifier: letters, digits, or underscores, not starting with a digit.");
        return new(outDir, value, label, keep, firm);
    }
}
