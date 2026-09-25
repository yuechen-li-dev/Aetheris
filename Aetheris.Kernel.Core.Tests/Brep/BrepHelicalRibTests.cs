using Aetheris.Kernel.Core.Brep;
using Aetheris.Kernel.Core.Brep.Features;
using Aetheris.Kernel.Core.Brep.Tessellation;
using Aetheris.Kernel.Core.Geometry;
using Aetheris.Kernel.Core.Math;
using Aetheris.Kernel.Core.Step242;
using System.Diagnostics;
using System.Text;
using System.Text.Json;

namespace Aetheris.Kernel.Core.Tests.Brep;

public sealed class BrepHelicalRibTests
{
    [Theory]
    [InlineData(1)]
    [InlineData(4)]
    [InlineData(24)]
    public void CompleteTurnRibProducesOneClosedManifoldBody(int turns)
    {
        const double pitch = 1.25d;
        const double width = 1.1d;
        var span = turns * pitch;
        var parameters = new HelicalRibParameters(Point3D.Origin,
            Direction3D.Create(new Vector3D(0, 0, 1)),
            Direction3D.Create(new Vector3D(1, 0, 0)),
            -1d, span + width + 1d, 3.2d, 4d, pitch,
            width / 2d, span + width / 2d, width, 0.1d);
        var rib = HelicalRibGeometry.Create(parameters);
        Assert.True(rib.IsSuccess);
        var result = BrepHelicalRib.Create(rib.Value);
        Assert.True(result.IsSuccess, string.Join(Environment.NewLine, result.Diagnostics));
        Assert.Equal(4 * turns + 5, result.Value.Body.Topology.Faces.Count());
        Assert.Equal(8 * turns + 9, result.Value.Body.Topology.Edges.Count());
        Assert.Equal(4 * turns + 6, result.Value.Body.Topology.Vertices.Count());
        Assert.Same(rib.Value, result.Value.Authority);
        Assert.Equal(3 * turns, result.Value.QualifiedSideFaces.Count);
        Assert.All(result.Value.QualifiedSideFaces.Values, surface =>
        {
            Assert.Equal(parameters.PitchMm, surface.Authority.Parameters.PitchMm);
            Assert.True(surface.CertifiedDeviationBoundMm <= surface.RequestedToleranceMm);
        });
        foreach (var edge in result.Value.Body.Topology.Edges)
        {
            var uses = result.Value.Body.Topology.Coedges.Where(item => item.EdgeId == edge.Id).ToArray();
            Assert.Equal(2, uses.Length);
            Assert.NotEqual(uses[0].IsReversed, uses[1].IsReversed);
        }
        var pcurves = BrepPcurveValidator.Validate(result.Value.Body, 1e-6d);
        Assert.True(pcurves.IsValid, string.Join(Environment.NewLine, pcurves.Diagnostics.Take(12)));
    }

    [Theory]
    [InlineData(1)]
    [InlineData(24)]
    public void CanonicalRibExportsAndReimports(int turns)
    {
        const double width = 1.1d;
        var span = turns * 1.25d;
        var parameters = new HelicalRibParameters(Point3D.Origin,
            Direction3D.Create(new Vector3D(0, 0, 1)),
            Direction3D.Create(new Vector3D(1, 0, 0)),
            -1d, span + width + 1d, 3.2d, 4d, 1.25d,
            width / 2d, span + width / 2d, width, 0.1d);
        var rib = HelicalRibGeometry.Create(parameters).Value;
        var body = BrepHelicalRib.Create(rib).Value.Body;
        var export = Step242Exporter.ExportBody(body, new Step242ExportOptions { BrepExportPreflightMode = BrepExportPreflightMode.Enforce });
        Assert.True(export.IsSuccess, string.Join(Environment.NewLine, export.Diagnostics));
        var imported = Step242Importer.ImportBody(export.Value);
        Assert.True(imported.IsSuccess, string.Join(Environment.NewLine, imported.Diagnostics));
        Assert.True(BrepExportPreflight.Validate(imported.Value).IsValid);
        Assert.Equal(body.Topology.Faces.Count(), imported.Value.Topology.Faces.Count());
        var points = imported.Value.Topology.Vertices.Select(vertex =>
        {
            Assert.True(imported.Value.TryGetVertexPoint(vertex.Id, out var point));
            return point;
        }).ToArray();
        Assert.Contains(points, point => double.Abs(point.X - 4d) < 1e-6d && double.Abs(point.Z - 0.5d) < 1e-6d);
        Assert.Contains(points, point => double.Abs(point.X - 3.2d) < 1e-6d && double.Abs(point.Z) < 1e-6d);
        Assert.Contains(points, point => double.Abs(point.X - 3.2d) < 1e-6d && double.Abs(point.Z - 1.25d) < 1e-6d);
        Assert.Contains(points, point => double.Abs(point.Z + 1d) < 1e-6d);
        Assert.Contains(points, point => double.Abs(point.Z - (span + width + 1d)) < 1e-6d);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(24)]
    public void DisplayTessellatorCoversEveryFace(int turns)
    {
        const double width = 1.1d;
        var span = turns * 1.25d;
        var parameters = new HelicalRibParameters(Point3D.Origin,
            Direction3D.Create(new Vector3D(0, 0, 1)),
            Direction3D.Create(new Vector3D(1, 0, 0)),
            -1d, span + width + 1d, 3.2d, 4d, 1.25d,
            width / 2d, span + width / 2d, width, 0.1d);
        var body = BrepHelicalRib.Create(HelicalRibGeometry.Create(parameters).Value).Value.Body;
        var mesh = BrepDisplayTessellator.TessellateBounded(body, executionTimeout: TimeSpan.FromSeconds(30));
        Assert.True(mesh.IsSuccess, string.Join(Environment.NewLine, mesh.Diagnostics.Take(12)));
        Assert.Equal(body.Topology.Faces.Count(), mesh.Value.FacePatches.Count);
        Assert.All(mesh.Value.FacePatches, patch => Assert.NotEmpty(patch.TriangleIndices));
        var rootRadius = 3.2d;
        var crestRadius = 4d;
        var widthSlope = (0.1d - width) / (crestRadius - rootRadius);
        var ribSectionMoment = width * (crestRadius * crestRadius - rootRadius * rootRadius) / 2d
            + widthSlope * ((double.Pow(crestRadius, 3) - double.Pow(rootRadius, 3)) / 3d
                - rootRadius * (crestRadius * crestRadius - rootRadius * rootRadius) / 2d);
        var expectedVolume = double.Pi * rootRadius * rootRadius * (span + width + 2d)
            + turns * 2d * double.Pi * ribSectionMoment;
        Assert.InRange(SignedMeshVolume(mesh.Value) / expectedVolume, 0.99d, 1.01d);
    }

    [Theory]
    [InlineData(1.25d, 4d, false)]
    [InlineData(1.5d, 4d, false)]
    [InlineData(1.25d, 4.2d, false)]
    [InlineData(1.25d, 4d, true)]
    public void ChangedPitchDepthAndAxisRemainManifold(double pitch, double crestRadius, bool rotated)
    {
        var axis = rotated ? Direction3D.Create(new Vector3D(0, 1, 0)) : Direction3D.Create(new Vector3D(0, 0, 1));
        var radial = rotated ? Direction3D.Create(new Vector3D(0, 0, 1)) : Direction3D.Create(new Vector3D(1, 0, 0));
        var parameters = new HelicalRibParameters(new Point3D(10, 20, 30), axis, radial,
            -1d, 4d * pitch + 2.1d, 3.2d, crestRadius, pitch,
            0.55d, 4d * pitch + 0.55d, 1.1d, 0.1d, 0.4d);
        var rib = HelicalRibGeometry.Create(parameters);
        Assert.True(rib.IsSuccess, string.Join(Environment.NewLine, rib.Diagnostics));
        var result = BrepHelicalRib.Create(rib.Value);
        Assert.True(result.IsSuccess, string.Join(Environment.NewLine, result.Diagnostics));
        Assert.Equal(21, result.Value.Body.Topology.Faces.Count());
        Assert.Equal(3, result.Value.SeamSplits);
        Assert.Equal(pitch, result.Value.Authority.MinimumAdjacentTurnClearanceMm + 1.1d, 9);
        Assert.True(BrepPcurveValidator.Validate(result.Value.Body, 1e-6d).IsValid);
    }

    [Fact]
    public void RejectsSupportEndContactBeforeTopologyAssembly()
    {
        var parameters = new HelicalRibParameters(Point3D.Origin,
            Direction3D.Create(new Vector3D(0, 0, 1)),
            Direction3D.Create(new Vector3D(1, 0, 0)),
            0d, 6.1d, 3.2d, 4d, 1.25d, 0.55d, 5.55d, 1.1d, 0.1d);
        var rib = HelicalRibGeometry.Create(parameters);
        Assert.True(rib.IsSuccess);
        var result = BrepHelicalRib.Create(rib.Value);
        Assert.False(result.IsSuccess);
        Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Message.Contains("support-end-contact-unsupported"));
    }

    [Fact]
    public void WriteCanonicalWitnessWhenRequested()
    {
        var output = Environment.GetEnvironmentVariable("AETHERIS_HELICAL_RIB_ARTIFACT_DIR");
        if (string.IsNullOrWhiteSpace(output)) return;
        Directory.CreateDirectory(output);
        var parameters = new HelicalRibParameters(Point3D.Origin,
            Direction3D.Create(new Vector3D(0, 0, 1)),
            Direction3D.Create(new Vector3D(1, 0, 0)),
            -1d, 32.1d, 3.2d, 4d, 1.25d,
            0.55d, 30.55d, 1.1d, 0.1d);
        var timer = Stopwatch.StartNew();
        var rib = HelicalRibGeometry.Create(parameters).Value;
        var result = BrepHelicalRib.Create(rib);
        Assert.True(result.IsSuccess, string.Join(Environment.NewLine, result.Diagnostics));
        var buildMs = timer.Elapsed.TotalMilliseconds;
        timer.Restart();
        var mesh = BrepDisplayTessellator.TessellateBounded(result.Value.Body,
            executionTimeout: TimeSpan.FromSeconds(60));
        Assert.True(mesh.IsSuccess, string.Join(Environment.NewLine, mesh.Diagnostics));
        var tessellationMs = timer.Elapsed.TotalMilliseconds;
        timer.Restart();
        var step = Step242Exporter.ExportBody(result.Value.Body,
            new Step242ExportOptions { BrepExportPreflightMode = BrepExportPreflightMode.Enforce });
        Assert.True(step.IsSuccess, string.Join(Environment.NewLine, step.Diagnostics));
        var exportMs = timer.Elapsed.TotalMilliseconds;
        var stepPath = Path.Combine(output, "helical-rib-24.step");
        File.WriteAllText(stepPath, step.Value);
        timer.Restart();
        var imported = Step242Importer.ImportBody(step.Value);
        Assert.True(imported.IsSuccess, string.Join(Environment.NewLine, imported.Diagnostics));
        var reimportMs = timer.Elapsed.TotalMilliseconds;
        var buildTimes = new List<object>();
        foreach (var turns in new[] { 1, 4, 24 })
        {
            var variant = parameters with
            {
                AxialEndMm = turns * 1.25d + 0.55d,
                SupportAxialMaxMm = turns * 1.25d + 2.1d
            };
            timer.Restart();
            var variantResult = BrepHelicalRib.Create(HelicalRibGeometry.Create(variant).Value);
            Assert.True(variantResult.IsSuccess, string.Join(Environment.NewLine, variantResult.Diagnostics));
            buildTimes.Add(new { turns, milliseconds = timer.Elapsed.TotalMilliseconds });
        }
        var obj = new StringBuilder();
        var vertexOffset = 1;
        foreach (var patch in mesh.Value.FacePatches)
        {
            obj.AppendLine($"g Face_{patch.FaceId.Value}");
            foreach (var point in patch.Positions)
                obj.AppendLine(FormattableString.Invariant($"v {point.X:R} {point.Y:R} {point.Z:R}"));
            for (var i = 0; i < patch.TriangleIndices.Count; i += 3)
                obj.AppendLine($"f {vertexOffset + patch.TriangleIndices[i]} {vertexOffset + patch.TriangleIndices[i + 1]} {vertexOffset + patch.TriangleIndices[i + 2]}");
            vertexOffset += patch.Positions.Count;
        }
        File.WriteAllText(Path.Combine(output, "helical-rib-24.obj"), obj.ToString());
        var report = new
        {
            turns = 24,
            faces = result.Value.Body.Topology.Faces.Count(),
            edges = result.Value.Body.Topology.Edges.Count(),
            vertices = result.Value.Body.Topology.Vertices.Count(),
            seamSplits = result.Value.SeamSplits,
            triangles = mesh.Value.FacePatches.Sum(patch => patch.TriangleIndices.Count / 3),
            buildMs, tessellationMs, exportMs, reimportMs, buildTimes,
            stepBytes = new FileInfo(stepPath).Length,
            signedMeshVolume = SignedMeshVolume(mesh.Value)
        };
        File.WriteAllText(Path.Combine(output, "helical-rib-24.json"), JsonSerializer.Serialize(report, new JsonSerializerOptions { WriteIndented = true }));
    }

    private static double SignedMeshVolume(DisplayTessellationResult mesh)
    {
        var volume = 0d;
        foreach (var patch in mesh.FacePatches)
            for (var i = 0; i < patch.TriangleIndices.Count; i += 3)
            {
                var a = patch.Positions[patch.TriangleIndices[i]];
                var b = patch.Positions[patch.TriangleIndices[i + 1]];
                var c = patch.Positions[patch.TriangleIndices[i + 2]];
                volume += new Vector3D(a.X, a.Y, a.Z).Dot(
                    new Vector3D(b.X, b.Y, b.Z).Cross(new Vector3D(c.X, c.Y, c.Z))) / 6d;
            }
        return volume;
    }
}
