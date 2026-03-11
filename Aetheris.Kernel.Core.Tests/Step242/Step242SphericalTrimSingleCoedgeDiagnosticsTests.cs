using Aetheris.Kernel.Core.Brep;
using Aetheris.Kernel.Core.Geometry;
using Aetheris.Kernel.Core.Step242;
using Aetheris.Kernel.Core.Topology;

namespace Aetheris.Kernel.Core.Tests.Step242;

public sealed class Step242SphericalTrimSingleCoedgeDiagnosticsTests
{
    [Fact]
    public void Step242_Stc06_Face33_IsSingleCoedgeClosedFullCircleLatitudeLoop_OnSphere()
    {
        const string relativePath = "testdata/step242/nist/STC/nist_stc_06_asme1_ap242-e3.stp";
        var fullPath = Path.Combine(Step242CorpusManifestRunner.RepoRoot(), relativePath.Replace('/', Path.DirectorySeparatorChar));

        var import = Step242Importer.ImportBody(File.ReadAllText(fullPath));
        Assert.True(import.IsSuccess);

        var body = import.Value;
        var faceId = new FaceId(33);
        var surface = body.GetFaceSurface(faceId);
        Assert.Equal(SurfaceGeometryKind.Sphere, surface.Kind);

        var loopId = Assert.Single(body.GetLoopIds(faceId));
        var coedgeId = Assert.Single(body.GetCoedgeIds(loopId));
        var coedge = body.Topology.GetCoedge(coedgeId);
        var curve = body.GetEdgeCurve(coedge.EdgeId);
        Assert.Equal(CurveGeometryKind.Circle3, curve.Kind);

        Assert.True(body.TryGetEdgeVertices(coedge.EdgeId, out var startVertexId, out var endVertexId));
        Assert.Equal(startVertexId, endVertexId);

        var trim = body.Bindings.GetEdgeBinding(coedge.EdgeId).TrimInterval;
        Assert.NotNull(trim);
        Assert.Equal(0d, trim!.Value.Start, 6);
        Assert.Equal(2d * double.Pi, trim.Value.End, 6);
    }

    [Fact]
    public void Step242_Stc06_AdvancesPastSingleCoedgeSphereTrim_Deterministically()
    {
        const string relativePath = "testdata/step242/nist/STC/nist_stc_06_asme1_ap242-e3.stp";
        var entry = new Step242CorpusManifestEntry(
            Id: Path.GetFileNameWithoutExtension(relativePath),
            Path: relativePath,
            Group: "nist-sphere-single-coedge-regression",
            Notes: "single-coedge spherical trim family progression",
            ExpectedFirstDiagnostic: null,
            ExpectHashStableAfterCanonicalization: null,
            ExpectTopologyCounts: null,
            ExpectGeometryKinds: null);

        var first = Step242CorpusManifestRunner.RunOne(entry);
        var second = Step242CorpusManifestRunner.RunOne(entry);

        Assert.Equal("tessellator", first.FirstFailureLayer);
        Assert.Equal("Viewer.Tessellation.CylinderTrimDegenerate", first.FirstDiagnostic.Source);
        Assert.StartsWith("Cylindrical face tessellation derived a degenerate trim patch.", first.FirstDiagnostic.MessagePrefix, StringComparison.Ordinal);

        Assert.Equal(first.FirstFailureLayer, second.FirstFailureLayer);
        Assert.Equal(first.FirstDiagnostic.Source, second.FirstDiagnostic.Source);
        Assert.Equal(first.FirstDiagnostic.MessagePrefix, second.FirstDiagnostic.MessagePrefix);
    }
}
