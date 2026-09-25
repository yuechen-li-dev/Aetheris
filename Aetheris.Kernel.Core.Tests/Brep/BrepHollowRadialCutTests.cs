using Aetheris.Kernel.Core.Brep;
using Aetheris.Kernel.Core.Brep.Features;
using Aetheris.Kernel.Core.Brep.Tessellation;
using Aetheris.Kernel.Core.Brep.Verification;
using Aetheris.Kernel.Core.Geometry;
using Aetheris.Kernel.Core.Geometry.Surfaces;
using Aetheris.Kernel.Core.Math;
using Aetheris.Kernel.Core.Step242;
using System.Diagnostics;
using System.Text.Json;

namespace Aetheris.Kernel.Core.Tests.Brep;

public sealed class BrepHollowRadialCutTests
{
    [Fact]
    public void CanonicalLocalHole_IsManifoldAndRoundTrips()
    {
        var hollow = ThinWalledBodyBRepPlanner.CreateCylinder(56, 142, 0.8).Value;
        var buildStart = Stopwatch.GetTimestamp();
        var built = BrepHollowRadialCut.Build(hollow, 4.5, 71, 0.4);
        var buildTime = Stopwatch.GetElapsedTime(buildStart);
        Assert.True(built.IsSuccess, string.Join("; ", built.Diagnostics.Select(d => $"{d.Source}: {d.Message}")));
        var body = built.Value.Body;
        Assert.Equal(6, body.Topology.Faces.Count());
        Assert.Equal(11, body.Topology.Edges.Count());
        Assert.Equal(8, body.Topology.Vertices.Count());
        Assert.Equal(3, body.Bindings.FaceBoundaryRoleBindings.Count(binding => binding.Role == FaceBoundaryRole.Inner));
        Assert.Equal("RadialHole.OuterOpening", built.Value.Identity.OuterOpening);
        Assert.Equal("RadialHole.InnerOpening", built.Value.Identity.InnerOpening);
        Assert.Equal("RadialHole.Wall", built.Value.Identity.Wall);
        Assert.Contains(body.Topology.Faces, face => face.Id == built.Value.TopologyMap.Faces[built.Value.Identity.Wall]);
        foreach (var name in new[] { built.Value.Identity.OuterOpening, built.Value.Identity.InnerOpening })
        {
            var loop = built.Value.TopologyMap.Loops[name];
            Assert.Contains(body.Bindings.FaceBoundaryRoleBindings, binding => binding.LoopId == loop && binding.Role == FaceBoundaryRole.Inner);
            Assert.Equal(2, built.Value.TopologyMap.Edges[name].Count);
        }
        Assert.Equal(2, body.Bindings.EdgeBindings.Count(binding => body.Geometry.GetCurve(binding.CurveGeometryId).CertifiedIntersection?.Authority.Host.Radius == 56));
        Assert.Equal(2, body.Bindings.EdgeBindings.Count(binding => body.Geometry.GetCurve(binding.CurveGeometryId).CertifiedIntersection?.Authority.Host.Radius == 55.2));
        Assert.All(body.Topology.Edges, edge => Assert.Equal(2, body.Topology.Coedges.Count(coedge => coedge.EdgeId == edge.Id)));
        Assert.All(body.Topology.Vertices.Skip(4), vertex =>
        {
            Assert.True(body.TryGetVertexPoint(vertex.Id, out var point));
            Assert.True(point.X * double.Cos(0.4) + point.Y * double.Sin(0.4) > 0);
        });
        Assert.InRange(built.Value.Certificate.OuterCurveBoundMm, 0, 1e-6);
        Assert.InRange(built.Value.Certificate.InnerCurveBoundMm, 0, 1e-6);
        Assert.InRange(built.Value.Certificate.OuterHostPcurveBoundMm, 0, 1e-6);
        Assert.InRange(built.Value.Certificate.InnerHostPcurveBoundMm, 0, 1e-6);
        Assert.True(BrepPcurveValidator.Validate(body, requireEveryCoedge: true).IsValid);
        Assert.True(BrepExportPreflight.Validate(body).IsValid);
        var mass = BrepMassProperties.Evaluate(body);
        Assert.True(mass.IsEnclosed, string.Join("; ", mass.Topology.Messages));
        Assert.True(mass.IsOrientationConsistent, string.Join("; ", mass.Topology.Messages));
        Assert.True(mass.SignedVolume > 0);
        var uncutMass = BrepMassProperties.Evaluate(hollow.Body);
        Assert.True(mass.AbsoluteVolume < uncutMass.AbsoluteVolume);
        Assert.True(uncutMass.AbsoluteVolume - mass.AbsoluteVolume > 0,
            $"uncut={uncutMass.AbsoluteVolume:R};cut={mass.AbsoluteVolume:R};method={mass.EvaluationMethod}");
        var tessellationStart = Stopwatch.GetTimestamp();
        var display = BrepDisplayTessellator.TessellateBounded(body, executionTimeout: TimeSpan.FromSeconds(10));
        var tessellationTime = Stopwatch.GetElapsedTime(tessellationStart);
        Assert.True(display.IsSuccess, string.Join("; ", display.Diagnostics.Select(d => d.Message)));
        Assert.Equal(6, display.Value.FacePatches.Count);
        Assert.All(display.Value.FacePatches, patch => Assert.NotEmpty(patch.TriangleIndices));
        var displayOutput = Environment.GetEnvironmentVariable("AETHERIS_HOLLOW_RADIAL_CUT_DISPLAY_OBJ_OUTPUT");
        if (!string.IsNullOrWhiteSpace(displayOutput))
        {
            var lines = new List<string>();
            var offset = 1;
            foreach (var patch in display.Value.FacePatches)
            {
                lines.Add($"g Face{patch.FaceId.Value}");
                lines.AddRange(patch.Positions.Select(p => FormattableString.Invariant($"v {p.X:R} {p.Y:R} {p.Z:R}")));
                for (var k = 0; k < patch.TriangleIndices.Count; k += 3)
                    lines.Add($"f {offset + patch.TriangleIndices[k]} {offset + patch.TriangleIndices[k + 1]} {offset + patch.TriangleIndices[k + 2]}");
                offset += patch.Positions.Count;
            }
            File.WriteAllLines(displayOutput, lines);
        }
        var exportStart = Stopwatch.GetTimestamp();
        var step = Step242Exporter.ExportBody(body);
        var exportTime = Stopwatch.GetElapsedTime(exportStart);
        Assert.True(step.IsSuccess, string.Join("; ", step.Diagnostics.Select(d => d.Message)));
        var debugOutput = Environment.GetEnvironmentVariable("AETHERIS_HOLLOW_RADIAL_CUT_STEP_OUTPUT");
        if (!string.IsNullOrWhiteSpace(debugOutput)) File.WriteAllText(debugOutput, step.Value);
        var importStart = Stopwatch.GetTimestamp();
        var imported = Step242Importer.ImportBody(step.Value);
        var importTime = Stopwatch.GetElapsedTime(importStart);
        Assert.True(imported.IsSuccess, string.Join("; ", imported.Diagnostics.Select(d => d.Message)));
        var reportOutput = Environment.GetEnvironmentVariable("AETHERIS_HOLLOW_RADIAL_CUT_REPORT_OUTPUT");
        if (!string.IsNullOrWhiteSpace(reportOutput))
        {
            var fineOptions = new BrepMassPropertiesOptions(0.005, double.Pi / 48, 1,
                MinimumSegments: 64, MaximumSegments: 256);
            var fineUncut = BrepMassProperties.Evaluate(hollow.Body, fineOptions);
            var fineCut = BrepMassProperties.Evaluate(body, fineOptions);
            File.WriteAllText(reportOutput, JsonSerializer.Serialize(new
            {
                buildMs = buildTime.TotalMilliseconds,
                tessellationMs = tessellationTime.TotalMilliseconds,
                stepExportMs = exportTime.TotalMilliseconds,
                stepImportMs = importTime.TotalMilliseconds,
                stepBytes = System.Text.Encoding.UTF8.GetByteCount(step.Value),
                faceCount = body.Topology.Faces.Count(), edgeCount = body.Topology.Edges.Count(), vertexCount = body.Topology.Vertices.Count(),
                holeCount = 1, certificate = built.Value.Certificate, phaseTimings = built.Value.Timings,
                massSanity = new { uncutVolume = uncutMass.AbsoluteVolume, cutVolume = mass.AbsoluteVolume, method = mass.EvaluationMethod },
                fineMassSanity = new { uncutVolume = fineUncut.AbsoluteVolume, cutVolume = fineCut.AbsoluteVolume,
                    decrease = fineUncut.AbsoluteVolume - fineCut.AbsoluteVolume, uncutError = fineUncut.ErrorBound,
                    cutError = fineCut.ErrorBound, method = fineCut.EvaluationMethod }
            }, new JsonSerializerOptions { WriteIndented = true }));
        }
        var importedMass = BrepMassProperties.Evaluate(imported.Value);
        Assert.True(importedMass.IsEnclosed && importedMass.IsOrientationConsistent);
        Assert.True(importedMass.SignedVolume > 0);
        Assert.Equal(3, imported.Value.Bindings.FaceBoundaryRoleBindings.Count(binding => binding.Role == FaceBoundaryRole.Inner));
        Assert.True(BrepPcurveValidator.Validate(imported.Value, requireEveryCoedge: true).IsValid);
        var radii = imported.Value.Bindings.FaceBindings
            .Select(binding => imported.Value.Geometry.GetSurface(binding.SurfaceGeometryId))
            .Where(surface => surface.Kind == SurfaceGeometryKind.Cylinder)
            .Select(surface => surface.Cylinder!.Value.Radius).OrderBy(radius => radius).ToArray();
        Assert.Equal(new[] { 4.5, 55.2, 56d }, radii);
    }

    [Theory]
    [InlineData(0.8, 4.5, 71, 0)] // authored +X seam, relocated to a clear quarter-turn
    [InlineData(0.8, 4.5, 40, 0.9)]
    [InlineData(0.8, 0.8, 71, 2.1)]
    [InlineData(0.8, 12, 71, 5.4)]
    [InlineData(3, 4.5, 71, 0.4)]
    public void PlacementAndWallVariation_StayLocalAndManifold(double thickness, double holeRadius, double z, double angle)
    {
        var hollow = ThinWalledBodyBRepPlanner.CreateCylinder(56, 142, thickness).Value;
        var built = BrepHollowRadialCut.Build(hollow, holeRadius, z, angle);
        Assert.True(built.IsSuccess, string.Join("; ", built.Diagnostics.Select(d => $"{d.Source}: {d.Message}")));
        Assert.Equal(2, built.Value.Body.Bindings.FaceBoundaryRoleBindings.Count(binding => binding.Role == FaceBoundaryRole.Inner &&
            built.Value.Body.Geometry.GetSurface(built.Value.Body.Bindings.FaceBindings.Single(face => face.FaceId == binding.FaceId).SurfaceGeometryId).Kind == SurfaceGeometryKind.Cylinder));
        var mass = BrepMassProperties.Evaluate(built.Value.Body);
        Assert.True(mass.IsEnclosed && mass.IsOrientationConsistent);
        Assert.True(mass.SignedVolume > 0);
    }

    [Fact]
    public void InvalidPlacementAndExtent_FailWithControlledDiagnostics()
    {
        var hollow = ThinWalledBodyBRepPlanner.CreateCylinder(56, 142, 0.8).Value;
        Assert.Contains(BrepHollowRadialCut.Build(hollow, 4.5, 4, 0).Diagnostics,
            d => d.Source == "Brep.HollowRadialCut.EndCollision");
        Assert.Contains(BrepHollowRadialCut.Build(hollow, 55.2, 71, 0).Diagnostics,
            d => d.Source == "Brep.HollowRadialCut.TangentOrOversize");
        Assert.Contains(BrepHollowRadialCut.Build(hollow, 4.5, 71, 0, insideRadialCoordinate: 56.1).Diagnostics,
            d => d.Source == "Brep.HollowRadialCut.IncompleteCutterExtent");
        Assert.Contains(BrepHollowRadialCut.Build(hollow, 4.5, 71, 0, outsideRadialCoordinate: 56).Diagnostics,
            d => d.Source == "Brep.HollowRadialCut.IncompleteCutterExtent");
        Assert.Contains(BrepHollowRadialCut.Build(hollow, 4.5, 71, 0, insideRadialCoordinate: -56).Diagnostics,
            d => d.Source == "Brep.HollowRadialCut.InvalidCutterExtent");
    }

    [Fact]
    public void PeriodicAngle_UsesOneOpeningAndByteStableStep()
    {
        var hollow = ThinWalledBodyBRepPlanner.CreateCylinder(56, 142, 0.8).Value;
        var seam = BrepHollowRadialCut.Build(hollow, 4.5, 71, 0).Value.Body;
        var wrapped = BrepHollowRadialCut.Build(hollow, 4.5, 71, 2d * double.Pi).Value.Body;
        Assert.Equal(2, seam.Bindings.FaceBoundaryRoleBindings.Count(binding =>
            binding.Role == FaceBoundaryRole.Inner && seam.Geometry.GetSurface(
                seam.Bindings.FaceBindings.Single(face => face.FaceId == binding.FaceId).SurfaceGeometryId).Kind == SurfaceGeometryKind.Cylinder));
        Assert.Equal(Step242Exporter.ExportBody(seam).Value, Step242Exporter.ExportBody(wrapped).Value);
    }
}
