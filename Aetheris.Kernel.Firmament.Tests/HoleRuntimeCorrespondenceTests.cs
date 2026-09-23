using Aetheris.Kernel.Core.Step242;
using Aetheris.Kernel.Core.Brep.Tessellation;
using Aetheris.Kernel.Firmament.Materializer;

namespace Aetheris.Kernel.Firmament.Tests;

public sealed class HoleRuntimeCorrespondenceTests
{
    private static string Source(double diameter, double x = 0) => $"Model HoleWitness {{ Units: mm Box Body {{ Size: [40mm, 30mm, 8mm] }} Modify Body {{ Hole<Shaft> H {{ On: +Z Center: Point2({x}mm, 0mm) Diameter: {diameter}mm End: ThroughAll }} }} }}";
    private static string WithWallDiameter(string source, string holeName = "H") => source[..^1] + $" Pmi {{ HoleDiameter WallDiameter {{ Target: face({holeName}.Wall) Value: 6mm }} }} }}";

    [Fact]
    public void SimpleThroughHoleCarriesWallAndRimsFromConstructionToDirectDisplay()
    {
        var build = FirmamentBuildAndExport.CompileSource(Source(6));
        Assert.True(build.IsSuccess, string.Join("; ", build.Diagnostics.Select(d => d.Message)));
        var body = Assert.IsType<Aetheris.Kernel.Core.Brep.BrepBody>(build.Value.RuntimeBody);
        var correspondence = Assert.IsType<SemanticTopologyCorrespondence>(build.Value.RuntimeCorrespondence);
        var wall = Assert.Single(correspondence.Descendants, d => d.Role == SemanticTopologyRole.HoleWallFace);
        var sourceMap = new GeometrySourceMap(correspondence);
        Assert.True(sourceMap.TryGetSourceSpan("Body.H", out var holeSpan));
        Assert.Contains("Hole<Shaft> H", Source(6).Substring(holeSpan.Start, holeSpan.Length));
        Assert.True(sourceMap.TryGetByBrepFace(wall.Face!.Value, out var mappedWall));
        Assert.Equal(wall, mappedWall);
        Assert.Contains(wall, sourceMap.GetEntitiesForSourceSymbol("Body.H"));
        Assert.Equal("hole:Body.H", wall.SourceStableId);
        Assert.Contains(body.Topology.Faces, face => face.Id == wall.Face);
        Assert.Contains(correspondence.Descendants, d => d.Role == SemanticTopologyRole.HoleEntryLoop);
        Assert.Contains(correspondence.Descendants, d => d.Role == SemanticTopologyRole.HoleExitLoop);
        Assert.Equal(wall.StableId, Assert.Single(FirmamentBuildAndExport.CompileSource(Source(8)).Value.RuntimeCorrespondence!.Descendants,
            d => d.Role == SemanticTopologyRole.HoleWallFace).StableId);
        Assert.True(Step242Importer.ImportBody(build.Value.StepText).IsSuccess);
    }

    [Fact]
    public void BoxTopTessellationCoversTheFullRectangle()
    {
        var build = FirmamentBuildAndExport.CompileSource("Model BoxWitness { Units: mm Box Body { Size: [40mm, 30mm, 8mm] } }");
        Assert.True(build.IsSuccess);
        var body = build.Value.RuntimeBody!;
        var top = Assert.Single(build.Value.RuntimeCorrespondence!.Descendants, d => d.FirmamentSelector == "face(+Z)");
        var mesh = BrepDisplayTessellator.Tessellate(body);
        Assert.True(mesh.IsSuccess, string.Join("; ", mesh.Diagnostics.Select(d => d.Message)));
        var patch = Assert.Single(mesh.Value.FacePatches, p => p.FaceId == top.Face);
        Assert.True(patch.TriangleIndices.Count >= 6, $"top triangles={patch.TriangleIndices.Count / 3}, positions={string.Join(';', patch.Positions)}");
        var area = 0d;
        for (var i = 0; i < patch.TriangleIndices.Count; i += 3)
        {
            var a = patch.Positions[patch.TriangleIndices[i]];
            var b = patch.Positions[patch.TriangleIndices[i + 1]];
            var c = patch.Positions[patch.TriangleIndices[i + 2]];
            area += Math.Abs((b.X - a.X) * (c.Y - a.Y) - (b.Y - a.Y) * (c.X - a.X)) / 2;
        }
        Assert.True(Math.Abs(area - 1200) < 1e-6, $"top area={area}, triangles={string.Join(',', patch.TriangleIndices)}, positions={string.Join(';', patch.Positions)}, vertices={string.Join(';', body.Topology.Vertices.Select(v => body.TryGetVertexPoint(v.Id, out var p) ? p.ToString() : "missing"))}");
    }

    [Fact]
    public void ExistingAdvancedHoleSelectionDeclarationIsNotAcceptedInSimpleHoleSource()
    {
        var source = Source(6);
        source = source[..^1] + " Selection WallH { Target: HoleWall Source: Hole(H) Require: NonEmptyFaceSet } }";
        var build = FirmamentBuildAndExport.CompileSource(source);
        Assert.False(build.IsSuccess);
        Assert.Contains(build.Diagnostics, d => d.Message.Contains("firmament-v2-phase3-edge-finish-syntax-invalid", StringComparison.Ordinal));
    }

    [Fact]
    public void HoleWallSelectorParsesBindsAndResolvesToConstructionWall()
    {
        var source = WithWallDiameter(Source(6));
        var parsed = FirmamentV2.FirmamentV2Parser.Parse(source);
        Assert.True(parsed.IsSuccess, string.Join("; ", parsed.Diagnostics));
        var selector = Assert.IsType<FirmamentV2.FirmamentV2HoleWallSelector>(Assert.Single(parsed.Document!.BoundPmi!.Dimensions).HoleWallTarget);
        Assert.Equal("H", selector.HoleName);
        Assert.Equal("Body", selector.BodyName);
        var build = FirmamentBuildAndExport.CompileSource(source);
        Assert.True(build.IsSuccess, string.Join("; ", build.Diagnostics.Select(d => d.Message)));
        var wall = Assert.Single(build.Value.RuntimeCorrespondence!.Descendants, d => d.Role == SemanticTopologyRole.HoleWallFace);
        Assert.Equal("face(H.Wall)", wall.FirmamentSelector);
        Assert.Equal("hole:Body.H", wall.SourceStableId);
        Assert.True(FirmamentV2.FirmamentV2HoleWallSelector.TryParse(wall.FirmamentSelector!, out var parsedHole));
        Assert.Equal(selector.HoleName, parsedHole);
        Assert.True(Step242Importer.ImportBody(build.Value.StepText).IsSuccess);
    }

    [Fact]
    public void HoleWallSelectorSurvivesDiameterAndPositionEditsAndRenameChangesSpelling()
    {
        var initial = Assert.Single(FirmamentBuildAndExport.CompileSource(WithWallDiameter(Source(6))).Value.RuntimeCorrespondence!.Descendants,
            d => d.Role == SemanticTopologyRole.HoleWallFace);
        var edited = FirmamentBuildAndExport.CompileSource(WithWallDiameter(Source(7, 3)));
        Assert.True(edited.IsSuccess, string.Join("; ", edited.Diagnostics.Select(d => d.Message)));
        var editedWall = Assert.Single(edited.Value.RuntimeCorrespondence!.Descendants, d => d.Role == SemanticTopologyRole.HoleWallFace);
        Assert.Equal(initial.StableId, editedWall.StableId);
        Assert.Equal(initial.FirmamentSelector, editedWall.FirmamentSelector);
        var renamed = WithWallDiameter(Source(6).Replace(" H {", " MountHole {", StringComparison.Ordinal), "MountHole");
        var renamedBuild = FirmamentBuildAndExport.CompileSource(renamed);
        Assert.True(renamedBuild.IsSuccess, string.Join("; ", renamedBuild.Diagnostics.Select(d => d.Message)));
        Assert.Equal("face(MountHole.Wall)", Assert.Single(renamedBuild.Value.RuntimeCorrespondence!.Descendants,
            d => d.Role == SemanticTopologyRole.HoleWallFace).FirmamentSelector);
    }

    [Fact]
    public void DeletedAndNonHoleTargetsGetSpecificDiagnostics()
    {
        var deleted = FirmamentBuildAndExport.CompileSource(WithWallDiameter("Model HoleWitness { Units: mm Box Body { Size: [40mm, 30mm, 8mm] } }"));
        Assert.False(deleted.IsSuccess);
        Assert.Contains(deleted.Diagnostics, d => d.Message.Contains("firmament-v2-hole-wall-unknown-hole:H", StringComparison.Ordinal));
        var nonHole = FirmamentBuildAndExport.CompileSource(WithWallDiameter(Source(6), "Body"));
        Assert.False(nonHole.IsSuccess);
        Assert.Contains(nonHole.Diagnostics, d => d.Message.Contains("firmament-v2-hole-wall-target-not-hole:Body", StringComparison.Ordinal));
        var blind = FirmamentBuildAndExport.CompileSource(WithWallDiameter(Source(6).Replace("End: ThroughAll", "End: Blind 4mm", StringComparison.Ordinal)));
        Assert.False(blind.IsSuccess);
        Assert.Contains(blind.Diagnostics, d => d.Message.Contains("firmament-v2-hole-wall-role-unavailable:H", StringComparison.Ordinal));
        var invalidDatum = FirmamentBuildAndExport.CompileSource(Source(6)[..^1]
            + " Pmi { Datum CylindricalWall { Target: face(H.Wall) } } }");
        Assert.False(invalidDatum.IsSuccess);
        Assert.Contains(invalidDatum.Diagnostics, d => d.Message.Contains("firmament-v2-hole-wall-consumer-invalid:CylindricalWall", StringComparison.Ordinal));
    }

    [Fact]
    public void TwoNamedHolesBindDistinctWallRoles()
    {
        var source = "Model TwinHoles { Units: mm Box Body { Size: [40mm, 30mm, 8mm] } "
            + "Modify Body { Hole<Shaft> H1 { On: +Z Center: Point2(-10mm, 0mm) Diameter: 6mm End: ThroughAll } "
            + "Hole<Shaft> H2 { On: +Z Center: Point2(10mm, 0mm) Diameter: 6mm End: ThroughAll } } "
            + "Pmi { HoleDiameter D1 { Target: face(H1.Wall) Value: 6mm } HoleDiameter D2 { Target: face(H2.Wall) Value: 6mm } } }";
        var build = FirmamentBuildAndExport.CompileSource(source);
        Assert.True(build.IsSuccess, string.Join("; ", build.Diagnostics.Select(d => d.Message)));
        var correspondence = Assert.IsType<SemanticTopologyCorrespondence>(build.Value.RuntimeCorrespondence);
        var walls = correspondence.Descendants.Where(d => d.Role == SemanticTopologyRole.HoleWallFace).ToArray();
        Assert.Equal(2, walls.Length);
        Assert.Equal(2, walls.Select(d => d.Face).Distinct().Count());
        Assert.Equal(new[] { "face(H1.Wall)", "face(H2.Wall)" }, walls.Select(d => d.FirmamentSelector));
        var map = new GeometrySourceMap(correspondence);
        foreach (var wall in walls)
        {
            Assert.True(map.TryGetByBrepFace(wall.Face!.Value, out var mapped));
            Assert.Equal(wall, mapped);
            Assert.True(map.TryGetSourceSpan(wall.ParentStableId!, out _));
        }
        Assert.True(Step242Importer.ImportBody(build.Value.StepText).IsSuccess);
    }
}
