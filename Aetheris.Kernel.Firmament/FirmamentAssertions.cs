using Aetheris.Kernel.Core.Diagnostics;
using Aetheris.Kernel.Core.Results;
using Aetheris.Kernel.Firmament.Analysis;
using Aetheris.Kernel.Firmament.FirmamentV2;

namespace Aetheris.Kernel.Firmament;

public static partial class FirmamentBuildAndExport
{
    private static KernelResult<FirmamentStepExportResult> EvaluateAssertions(string source, string? sourceDirectory, FirmamentStepExportResult export)
    {
        var document = export.RuntimeAdmissionDocument ?? ParseSemanticSource(source, sourceDirectory).Document;
        var volume = document?.VolumeAssertions ?? [];
        var geometry = document?.GeometryAssertions ?? [];
        if (volume.Count == 0 && geometry.Count == 0) return KernelResult<FirmamentStepExportResult>.Success(export);
        var diagnostics = new List<KernelDiagnostic>();
        void Error(string message, FirmamentV2SourceSpan span) => diagnostics.Add(new(KernelDiagnosticCode.ValidationFailed,
            KernelDiagnosticSeverity.Error, message, "FirmamentV2.Assert") { SourceSpan = new(span.Start, span.Length) });
        FirmamentMeasurements measurements;
        try { measurements = new(export); }
        catch (InvalidOperationException error)
        {
            Error(error.Message, volume.FirstOrDefault()?.SourceSpan ?? geometry[0].SourceSpan);
            return KernelResult<FirmamentStepExportResult>.Failure(diagnostics);
        }
        bool Target(string name, FirmamentV2SourceSpan span)
        {
            if (name == measurements.TargetBodyId) return true;
            Error($"firmament-v2-assert-target-not-exported: '{name}' is not the uniquely identified exported product '{measurements.TargetBodyId ?? "unknown"}'. Assertions never measure a different body under the requested name.", span);
            return false;
        }
        var results = new List<FirmamentV2VolumeAssertionResult>();
        FirmamentVolumeMeasurement? measuredVolume = null;
        foreach (var assertion in volume)
        {
            if (!Target(assertion.TargetBodyId, assertion.SourceSpan)) continue;
            measuredVolume ??= measurements.Volume(volume.Min(a => a.ToleranceMm3) * .25);
            var result = FirmamentV2VolumeAssertionComparer.Compare(assertion, measuredVolume);
            results.Add(result);
            if (!result.Passed) Error(result.Diagnostic!, assertion.SourceSpan);
        }
        var geometryResults = new List<FirmamentGeometryAssertionResult>();
        foreach (var assertion in geometry)
        {
            if (!Target(assertion.TargetBodyId, assertion.SourceSpan)) continue;
            bool passed; string method; string? reason;
            if (assertion.Kind == "Bounds")
            {
                var bounds = measurements.Bounds;
                passed = bounds is not null && Enumerable.Range(0, 3).All(i =>
                    Math.Abs(bounds.MinimumMm[i] - assertion.Minimum![i]) <= assertion.ToleranceMm
                    && Math.Abs(bounds.MaximumMm[i] - assertion.Maximum![i]) <= assertion.ToleranceMm);
                method = bounds?.Method ?? "Unavailable";
                reason = bounds is null ? "Exact world-space bounds are not yet qualified for this body; a sampled or conservative field envelope was not substituted."
                    : $"measuredMinMm=[{string.Join(",", bounds.MinimumMm)}]; measuredMaxMm=[{string.Join(",", bounds.MaximumMm)}]; toleranceMm={assertion.ToleranceMm:G17}";
            }
            else
            {
                var counts = measurements.Topology;
                int Count(string name) => name switch { "Faces" => counts.Faces, "Edges" => counts.Edges, "Vertices" => counts.Vertices, _ => counts.Shells };
                passed = assertion.Counts!.All(pair => Count(pair.Key) == pair.Value);
                method = "ExportedBrepTopology";
                reason = string.Join("; ", assertion.Counts!.Select(pair => $"{pair.Key}: expected={pair.Value}, measured={Count(pair.Key)}"));
            }
            var message = passed ? null : $"firmament-v2-assert-{assertion.Kind.ToLowerInvariant()}-failed: target={assertion.TargetBodyId}; {reason}";
            geometryResults.Add(new(assertion.Id, assertion.Kind, assertion.TargetBodyId, passed, method, message, assertion.SourceSpan));
            if (message is not null) Error(message, assertion.SourceSpan);
        }
        return diagnostics.Count > 0 ? KernelResult<FirmamentStepExportResult>.Failure(diagnostics)
            : KernelResult<FirmamentStepExportResult>.Success(export with { Assertions = results, GeometryAssertions = geometryResults });
    }
}
