using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Aetheris.Kernel.Core.Brep;
using Aetheris.Kernel.Core.Brep.Picking;
using Aetheris.Kernel.Core.Brep.Tessellation;
using Aetheris.Kernel.Core.Diagnostics;
using Aetheris.Kernel.Core.Math;
using Aetheris.Kernel.Core.Results;
using Aetheris.Kernel.Core.Step242;

namespace Aetheris.Kernel.Core.Tests.Step242;

internal static class Step242CorpusManifestRunner
{
    private static readonly Regex PositionInParensRegex = new(@"\(position\s+\d+\)", RegexOptions.CultureInvariant | RegexOptions.Compiled);
    private static readonly Regex PositionTokenRegex = new(@"position\s+\d+", RegexOptions.CultureInvariant | RegexOptions.Compiled);

    public static Step242CorpusManifest LoadManifest(string relativePath)
    {
        var fullPath = Path.Combine(RepoRoot(), relativePath.Replace('/', Path.DirectorySeparatorChar));
        var json = File.ReadAllText(fullPath, Encoding.UTF8);
        var manifest = JsonSerializer.Deserialize<Step242CorpusManifest>(json, new JsonSerializerOptions(JsonSerializerDefaults.Web));
        if (manifest is null)
        {
            throw new InvalidOperationException($"Unable to deserialize manifest: {relativePath}");
        }

        return manifest;
    }

    public static string BuildReportJson(IEnumerable<Step242CorpusManifestEntry> entries)
    {
        var reportEntries = entries
            .OrderBy(e => e.Path, StringComparer.Ordinal)
            .Select(RunOne)
            .ToArray();

        var report = JsonSerializer.Serialize(reportEntries, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = true
        });

        return NormalizeLf(report) + "\n";
    }

    public static Step242CorpusReportEntry RunOne(Step242CorpusManifestEntry entry)
    {
        var fullPath = Path.Combine(RepoRoot(), entry.Path.Replace('/', Path.DirectorySeparatorChar));
        var text = File.ReadAllText(fullPath, Encoding.UTF8);
        var sizeBytes = Encoding.UTF8.GetByteCount(NormalizeLf(text));

        try
        {
            var import = Step242Importer.ImportBody(text);
            if (!import.IsSuccess)
            {
                return BuildAp242Failure(entry, sizeBytes, DetermineImportFailureLayer(import.Diagnostics), import.Diagnostics);
            }

            var body = import.Value;
            var validation = BrepBindingValidator.Validate(body, requireAllEdgeAndFaceBindings: true);
            if (!validation.IsSuccess)
            {
                return BuildAp242Failure(entry, sizeBytes, "validator", validation.Diagnostics, body);
            }

            var export = Step242Exporter.ExportBody(body);
            if (!export.IsSuccess)
            {
                return BuildAp242Failure(entry, sizeBytes, "exporter", export.Diagnostics, body);
            }

            var displayAudit = RunDisplayAudit(body);
            return new Step242CorpusReportEntry(
                entry.Id,
                entry.Path,
                entry.Group,
                sizeBytes,
                Status: "success",
                FirstFailureLayer: string.Empty,
                FirstDiagnostic: FirstDiagnostic(import.Diagnostics),
                DiagnosticCount: import.Diagnostics.Count,
                ExceptionEscaped: false,
                DisplayStatus: displayAudit.Status,
                DisplayFirstFailureLayer: displayAudit.FirstFailureLayer,
                DisplayFirstDiagnostic: displayAudit.FirstDiagnostic,
                DisplayDiagnosticCount: displayAudit.DiagnosticCount,
                TopologyCounts: new Step242TopologyCounts(body.Topology.Vertices.Count(), body.Topology.Edges.Count(), body.Topology.Faces.Count()),
                TessellationCounts: displayAudit.TessellationCounts,
                CanonicalSha256: ComputeSha256LowerHex(export.Value),
                ExportedCanonicalText: export.Value);
        }
        catch (Exception ex)
        {
            return new Step242CorpusReportEntry(
                entry.Id,
                entry.Path,
                entry.Group,
                sizeBytes,
                Status: "importFail",
                FirstFailureLayer: "importer-topology",
                FirstDiagnostic: new Step242AuditDiagnostic("InvalidOperation", "Audit.Exception", StableMessagePrefix(ex.Message)),
                DiagnosticCount: 1,
                ExceptionEscaped: true,
                DisplayStatus: "notRun",
                DisplayFirstFailureLayer: string.Empty,
                DisplayFirstDiagnostic: new Step242AuditDiagnostic("Unknown", "Audit.None", "Not run."),
                DisplayDiagnosticCount: 0,
                TopologyCounts: Step242TopologyCounts.Zero,
                TessellationCounts: Step242TessellationCounts.Zero,
                CanonicalSha256: null,
                ExportedCanonicalText: null);
        }
    }

    public static string NormalizeLf(string value) => value.Replace("\r\n", "\n", StringComparison.Ordinal).Replace("\r", "\n", StringComparison.Ordinal);

    public static string RepoRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "Aetheris.slnx")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new InvalidOperationException("Unable to locate repository root.");
    }

    private static string ComputeSha256LowerHex(string text)
    {
        var bytes = Encoding.UTF8.GetBytes(text);
        var hash = SHA256.HashData(bytes);
        return Convert.ToHexString(hash).ToLowerInvariant();
    }


    private static Ray3D BuildSmokeRay(DisplayTessellationResult tessellation)
    {
        var points = tessellation.FacePatches.SelectMany(p => p.Positions).ToArray();
        if (points.Length == 0)
        {
            return new Ray3D(new Point3D(0.25d, 0.25d, 250d), Direction3D.Create(new Vector3D(0d, 0d, -1d)));
        }

        var minX = points.Min(p => p.X);
        var maxX = points.Max(p => p.X);
        var minY = points.Min(p => p.Y);
        var maxY = points.Max(p => p.Y);
        var maxZ = points.Max(p => p.Z);

        var origin = new Point3D((minX + maxX) * 0.5d, (minY + maxY) * 0.5d, maxZ + 10d);
        return new Ray3D(origin, Direction3D.Create(new Vector3D(0d, 0d, -1d)));
    }

    private static KernelResult<IReadOnlyList<PickHit>> TryPickSmoke(BrepBody body, DisplayTessellationResult tessellation)
    {
        foreach (var ray in BuildSmokeRays(tessellation))
        {
            var pick = BrepPicker.Pick(body, tessellation, ray, PickQueryOptions.Default with { NearestOnly = true });
            if (pick.IsSuccess && pick.Value.Count > 0)
            {
                return pick;
            }
        }

        return KernelResult<IReadOnlyList<PickHit>>.Failure([
            new KernelDiagnostic(KernelDiagnosticCode.ValidationFailed, KernelDiagnosticSeverity.Error, "Picker smoke ray produced no hit.", "Audit.Picker")
        ]);
    }

    private static IReadOnlyList<Ray3D> BuildSmokeRays(DisplayTessellationResult tessellation)
    {
        var points = tessellation.FacePatches.SelectMany(p => p.Positions).ToArray();
        if (points.Length == 0)
        {
            return [new Ray3D(new Point3D(0.25d, 0.25d, 250d), Direction3D.Create(new Vector3D(0d, 0d, -1d)))];
        }

        var minX = points.Min(p => p.X);
        var maxX = points.Max(p => p.X);
        var minY = points.Min(p => p.Y);
        var maxY = points.Max(p => p.Y);
        var minZ = points.Min(p => p.Z);
        var maxZ = points.Max(p => p.Z);
        var cx = (minX + maxX) * 0.5d;
        var cy = (minY + maxY) * 0.5d;
        var cz = (minZ + maxZ) * 0.5d;

        return
        [
            new Ray3D(new Point3D(cx, cy, maxZ + 10d), Direction3D.Create(new Vector3D(0d, 0d, -1d))),
            new Ray3D(new Point3D(maxX + 10d, cy, cz), Direction3D.Create(new Vector3D(-1d, 0d, 0d))),
            new Ray3D(new Point3D(cx, maxY + 10d, cz), Direction3D.Create(new Vector3D(0d, -1d, 0d)))
        ];
    }

    private static Step242CorpusReportEntry BuildAp242Failure(
        Step242CorpusManifestEntry entry,
        int sizeBytes,
        string layer,
        IReadOnlyList<KernelDiagnostic> diagnostics,
        BrepBody? body = null)
    {
        return new Step242CorpusReportEntry(
            entry.Id,
            entry.Path,
            entry.Group,
            sizeBytes,
            Status: ClassifyAp242Status(layer),
            FirstFailureLayer: layer,
            FirstDiagnostic: FirstDiagnostic(diagnostics),
            DiagnosticCount: diagnostics.Count,
            ExceptionEscaped: false,
            DisplayStatus: "notRun",
            DisplayFirstFailureLayer: string.Empty,
            DisplayFirstDiagnostic: new Step242AuditDiagnostic("Unknown", "Audit.None", "Not run."),
            DisplayDiagnosticCount: 0,
            TopologyCounts: body is null ? Step242TopologyCounts.Zero : new Step242TopologyCounts(body.Topology.Vertices.Count(), body.Topology.Edges.Count(), body.Topology.Faces.Count()),
            TessellationCounts: Step242TessellationCounts.Zero,
            CanonicalSha256: null,
            ExportedCanonicalText: null);
    }

    private static string ClassifyAp242Status(string layer)
    {
        if (string.Equals(layer, "parser", StringComparison.Ordinal))
        {
            return "parseFail";
        }

        return "importFail";
    }

    private static DisplayAuditResult RunDisplayAudit(BrepBody body)
    {
        var tessellation = BrepDisplayTessellator.Tessellate(body);
        if (!tessellation.IsSuccess)
        {
            return new DisplayAuditResult(
                "tessellationFail",
                "tessellator",
                FirstDiagnostic(tessellation.Diagnostics),
                tessellation.Diagnostics.Count,
                Step242TessellationCounts.Zero);
        }

        var counts = new Step242TessellationCounts(tessellation.Value.FacePatches.Count, tessellation.Value.EdgePolylines.Count);
        var pick = TryPickSmoke(body, tessellation.Value);
        if (pick.IsSuccess)
        {
            return new DisplayAuditResult("success", string.Empty, FirstDiagnostic(pick.Diagnostics), pick.Diagnostics.Count, counts);
        }

        var status = HasTruthfulTessellationSkip(tessellation.Diagnostics)
            ? "pickerBlockedByTessellationSkip"
            : "pickerFail";
        return new DisplayAuditResult(status, "picker", FirstDiagnostic(pick.Diagnostics), pick.Diagnostics.Count, counts);
    }

    private static bool HasTruthfulTessellationSkip(IReadOnlyList<KernelDiagnostic>? tessellationDiagnostics)
        => tessellationDiagnostics?.Any(d => string.Equals(d.Source, "Viewer.Tessellation.TrimEvaluationFailed", StringComparison.Ordinal)) == true;

    private static Step242AuditDiagnostic FirstDiagnostic(IReadOnlyList<KernelDiagnostic> diagnostics)
    {
        if (diagnostics.Count == 0)
        {
            return new Step242AuditDiagnostic("Unknown", "Audit.None", "No diagnostics.");
        }

        var d = diagnostics[0];
        return new Step242AuditDiagnostic(d.Code.ToString(), NormalizeSource(d.Source ?? string.Empty), StableMessagePrefix(d.Message));
    }

    private static string DetermineImportFailureLayer(IReadOnlyList<KernelDiagnostic> diagnostics)
    {
        if (diagnostics.Count == 0)
        {
            return "importer-topology";
        }

        var source = diagnostics[0].Source ?? string.Empty;
        if (source.StartsWith("Parser", StringComparison.Ordinal))
        {
            return "parser";
        }

        if (source.StartsWith("Importer.Geometry", StringComparison.Ordinal))
        {
            return "importer-geometry";
        }

        return "importer-topology";
    }

    private static string NormalizeSource(string source)
    {
        var split = source.IndexOf('|', StringComparison.Ordinal);
        return split > 0 ? source[..split] : source;
    }

    private static string StableMessagePrefix(string message)
    {
        var normalized = NormalizeForSnapshot(message);
        var idx = normalized.IndexOf(':');
        return idx > 0 ? normalized[..idx].TrimEnd() : Truncate(normalized.TrimEnd());
    }

    private static string NormalizeForSnapshot(string value)
    {
        var normalized = value.Replace("\r\n", "\n", StringComparison.Ordinal).Replace('\r', '\n');
        normalized = PositionInParensRegex.Replace(normalized, "(position *)");
        normalized = PositionTokenRegex.Replace(normalized, "position *");
        return normalized;
    }

    private static string Truncate(string value)
    {
        const int max = 120;
        return value.Length <= max ? value : value[..max];
    }
}

internal sealed record Step242CorpusManifest(
    string Version,
    IReadOnlyList<Step242CorpusManifestEntry> Entries);

internal sealed record Step242CorpusManifestEntry(
    string Id,
    string Path,
    string Group,
    string? Notes,
    Step242ExpectedDiagnostic? ExpectedFirstDiagnostic,
    bool? ExpectHashStableAfterCanonicalization,
    Step242ExpectedTopologyCounts? ExpectTopologyCounts,
    Step242ExpectedGeometryKinds? ExpectGeometryKinds);

internal sealed record Step242ExpectedDiagnostic(string Code, string Source, string MessagePrefix);

internal sealed record Step242ExpectedTopologyCounts(int V, int E, int F);

internal sealed record Step242ExpectedGeometryKinds(IReadOnlyList<string>? Surfaces, IReadOnlyList<string>? Curves);

internal sealed record Step242CorpusReportEntry(
    string Id,
    string Path,
    string Group,
    int SizeBytes,
    string Status,
    string FirstFailureLayer,
    Step242AuditDiagnostic FirstDiagnostic,
    int DiagnosticCount,
    bool ExceptionEscaped,
    string DisplayStatus,
    string DisplayFirstFailureLayer,
    Step242AuditDiagnostic DisplayFirstDiagnostic,
    int DisplayDiagnosticCount,
    Step242TopologyCounts TopologyCounts,
    Step242TessellationCounts TessellationCounts,
    string? CanonicalSha256,
    string? ExportedCanonicalText);

internal sealed record Step242AuditDiagnostic(string Code, string Source, string MessagePrefix);

internal sealed record Step242TopologyCounts(int Vertices, int Edges, int Faces)
{
    public static Step242TopologyCounts Zero { get; } = new(0, 0, 0);
}

internal sealed record Step242TessellationCounts(int FacePatches, int EdgePolylines)
{
    public static Step242TessellationCounts Zero { get; } = new(0, 0);
}

internal sealed record DisplayAuditResult(
    string Status,
    string FirstFailureLayer,
    Step242AuditDiagnostic FirstDiagnostic,
    int DiagnosticCount,
    Step242TessellationCounts TessellationCounts);
