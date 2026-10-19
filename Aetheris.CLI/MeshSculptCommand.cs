using System.Text.Json;
using Aetheris.Surfacing;
using Aetheris.Kernel.Core.Brep.Tessellation;
using Aetheris.Kernel.Firmament.FirmamentV2;

namespace Aetheris.CLI;

internal static class MeshSculptCommand
{
    internal static int Write(SculptingCompileResult compile, string source, string? output, string format, string command, bool json, TextWriter stdout, TextWriter stderr)
    {
        var diagnostics = compile.Diagnostics.ToList();
        if (format is not ("stl" or "glb")) diagnostics.Add(new("sculpt-mesh-export-format-unsupported", "MeshState outputs are mesh-only. Use mesh --format stl|glb, or build --out <file.stl|file.glb>; STEP export cannot retain this representation.", compile.OutputMesh?.Name));
        if (!compile.IsSuccess || compile.OutputMesh is null || diagnostics.Count != 0) return Failure();
        var state = compile.OutputMesh;
        var path = Path.GetFullPath(output ?? Path.Combine("artifacts", "local", "mesh-sculpt", Path.GetFileNameWithoutExtension(source) + "." + format));
        var sidecar = path + ".sculpt.json";
        if (path.Equals(Path.GetFullPath(source), StringComparison.OrdinalIgnoreCase) || sidecar.Equals(Path.GetFullPath(source), StringComparison.OrdinalIgnoreCase) || !Path.GetExtension(path).Equals("." + format, StringComparison.OrdinalIgnoreCase))
        { diagnostics.Add(new("mesh-export-path-invalid", "Output must differ from source and have the selected .stl or .glb extension.")); return Failure(); }
        var prepared = SubdivisionSculptor.PrepareExport(state.Cage, state.Construction.MaximumDeviationMm);
        var mesh = prepared.NativeMesh;
        var topology = prepared.Topology;
        var maximumFloatQuantizationMm = prepared.MaximumFloatQuantizationMm;
        // Serialize/validate all payloads before creating any output.
        var recipe = MeshConstructionReplayer.Serialize(state.Construction);
        using var packet = JsonDocument.Parse(recipe);
        var metadata = JsonSerializer.Serialize(new
        {
            schema = SubdivisionSculptor.Schema,
            state.StateId,
            state.PredecessorStateId,
            state.SourceBodyStateId,
            state.ArtifactKind,
            state.StepExportAvailable,
            state.SelfIntersectionStatus,
            state.ConversionDeviationMm,
            state.ConversionEvidence,
            maximumFloatQuantizationMm,
            cage = state.Cage,
            edges = state.Edges,
            construction = packet.RootElement,
            topology
        }, CliRunner.JsonOptions);
        byte[]? glb = format == "glb" ? MeshSculptExport.Glb(state) : null;
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        if (glb is not null) File.WriteAllBytes(path, glb); else BinaryStlExporter.Export(path, mesh);
        File.WriteAllText(sidecar, metadata);
        var report = new
        {
            command,
            success = true,
            output = path,
            format,
            domain = "Sculpting",
            artifactKind = state.ArtifactKind,
            stepExportAvailable = false,
            state.StateId,
            state.SourceBodyStateId,
            state.SelfIntersectionStatus,
            maximumFloatQuantizationMm,
            vertices = mesh.Positions.Count,
            triangles = mesh.TriangleIndices.Count / 3,
            topology,
            provenance = sidecar
        };
        if (json) stdout.WriteLine(JsonSerializer.Serialize(report, CliRunner.JsonOptions)); else stdout.WriteLine($"Built {state.Name}: {path}; {report.triangles} triangles; mesh-only; global self-intersection not certified.");
        return 0;

        int Failure()
        {
            var presented = SculptingAuthoring.KernelDiagnostics(compile with { Diagnostics = diagnostics }).Select(d => FirmamentBuildAdmissionValidation.Present(d, source, File.ReadAllText(source))).ToArray();
            if (json) stdout.WriteLine(JsonSerializer.Serialize(new { command, success = false, domain = "Sculpting", diagnostics = presented }, CliRunner.JsonOptions));
            else foreach (var d in presented) stderr.WriteLine($"{d.SourceFile}:{d.Line}:{d.Column}: {d.Severity} {d.Code}: {d.Message}");
            return 1;
        }
    }
}
