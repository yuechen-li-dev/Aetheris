using Aetheris.Kernel.Core.Brep;
using Aetheris.Kernel.Core.Brep.Features;
using Aetheris.Kernel.Core.Brep.Tessellation;
using Aetheris.Kernel.Core.Brep.Verification;
using Aetheris.Kernel.Core.Step242;

namespace Aetheris.Kernel.Core.Tests.Brep;

public sealed class BrepHollowRadialCutPatternTests
{
    [Fact]
    public void CoupledBottomBlend_PreservesThicknessAndPerforationRoundTrip()
    {
        const double blend = 8;
        const double thickness = 0.8;
        var hollow = ThinWalledBodyBRepPlanner.CreateCylinder(56, 142, thickness, blend);
        Assert.True(hollow.IsSuccess, string.Join(" | ", hollow.Diagnostics.Select(d => d.Message)));
        Assert.Equal(7, hollow.Value.Body.Topology.Faces.Count());
        var result = BrepHollowRadialCutPattern.Build(hollow.Value,
            [new("Vents.Row0.Col0", 4.5, 30, 0), new("Vents.Row0.Col1", 4.5, 30, 0.4)]);
        Assert.True(result.IsSuccess, string.Join(" | ", result.Diagnostics.Select(d => $"{d.Source}: {d.Message}")));
        var body = result.Value.Body;
        Assert.Equal(9, body.Topology.Faces.Count());
        var tori = body.Geometry.Surfaces.Where(s => s.Value.Kind == Aetheris.Kernel.Core.Geometry.SurfaceGeometryKind.Torus)
            .Select(s => s.Value.Torus!.Value).ToArray();
        Assert.Equal(2, tori.Length);
        Assert.All(tori, torus => Assert.Equal(48, torus.MajorRadius, 8));
        Assert.Equal(thickness, tori.Max(t => t.MinorRadius) - tori.Min(t => t.MinorRadius), 8);
        var mass = BrepMassProperties.Evaluate(body);
        Assert.True(mass.IsEnclosed && mass.IsOrientationConsistent && mass.SignedVolume > 0);
        var step = Step242Exporter.ExportBody(body);
        Assert.True(step.IsSuccess, string.Join(" | ", step.Diagnostics.Select(d => d.Message)));
        var imported = Step242Importer.ImportBody(step.Value);
        Assert.True(imported.IsSuccess, string.Join(" | ", imported.Diagnostics.Select(d => d.Message)));
        Assert.Equal(9, imported.Value.Topology.Faces.Count());
        var importedTori = imported.Value.Geometry.Surfaces.Where(s => s.Value.Kind == Aetheris.Kernel.Core.Geometry.SurfaceGeometryKind.Torus)
            .Select(s => s.Value.Torus!.Value).ToArray();
        Assert.Equal(2, importedTori.Length);
        Assert.All(importedTori, torus => Assert.Equal(48, torus.MajorRadius, 6));
        Assert.Equal(thickness, importedTori.Max(t => t.MinorRadius) - importedTori.Min(t => t.MinorRadius), 6);
        var importedMass = BrepMassProperties.Evaluate(imported.Value);
        Assert.True(importedMass.IsEnclosed && importedMass.IsOrientationConsistent && importedMass.SignedVolume > 0);
    }

    [Theory]
    [InlineData(0.8)]
    [InlineData(56)]
    [InlineData(142)]
    public void CoupledBottomBlend_RejectsCollapsedProfile(double blendRadius)
    {
        var hollow = ThinWalledBodyBRepPlanner.CreateCylinder(56, 142, 0.8, blendRadius);
        Assert.False(hollow.IsSuccess);
        Assert.Contains(hollow.Diagnostics, d => d.Message == "CylinderBottomBlendInvalid");
    }

    [Fact]
    public void CoupledBottomBlend_RejectsHoleAtJunction()
    {
        var hollow = ThinWalledBodyBRepPlanner.CreateCylinder(56, 142, 0.8, 8).Value;
        var cut = BrepHollowRadialCutPattern.Build(hollow, [new("Vents.Row0.Col0", 4.5, 12, 0)]);
        Assert.False(cut.IsSuccess);
        Assert.Contains(cut.Diagnostics, d => d.Source == "Brep.HollowRadialCutPattern.InvalidPlacement");
    }

    [Fact]
    public void TwoLocalHoles_ShareOneTrimmedHostAndRoundTrip()
    {
        var hollow = ThinWalledBodyBRepPlanner.CreateCylinder(56, 142, 0.8).Value;
        var result = BrepHollowRadialCutPattern.Build(hollow,
            [new("Vents.Row0.Col0", 4.5, 40, 0), new("Vents.Row0.Col1", 4.5, 40, 0.4)]);
        Assert.True(result.IsSuccess, string.Join(" | ", result.Diagnostics.Select(d => $"{d.Source}: {d.Message}")));
        var body = result.Value.Body;
        Assert.Equal(7, body.Topology.Faces.Count());
        Assert.Equal(4, result.Value.TopologyMap.Loops.Count);
        var mass = BrepMassProperties.Evaluate(body);
        Assert.True(mass.IsEnclosed && mass.IsOrientationConsistent && mass.SignedVolume > 0);
        var step = Step242Exporter.ExportBody(body);
        Assert.True(step.IsSuccess, string.Join(" | ", step.Diagnostics.Select(d => d.Message)));
        var imported = Step242Importer.ImportBody(step.Value);
        Assert.True(imported.IsSuccess, string.Join(" | ", imported.Diagnostics.Select(d => d.Message)));
        var importedMass = BrepMassProperties.Evaluate(imported.Value);
        Assert.True(importedMass.IsEnclosed && importedMass.IsOrientationConsistent && importedMass.SignedVolume > 0);
        var wrapped = BrepHollowRadialCutPattern.Build(hollow,
            [new("Vents.Row0.Col0", 4.5, 40, 2 * System.Math.PI), new("Vents.Row0.Col1", 4.5, 40, 0.4)]);
        Assert.True(wrapped.IsSuccess);
        Assert.Equal(step.Value, Step242Exporter.ExportBody(wrapped.Value.Body).Value);
        var display = BrepDisplayTessellator.TessellateBounded(body, executionTimeout: TimeSpan.FromSeconds(10));
        Assert.True(display.IsSuccess, string.Join(" | ", display.Diagnostics.Select(d => d.Message)));
        Assert.Equal(7, display.Value.FacePatches.Count);
    }

    [Fact]
    public void TwentyFourOpenings_AreLocalAndDisplayTessellates()
    {
        var hollow = ThinWalledBodyBRepPlanner.CreateCylinder(24, 60, 0.8).Value;
        var holes = (from row in Enumerable.Range(0, 3)
                     from column in Enumerable.Range(0, 8)
                     select new HollowRadialCutPlacement($"Vents.Row{row}.Col{column}", 2.5,
                         16 + 14 * row, column * 2 * System.Math.PI / 8)).ToArray();
        var result = BrepHollowRadialCutPattern.Build(hollow, holes);
        Assert.True(result.IsSuccess, string.Join(" | ", result.Diagnostics.Select(d => $"{d.Source}: {d.Message}")));
        var body = result.Value.Body;
        Assert.Equal(29, body.Topology.Faces.Count());
        Assert.Equal(48, result.Value.TopologyMap.Loops.Count);
        Assert.All(holes, placement =>
        {
            Assert.Contains($"{placement.StableId}.Wall", result.Value.TopologyMap.Faces.Keys);
            Assert.Contains($"{placement.StableId}.OuterOpening", result.Value.TopologyMap.Loops.Keys);
            Assert.Contains($"{placement.StableId}.InnerOpening", result.Value.TopologyMap.Loops.Keys);
            foreach (var opening in new[] { "OuterOpening", "InnerOpening" })
                foreach (var edgeId in result.Value.TopologyMap.Edges[$"{placement.StableId}.{opening}"])
                {
                    var edge = body.Topology.GetEdge(edgeId);
                    foreach (var vertexId in new[] { edge.StartVertexId, edge.EndVertexId })
                    {
                        Assert.True(body.TryGetVertexPoint(vertexId, out var point));
                        var radial = point.X * System.Math.Cos(placement.AngleRadians) +
                                     point.Y * System.Math.Sin(placement.AngleRadians);
                        Assert.True(radial > 0, $"{placement.StableId} reached the opposite wall");
                    }
                }
        });
        Assert.All(body.Topology.Edges, edge => Assert.Equal(2, body.Topology.Coedges.Count(coedge => coedge.EdgeId == edge.Id)));
        var mass = BrepMassProperties.Evaluate(body);
        Assert.True(mass.IsEnclosed && mass.IsOrientationConsistent && mass.SignedVolume > 0);
        var display = BrepDisplayTessellator.TessellateBounded(body, executionTimeout: TimeSpan.FromSeconds(30));
        Assert.True(display.IsSuccess, string.Join(" | ", display.Diagnostics.Select(d => d.Message)));
        Assert.Equal(29, display.Value.FacePatches.Count);
        var output = Environment.GetEnvironmentVariable("AETHERIS_CYLINDRICAL_PERFORATION_OBJ_OUTPUT");
        if (!string.IsNullOrWhiteSpace(output))
        {
            var lines = new List<string>(); var offset = 1;
            foreach (var patch in display.Value.FacePatches)
            {
                lines.Add($"g Face{patch.FaceId.Value}");
                lines.AddRange(patch.Positions.Select(p => FormattableString.Invariant($"v {p.X:R} {p.Y:R} {p.Z:R}")));
                for (var k = 0; k < patch.TriangleIndices.Count; k += 3)
                    lines.Add($"f {offset + patch.TriangleIndices[k]} {offset + patch.TriangleIndices[k + 1]} {offset + patch.TriangleIndices[k + 2]}");
                offset += patch.Positions.Count;
            }
            File.WriteAllLines(output, lines);
        }
    }
}
