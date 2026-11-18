using Aetheris.Kernel.Firmament.FirmamentV2;
using Aetheris.Kernel.Firmament.Materializer;
using Aetheris.Kernel.Core.Step242;

namespace Aetheris.Kernel.Firmament.Tests;

public sealed class BoxRuntimeCorrespondenceTests
{
    private const string Source = "Model BoxWitness { Units: mm Box Body { Size: [30mm, 20mm, 10mm] } }";

    [Theory]
    [InlineData("Fillet", "Radius")]
    [InlineData("Chamfer", "Distance")]
    public void LocalizedFinish_PreservesPartNoteInStep(string kind, string field)
    {
        var withNote = Source[..^1] + $$"""
            Modify Body { EdgeFinish Round { Face: +X Target: SharedEdgePlusZ Kind: {{kind}} {{field}}: 1mm } }
            Pmi { Note Finish { text: "Deburr the operator's side." } }
            }
            """;
        var build = FirmamentBuildAndExport.CompileSource(withNote);
        Assert.True(build.IsSuccess, string.Join(" | ", build.Diagnostics.Select(item => item.Message)));
        var note = Assert.Single(Step242SemanticPmiInspector.Inspect(build.Value.StepText).Items, item => item.Kind == "Annotation");
        Assert.Equal("Finish", note.Name);
        Assert.Equal("Deburr the operator's side.", note.Text);
    }

    [Fact]
    public void BoxFacesRetainConstructionIdentityForDirectDisplay()
    {
        var build = FirmamentBuildAndExport.CompileSource(Source);
        Assert.True(build.IsSuccess, string.Join("; ", build.Diagnostics.Select(item => item.Message)));
        var body = Assert.IsType<Aetheris.Kernel.Core.Brep.BrepBody>(build.Value.RuntimeBody);
        var correspondence = Assert.IsType<SemanticTopologyCorrespondence>(build.Value.RuntimeCorrespondence);
        Assert.Equal("Body", correspondence.BodyStableId);
        var sourceMap = new GeometrySourceMap(correspondence);
        Assert.True(sourceMap.TryGetSourceSpan("Body", out var boxSpan));
        Assert.Contains("Box Body", Source.Substring(boxSpan.Start, boxSpan.Length));
        var faces = correspondence.Descendants.Where(item => item.Kind == "Face").ToArray();
        Assert.Equal(6, faces.Length);
        Assert.Equal(6, faces.Select(item => item.Face).Distinct().Count());
        Assert.All(faces, item =>
        {
            Assert.Equal(SemanticTopologyAddressability.DerivedStable, item.Addressability);
            Assert.Contains(body.Topology.Faces, face => face.Id == item.Face);
        });
        Assert.Equal("Body.face(+Z)", Assert.Single(faces, item => item.FirmamentSelector == "face(+Z)").StableId);
        var top = Assert.Single(faces, item => item.FirmamentSelector == "face(+Z)");
        Assert.True(sourceMap.TryGetByBrepFace(top.Face!.Value, out var mappedTop));
        Assert.Equal(top, mappedTop);
        Assert.Contains(top, sourceMap.GetEntitiesForSemanticKey(top.StableId));
        Assert.Equal(6, sourceMap.GetEntitiesForSourceSymbol("Body").Count(item => item.Face.HasValue));
        Assert.Equal("Body.face(+X)", Assert.Single(faces, item => item.FirmamentSelector == "face(+X)").StableId);
        foreach (var face in faces)
        {
            var binding = Assert.Single(body.Bindings.FaceBindings, item => item.FaceId == face.Face);
            var normal = body.Geometry.GetSurface(binding.SurfaceGeometryId).Plane!.Value.Normal;
            var axis = face.FirmamentSelector![6];
            var expected = face.FirmamentSelector[5] == '+' ? 1d : -1d;
            var component = axis switch { 'X' => normal.X, 'Y' => normal.Y, 'Z' => normal.Z, _ => throw new InvalidOperationException() };
            Assert.Equal(expected, component, 10);
        }
        var front = Assert.Single(correspondence.Descendants, item => item.StableId == "Body.edge(TopFront)");
        Assert.Equal("SharedEdge(-Y,+Z)", front.FirmamentSelector);
        Assert.True(front.Edge.HasValue);
    }

    [Fact]
    public void BoxSizeEditPreservesSemanticFaceIdentityButBodyRenameInvalidatesIt()
    {
        var initial = FirmamentBuildAndExport.CompileSource(Source).Value.RuntimeCorrespondence!;
        var resized = FirmamentBuildAndExport.CompileSource(Source.Replace("10mm]", "40mm]", StringComparison.Ordinal)).Value.RuntimeCorrespondence!;
        var renamed = FirmamentBuildAndExport.CompileSource(Source.Replace("Box Body", "Box Renamed", StringComparison.Ordinal)).Value.RuntimeCorrespondence!;
        Assert.Equal(initial.Descendants.Where(item => item.Face.HasValue).Select(item => item.StableId),
            resized.Descendants.Where(item => item.Face.HasValue).Select(item => item.StableId));
        Assert.DoesNotContain(renamed.Descendants, item => item.StableId == "Body.face(+Z)");
    }

    [Theory]
    [InlineData("Blind(4mm)")]
    [InlineData("ThroughAll")]
    public void PickedTopHoleFrameUsesAuthoredCoordinatesAndRetainsExistingOpening(string end)
    {
        var source = Source.Replace(" } }", " } Modify Body { Hole<Shaft> bore { on: face(+Z); center: Point2(8mm, 5mm); diameter: 4mm; end: " + end + " } } }", StringComparison.Ordinal);
        var build = FirmamentBuildAndExport.CompileSource(source);
        Assert.True(build.IsSuccess, string.Join("; ", build.Diagnostics.Select(item => item.Message)));
        var body = build.Value.RuntimeBody!;
        var descendants = build.Value.RuntimeCorrespondence!.Descendants;
        var top = Assert.Single(descendants, face => face.FirmamentSelector == "face(+Z)");
        var frame = Assert.IsType<PlanarFaceSupport>(PlanarFaceThroughHole.SupportForSelection(body, top));
        Assert.Equal([0d, 0d, 10d], frame.Origin);
        Assert.Equal([1d, 0d, 0d], frame.X);
        Assert.Equal([0d, 1d, 0d], frame.Y);
        var opening = Assert.Single(frame.ExcludedDisks);
        Assert.Equal([8d, 5d], opening.Center);
        Assert.Equal(2d, opening.Radius);
        Assert.All(descendants.Where(face => face.Role == SemanticTopologyRole.BoxFace && face != top),
            face => Assert.Null(PlanarFaceThroughHole.SupportForSelection(body, face)));
    }

    [Fact]
    public void MultipleBoxHolesRefuseDrillPointsAndPreserveExplicitFlatBottoms()
    {
        const string blind = "Hole<Shaft> BlindHole { on: face(+Z); center: Point2(8mm, 5mm); diameter: 4mm; end: Blind(4mm) }";
        const string through = "Hole<Shaft> ThroughHole { on: face(+Z); center: Point2(-8mm, -5mm); diameter: 4mm; end: ThroughAll }";
        var singleSource = Source.Replace(" } }", " } Modify Body { " + blind + " } }", StringComparison.Ordinal);
        var single = FirmamentBuildAndExport.CompileSource(singleSource);
        Assert.True(single.IsSuccess, string.Join("; ", single.Diagnostics.Select(d => d.Message)));
        Assert.Contains("CONICAL_SURFACE(", single.Value.StepText);

        var combinedSource = singleSource.Replace(blind, blind + " " + through, StringComparison.Ordinal);
        var blocked = FirmamentBuildAndExport.CompileSource(combinedSource);
        Assert.False(blocked.IsSuccess);
        var diagnostic = Assert.Single(blocked.Diagnostics, d => d.Message.StartsWith("firmament-v2-hole-drill-point-composition:", StringComparison.Ordinal));
        Assert.Contains("Body.BlindHole", diagnostic.Message);
        Assert.Contains("Through all", diagnostic.Message);

        var flatSource = combinedSource.Replace("end: Blind(4mm)", "end: Blind(4mm); Termination: Flat", StringComparison.Ordinal);
        var flat = FirmamentBuildAndExport.CompileSource(flatSource);
        Assert.True(flat.IsSuccess, string.Join("; ", flat.Diagnostics.Select(d => d.Message)));
        Assert.Equal(2, flat.Value.MaterializedFeatures!.Count);
        Assert.DoesNotContain("CONICAL_SURFACE(", flat.Value.StepText);
        Assert.True(Step242Importer.ImportBody(flat.Value.StepText).IsSuccess);
    }

    [Fact]
    public void FormattedTopFaceIsAcceptedByExistingFirmamentPmiBinder()
    {
        var source = Source.Replace(" } }", " } Pmi { Datum A { Target: face(+Z) } } }", StringComparison.Ordinal);
        var parsed = FirmamentV2Parser.Parse(source);
        Assert.True(parsed.IsSuccess, string.Join("; ", parsed.Diagnostics));
        Assert.Contains(parsed.Document!.Pmi!, datum => datum.Target == "face(+Z)");
        var build = FirmamentBuildAndExport.CompileSource(source);
        Assert.True(build.IsSuccess, string.Join("; ", build.Diagnostics.Select(item => item.Message)));
        Assert.NotNull(build.Value.RuntimeCorrespondence);
    }

    [Fact]
    public void DirectDisplayDoesNotChangeStepExportOrReimport()
    {
        var build = FirmamentBuildAndExport.CompileSource(Source);
        Assert.True(build.IsSuccess, string.Join("; ", build.Diagnostics.Select(item => item.Message)));
        var reimport = Step242Importer.ImportBody(build.Value.StepText);
        Assert.True(reimport.IsSuccess, string.Join("; ", reimport.Diagnostics.Select(item => item.Message)));
        Assert.Equal(6, reimport.Value!.Topology.Faces.Count());
    }

    [Fact]
    public void SourceMapAllowsSplitRoleButRejectsDuplicateBrepEntity()
    {
        var correspondence = FirmamentBuildAndExport.CompileSource(Source).Value.RuntimeCorrespondence!;
        var faces = correspondence.Descendants.Where(item => item.Face.HasValue).ToArray();
        var split = correspondence with { Descendants = correspondence.Descendants.Select(item =>
            item == faces[1] ? item with { StableId = faces[0].StableId } : item).ToArray() };
        var map = new GeometrySourceMap(split);
        Assert.Equal(2, map.GetEntitiesForSemanticKey(faces[0].StableId).Count);
        Assert.Throws<InvalidOperationException>(() => new GeometrySourceMap(correspondence with
        {
            Descendants = [.. correspondence.Descendants, faces[0]]
        }));
        Assert.Throws<InvalidOperationException>(() => new GeometrySourceMap(correspondence with
        {
            Descendants = correspondence.Descendants.Select(item => item == faces[1]
                ? item with { StableId = faces[0].StableId, SourceStableId = "another-origin" } : item).ToArray()
        }));
    }
}
