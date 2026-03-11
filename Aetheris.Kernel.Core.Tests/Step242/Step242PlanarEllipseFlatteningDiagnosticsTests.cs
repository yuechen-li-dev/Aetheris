using Aetheris.Kernel.Core.Brep;
using Aetheris.Kernel.Core.Geometry;
using Aetheris.Kernel.Core.Step242;
using Aetheris.Kernel.Core.Topology;

namespace Aetheris.Kernel.Core.Tests.Step242;

public sealed class Step242PlanarEllipseFlatteningDiagnosticsTests
{
    [Fact]
    public void Step242_Stc06_Face18_PlanarEllipseFamily_IsExplicit_AndDeterministic()
    {
        const string relativePath = "testdata/step242/nist/STC/nist_stc_06_asme1_ap242-e3.stp";
        var body = ImportBody(relativePath);

        var faceId = new FaceId(18);
        var surface = body.GetFaceSurface(faceId);
        Assert.Equal(SurfaceGeometryKind.Plane, surface.Kind);

        Assert.True(surface.Plane.HasValue);
        var plane = surface.Plane!.Value;
        var coedges = GetFaceCoedges(body, faceId);
        var ellipseCoedges = coedges
            .Where(c => body.GetEdgeCurve(c.EdgeId).Kind == CurveGeometryKind.Ellipse3)
            .OrderBy(c => c.EdgeId.Value)
            .ToArray();

        Assert.NotEmpty(ellipseCoedges);

        foreach (var coedge in ellipseCoedges)
        {
            var curve = body.GetEdgeCurve(coedge.EdgeId);
            var ellipse = curve.Ellipse3!.Value;
            Assert.True(double.Abs(ellipse.Normal.ToVector().Dot(plane.Normal.ToVector())) > 0.999999d);
            Assert.True(double.Abs((ellipse.Center - plane.Origin).Dot(plane.Normal.ToVector())) <= 1e-7d);

            var binding = body.Bindings.GetEdgeBinding(coedge.EdgeId);
            Assert.True(binding.TrimInterval.HasValue);
            var trim = binding.TrimInterval!.Value;
            Assert.True(double.IsFinite(trim.Start));
            Assert.True(double.IsFinite(trim.End));
            Assert.True(double.Abs(trim.End - trim.Start) > 1e-9d);
        }
    }

    private static BrepBody ImportBody(string relativePath)
    {
        var absolutePath = Path.Combine(Step242CorpusManifestRunner.RepoRoot(), relativePath.Replace('/', Path.DirectorySeparatorChar));
        var text = File.ReadAllText(absolutePath);
        var result = Step242Importer.ImportBody(text);
        Assert.True(result.IsSuccess, result.IsSuccess ? string.Empty : result.Diagnostics[0].Message);
        return result.Value;
    }

    private static IReadOnlyList<Coedge> GetFaceCoedges(BrepBody body, FaceId faceId)
    {
        var coedges = new List<Coedge>();
        foreach (var loopId in body.GetLoopIds(faceId))
        {
            foreach (var coedgeId in body.GetCoedgeIds(loopId))
            {
                coedges.Add(body.Topology.GetCoedge(coedgeId));
            }
        }

        return coedges;
    }
}
