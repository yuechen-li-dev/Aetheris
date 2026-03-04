using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Aetheris.Kernel.Core.Brep;
using Aetheris.Kernel.Core.Brep.Picking;
using Aetheris.Kernel.Core.Brep.Tessellation;
using Aetheris.Kernel.Core.Diagnostics;
using Aetheris.Kernel.Core.Math;
using Aetheris.Kernel.Core.Step242;

namespace Aetheris.Kernel.Core.Tests.Step242;

public sealed class Step242NistAuditHarnessTests
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true
    };

    [Fact]
    public void NistCorpus_AuditReport_IsByteStableAcrossConsecutiveRuns_AndMatchesSnapshot()
    {
        var first = BuildAuditReportJson();
        var second = BuildAuditReportJson();

        Assert.Equal(first, second);

        var snapshotPath = Path.Combine(RepoRoot(), "testdata", "step242", "manifests", "nist.v0.report.json");
        var expected = File.ReadAllText(snapshotPath, Encoding.UTF8);
        if (!string.Equals(expected, first, StringComparison.Ordinal))
        {
            if (string.Equals(Environment.GetEnvironmentVariable("AETHERIS_UPDATE_STEP242_SNAPSHOT"), "1", StringComparison.Ordinal))
            {
                File.WriteAllText(snapshotPath, first, new UTF8Encoding(false));
                return;
            }

            var hint = FirstDiffHint(expected, first);
            Assert.Fail($"NIST audit snapshot mismatch. {hint}");
        }
    }

    private static string BuildAuditReportJson()
    {
        var nistRoot = Path.Combine(RepoRoot(), "testdata", "step242", "nist");
        var files = Directory
            .EnumerateFiles(nistRoot, "*.stp", SearchOption.AllDirectories)
            .Select(path => Path.GetRelativePath(RepoRoot(), path).Replace('\\', '/'))
            .OrderBy(path => path, StringComparer.Ordinal)
            .ToArray();

        var entries = new List<Step242NistAuditEntry>(files.Length);
        foreach (var relativePath in files)
        {
            entries.Add(RunOne(relativePath));
        }

        Assert.All(entries, entry => Assert.False(entry.ExceptionEscaped));

        foreach (var entry in entries.Where(e => string.Equals(e.Status, "success", StringComparison.Ordinal)))
        {
            Assert.NotNull(entry.CanonicalSha256);
            Assert.Matches("^[0-9a-f]{64}$", entry.CanonicalSha256!);
        }

        return JsonSerializer.Serialize(entries, JsonOptions) + "\n";
    }

    private static Step242NistAuditEntry RunOne(string relativePath)
    {
        var fullPath = Path.Combine(RepoRoot(), relativePath.Replace('/', Path.DirectorySeparatorChar));
        var text = File.ReadAllText(fullPath, Encoding.UTF8);
        var canonicalSizeBytes = ComputeCanonicalSizeBytes(text);

        try
        {
            var import = Step242Importer.ImportBody(text);
            if (!import.IsSuccess)
            {
                return BuildFailureEntry(relativePath, canonicalSizeBytes, import.Diagnostics, DetermineImportFailureLayer(import.Diagnostics));
            }

            var body = import.Value;
            var validation = BrepBindingValidator.Validate(body, requireAllEdgeAndFaceBindings: true);
            if (!validation.IsSuccess)
            {
                return BuildFailureEntry(relativePath, canonicalSizeBytes, validation.Diagnostics, "validator", body);
            }

            var tessellation = BrepDisplayTessellator.Tessellate(body);
            if (!tessellation.IsSuccess)
            {
                return BuildFailureEntry(relativePath, canonicalSizeBytes, tessellation.Diagnostics, "tessellator", body);
            }

            var smokeRay = new Ray3D(new Point3D(0.25d, 0.25d, 250d), Direction3D.Create(new Vector3D(0d, 0d, -1d)));
            var pick = BrepPicker.Pick(body, tessellation.Value, smokeRay, PickQueryOptions.Default with { NearestOnly = true });
            if (!pick.IsSuccess)
            {
                return BuildFailureEntry(relativePath, canonicalSizeBytes, pick.Diagnostics, "picker", body, tessellation.Value);
            }

            if (pick.Value.Count == 0)
            {
                return BuildFailureEntry(
                    relativePath,
                    canonicalSizeBytes,
                    [new KernelDiagnostic(KernelDiagnosticCode.InvalidArgument, KernelDiagnosticSeverity.Error, "Picker smoke ray produced no hit.", "Audit.Picker")],
                    "picker",
                    body,
                    tessellation.Value);
            }

            var export = Step242Exporter.ExportBody(body);
            if (!export.IsSuccess)
            {
                return BuildFailureEntry(relativePath, canonicalSizeBytes, export.Diagnostics, "exporter", body, tessellation.Value);
            }

            return new Step242NistAuditEntry(
                FileId(relativePath),
                relativePath,
                canonicalSizeBytes,
                "success",
                FirstFailureLayer: "",
                FirstDiagnostic(import.Diagnostics),
                DiagnosticCount: import.Diagnostics.Count,
                ExceptionEscaped: false,
                TopologyCounts: new Step242TopologyCounts(body.Topology.Vertices.Count(), body.Topology.Edges.Count(), body.Topology.Faces.Count()),
                TessellationCounts: new Step242TessellationCounts(tessellation.Value.FacePatches.Count, tessellation.Value.EdgePolylines.Count),
                CanonicalSha256: ComputeSha256LowerHex(export.Value));
        }
        catch (Exception ex)
        {
            return new Step242NistAuditEntry(
                FileId(relativePath),
                relativePath,
                canonicalSizeBytes,
                Status: "importFail",
                FirstFailureLayer: "importer-topology",
                FirstDiagnostic: new Step242AuditDiagnostic("InvalidArgument", "Audit.Exception", Truncate(ex.Message)),
                DiagnosticCount: 1,
                ExceptionEscaped: true,
                TopologyCounts: Step242TopologyCounts.Zero,
                TessellationCounts: Step242TessellationCounts.Zero,
                CanonicalSha256: null);
        }
    }

    private static Step242NistAuditEntry BuildFailureEntry(
        string relativePath,
        long sizeBytes,
        IReadOnlyList<KernelDiagnostic> diagnostics,
        string layer,
        BrepBody? body = null,
        DisplayTessellationResult? tessellation = null)
    {
        var first = FirstDiagnostic(diagnostics);
        var status = string.Equals(layer, "parser", StringComparison.Ordinal) ? "parseFail" : "importFail";

        return new Step242NistAuditEntry(
            FileId(relativePath),
            relativePath,
            (int)sizeBytes,
            status,
            layer,
            first,
            diagnostics.Count,
            ExceptionEscaped: false,
            TopologyCounts: body is null
                ? Step242TopologyCounts.Zero
                : new Step242TopologyCounts(body.Topology.Vertices.Count(), body.Topology.Edges.Count(), body.Topology.Faces.Count()),
            TessellationCounts: tessellation is null
                ? Step242TessellationCounts.Zero
                : new Step242TessellationCounts(tessellation.FacePatches.Count, tessellation.EdgePolylines.Count),
            CanonicalSha256: null);
    }

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
        var idx = message.IndexOf(':');
        if (idx > 0)
        {
            return message[..idx];
        }

        return Truncate(message);
    }

    private static string Truncate(string value)
    {
        const int max = 120;
        return value.Length <= max ? value : value[..max];
    }

    private static string ComputeSha256LowerHex(string text)
    {
        var bytes = Encoding.UTF8.GetBytes(text);
        var hash = SHA256.HashData(bytes);
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    private static int ComputeCanonicalSizeBytes(string text)
    {
        var normalized = text.Replace("\r\n", "\n", StringComparison.Ordinal).Replace('\r', '\n');
        return Encoding.UTF8.GetByteCount(normalized);
    }

    private static string FileId(string relativePath)
    {
        var stem = Path.GetFileNameWithoutExtension(relativePath);
        var folder = Path.GetDirectoryName(relativePath)?.Replace('\\', '/').Replace('/', '_') ?? string.Empty;
        return string.IsNullOrWhiteSpace(folder) ? stem : $"{folder}_{stem}";
    }

    private static string RepoRoot()
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

    private static string FirstDiffHint(string expected, string actual)
    {
        var max = System.Math.Min(expected.Length, actual.Length);
        for (var i = 0; i < max; i++)
        {
            if (expected[i] != actual[i])
            {
                return $"First difference at byte {i}: expected '{EscapeChar(expected[i])}', actual '{EscapeChar(actual[i])}'. Expected window: '{EscapeWindow(expected, i)}'. Actual window: '{EscapeWindow(actual, i)}'.";
            }
        }

        return $"Length differs: expected {expected.Length}, actual {actual.Length}.";
    }

    private static string EscapeWindow(string value, int index)
    {
        const int radius = 16;
        var start = System.Math.Max(0, index - radius);
        var length = System.Math.Min(value.Length - start, (radius * 2) + 1);
        return EscapeText(value.Substring(start, length));
    }

    private static string EscapeText(string text)
    {
        return text
            .Replace("\\", "\\\\", StringComparison.Ordinal)
            .Replace("\r", "\\r", StringComparison.Ordinal)
            .Replace("\n", "\\n", StringComparison.Ordinal)
            .Replace("\t", "\\t", StringComparison.Ordinal);
    }

    private static string EscapeChar(char value)
    {
        return EscapeText(value.ToString());
    }

    private sealed record Step242NistAuditEntry(
        string FileId,
        string Path,
        int SizeBytes,
        string Status,
        string FirstFailureLayer,
        Step242AuditDiagnostic FirstDiagnostic,
        int DiagnosticCount,
        bool ExceptionEscaped,
        Step242TopologyCounts TopologyCounts,
        Step242TessellationCounts TessellationCounts,
        string? CanonicalSha256);

    private sealed record Step242AuditDiagnostic(string Code, string Source, string MessagePrefix);

    private sealed record Step242TopologyCounts(int Vertices, int Edges, int Faces)
    {
        public static Step242TopologyCounts Zero { get; } = new(0, 0, 0);
    }

    private sealed record Step242TessellationCounts(int FacePatches, int EdgePolylines)
    {
        public static Step242TessellationCounts Zero { get; } = new(0, 0);
    }
}
