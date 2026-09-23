using Aetheris.Kernel.Firmament.FirmamentV2;
using Aetheris.Kernel.Firmament.Materializer;
using Aetheris.Kernel.Core.Step242;

namespace Aetheris.Kernel.Firmament.Tests;

public sealed class BoxRuntimeCorrespondenceTests
{
    private const string Source = "Model BoxWitness { Units: mm Box Body { Size: [30mm, 20mm, 10mm] } }";

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
        Assert.Contains(correspondence.Descendants, item => item.Kind == "Edge" && item.Edge.HasValue && item.FirmamentSelector is null);
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
