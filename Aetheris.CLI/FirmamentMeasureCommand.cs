using System.Globalization;
using System.Text.Json;
using Aetheris.Kernel.Core.Brep.Verification;
using Aetheris.Kernel.Firmament;
using Aetheris.Kernel.Firmament.Analysis;
using Aetheris.Kernel.Firmament.FirmamentV2;

namespace Aetheris.CLI;

internal static class FirmamentMeasureCommand
{
    private const string Usage = "Usage: aetheris measure <part.firmament> [--sdf] [--error <mm3>] [--compare-mesh] [--json]";
    internal static int Run(string[] args, TextWriter stdout, TextWriter stderr, JsonSerializerOptions jsonOptions)
    {
        if (args.Length == 0 || args[0] is "--help" or "-h")
        { CommandHelp.Write(args.Length == 0 ? stderr : stdout, Usage, "Compile a part and report authoritative geometry measurements for inspection or assertions.", "aetheris measure bracket.firmament --json"); return args.Length == 0 ? 1 : 0; }
        var json = args.Contains("--json"); var sdf = false; var mesh = false; double? error = null;
        for (var i = 1; i < args.Length; i++)
            switch (args[i])
            {
                case "--json": break;
                case "--sdf": sdf = true; break;
                case "--compare-mesh": mesh = true; break;
                case "--error" when i + 1 < args.Length && double.TryParse(args[++i], NumberStyles.Float, CultureInfo.InvariantCulture, out var n) && double.IsFinite(n) && n >= 0: error = n; break;
                default: stderr.WriteLine(Usage); return 1;
            }
        var path = Path.GetFullPath(args[0]); var source = File.ReadAllText(path);
        var built = FirmamentBuildAndExport.CompileForMeasurement(source, Path.GetDirectoryName(path));
        if (!built.IsSuccess)
        {
            var diagnostics = built.Diagnostics.Select(d => FirmamentBuildAdmissionValidation.Present(d, path, source)).ToArray();
            if (json) stdout.WriteLine(JsonSerializer.Serialize(new { success = false, geometryBuilt = false, assertionsEvaluated = false, diagnostics }, jsonOptions));
            else foreach (var d in diagnostics) stderr.WriteLine($"{d.SourceFile}:{d.Line}:{d.Column}: {d.Severity} {d.Code}: {d.Message}");
            return 1;
        }
        var measurements = new FirmamentMeasurements(built.Value);
        var volume = measurements.Volume(error, sdf);
        var bounds = measurements.Bounds;
        var topology = measurements.Topology;
        var snippets = new List<string>();
        string N(double value) => value.ToString("G17", CultureInfo.InvariantCulture);
        if (measurements.TargetBodyId is { } target)
        {
            if (volume.ValueMm3 is { } value && volume.ErrorBoundMm3 is { } uncertainty)
                snippets.Add($"Assert Volume {target} {{ expected: {N(value)}mm^3; tolerance: {N(uncertainty == 0 ? 0 : uncertainty * 1.1)}mm^3; }}");
            if (bounds is not null) snippets.Add($"Assert Bounds {target} {{ min: [{string.Join(", ", bounds.MinimumMm.Select(v => N(v) + "mm"))}]; max: [{string.Join(", ", bounds.MaximumMm.Select(v => N(v) + "mm"))}]; tolerance: 0mm; }}");
            snippets.Add($"Assert Topology {target} {{ faces: {topology.Faces}; edges: {topology.Edges}; vertices: {topology.Vertices}; shells: {topology.Shells}; }}");
        }
        var comparison = mesh ? BrepMassProperties.Evaluate(measurements.Body) : null;
        var notes = new[] { "Measurements only: declared assertions were not evaluated. This is not validation success.",
            "Suggested verification tolerances cover numerical uncertainty; choose the design's acceptance range independently.",
            "Bounds are exact only for qualified rectangular stock or planar straight-edge bodies. SDF bounds are enclosing domains, not necessarily the tight bounds of Boolean results." };
        if (json) stdout.WriteLine(JsonSerializer.Serialize(new { command = "measure", success = volume.Authoritative, geometryBuilt = true,
            status = "MeasuredNotAsserted", assertionsEvaluated = false, target = measurements.TargetBodyId, volume, bounds, topology,
            meshComparison = comparison, assertionTemplates = snippets, notes }, jsonOptions));
        else
        {
            stdout.WriteLine("MeasuredNotAsserted — declared assertions were not evaluated.");
            stdout.WriteLine(FormattableString.Invariant($"Volume: {volume.ValueMm3:G17} mm^3; interval [{volume.LowerBoundMm3:G17}, {volume.UpperBoundMm3:G17}]; method={volume.Method}; errorBound={volume.ErrorBoundMm3:G17} mm^3"));
            if (volume.Diagnostic is { } message) stderr.WriteLine(message);
            stdout.WriteLine($"Topology: {topology.Faces} faces, {topology.Edges} edges, {topology.Vertices} vertices, {topology.Shells} shells");
            foreach (var snippet in snippets) stdout.WriteLine(snippet);
            if (comparison is not null) stdout.WriteLine(FormattableString.Invariant($"Mesh comparison (diagnostic only): {comparison.AbsoluteVolume:G17} mm^3; {comparison.EvaluationMethod}"));
            stdout.WriteLine(notes[1]);
        }
        return volume.Authoritative ? 0 : 1;
    }
}
