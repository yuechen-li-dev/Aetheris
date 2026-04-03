using Aetheris.Kernel.Core.Brep;
using Aetheris.Kernel.Core.Brep.Tessellation;
using Aetheris.Kernel.Core.Geometry;
using Aetheris.Kernel.Core.Geometry.Surfaces;
using Aetheris.Kernel.Core.Step242;
using Aetheris.Kernel.Core.Topology;

namespace Aetheris.Kernel.Core.Tests.Step242;

public sealed class Step242CylinderTrimDualSingleCoedgeClosedCircleRegressionTests
{
    [Theory]
    [InlineData("testdata/step242/nist/CTC/nist_ctc_05_asme1_ap242-e1.stp")]
    [InlineData("testdata/step242/nist/FTC/nist_ftc_11_asme1_ap242-e2.stp")]
    public void Step242_Targets_ContainDualSingleCoedgeClosedCircleCylinderFamily(string relativePath)
    {
        var fullPath = Path.Combine(Step242CorpusManifestRunner.RepoRoot(), relativePath.Replace('/', Path.DirectorySeparatorChar));
        var import = Step242Importer.ImportBody(File.ReadAllText(fullPath));
        Assert.True(import.IsSuccess);

        var body = import.Value;
        var matches = body.Topology.Faces
            .Select(face => (FaceId: face.Id, Surface: body.GetFaceSurface(face.Id)))
            .Where(pair => pair.Surface.Kind == SurfaceGeometryKind.Cylinder)
            .Where(pair => IsDualSingleCoedgeClosedCircleCylinderFamily(body, pair.FaceId, pair.Surface.Cylinder!.Value, out _))
            .ToArray();

        Assert.NotEmpty(matches);
    }

    [Fact]
    public void Step242_Ctc05_AdvancesThroughDualSingleCoedgeClosedCircleFamily_Deterministically()
    {
        const string relativePath = "testdata/step242/nist/CTC/nist_ctc_05_asme1_ap242-e1.stp";
        var first = Step242CorpusManifestRunner.RunOne(new Step242CorpusManifestEntry("ctc05", relativePath, "nist-regression", null, null, null, null, null));
        var second = Step242CorpusManifestRunner.RunOne(new Step242CorpusManifestEntry("ctc05", relativePath, "nist-regression", null, null, null, null, null));

        Assert.Equal("success", first.Status);
        Assert.Equal(string.Empty, first.FirstFailureLayer);
        Assert.Equal(first.Status, second.Status);
        Assert.Equal(first.FirstFailureLayer, second.FirstFailureLayer);
        Assert.Equal(first.FirstDiagnostic.Source, second.FirstDiagnostic.Source);
        Assert.Equal(first.FirstDiagnostic.MessagePrefix, second.FirstDiagnostic.MessagePrefix);
    }

    [Fact]
    public void Step242_Ftc11_AdvancesPastCylinderTrimDegenerate_ToNextExplicitBlocker_Deterministically()
    {
        const string relativePath = "testdata/step242/nist/FTC/nist_ftc_11_asme1_ap242-e2.stp";
        var first = Step242CorpusManifestRunner.RunOne(new Step242CorpusManifestEntry("ftc11", relativePath, "nist-regression", null, null, null, null, null));
        var second = Step242CorpusManifestRunner.RunOne(new Step242CorpusManifestEntry("ftc11", relativePath, "nist-regression", null, null, null, null, null));

        Assert.Equal("success", first.Status);
        Assert.Equal(string.Empty, first.FirstFailureLayer);
        Assert.Equal("pickerBlockedByTessellationSkip", first.DisplayStatus);
        Assert.Equal("picker", first.DisplayFirstFailureLayer);
        Assert.Equal("Audit.Picker", first.DisplayFirstDiagnostic.Source);
        Assert.Equal("Picker smoke ray produced no hit.", first.DisplayFirstDiagnostic.MessagePrefix);

        Assert.Equal(first.DisplayStatus, second.DisplayStatus);
        Assert.Equal(first.DisplayFirstFailureLayer, second.DisplayFirstFailureLayer);
        Assert.Equal(first.DisplayFirstDiagnostic.Source, second.DisplayFirstDiagnostic.Source);
        Assert.Equal(first.DisplayFirstDiagnostic.MessagePrefix, second.DisplayFirstDiagnostic.MessagePrefix);
    }

    [Fact]
    public void Step242_Stc06_AlsoMatchesDualSingleCoedgeClosedCircleCylinderFamily_AndNowAdvances()
    {
        const string relativePath = "testdata/step242/nist/STC/nist_stc_06_asme1_ap242-e3.stp";
        var fullPath = Path.Combine(Step242CorpusManifestRunner.RepoRoot(), relativePath.Replace('/', Path.DirectorySeparatorChar));
        var import = Step242Importer.ImportBody(File.ReadAllText(fullPath));
        Assert.True(import.IsSuccess);

        var body = import.Value;
        var hasFamilyMatch = body.Topology.Faces
            .Select(face => (FaceId: face.Id, Surface: body.GetFaceSurface(face.Id)))
            .Where(pair => pair.Surface.Kind == SurfaceGeometryKind.Cylinder)
            .Any(pair => IsDualSingleCoedgeClosedCircleCylinderFamily(body, pair.FaceId, pair.Surface.Cylinder!.Value, out _));
        Assert.True(hasFamilyMatch);

        var first = Step242CorpusManifestRunner.RunOne(new Step242CorpusManifestEntry("stc06", relativePath, "nist-regression", null, null, null, null, null));
        Assert.Equal("success", first.Status);
        Assert.Equal(string.Empty, first.FirstFailureLayer);
    }

    private static bool IsDualSingleCoedgeClosedCircleCylinderFamily(BrepBody body, FaceId faceId, CylinderSurface cylinder, out double centerAxialDelta)
    {
        const double fullWrapTolerance = 1e-3d;
        const double axisAlignmentTolerance = 1e-4d;
        const double centerAxisOffsetTolerance = 1e-4d;
        const double radiusTolerance = 1e-4d;
        const double minSpan = 1e-8d;

        centerAxialDelta = 0d;
        var loopIds = body.GetLoopIds(faceId);
        if (loopIds.Count != 2)
        {
            return false;
        }

        var axis = cylinder.Axis.ToVector();
        var axialCenters = new List<double>(capacity: 2);
        foreach (var loopId in loopIds)
        {
            var coedgeIds = body.GetCoedgeIds(loopId);
            if (coedgeIds.Count != 1)
            {
                return false;
            }

            var coedge = body.Topology.GetCoedge(coedgeIds[0]);
            var curve = body.GetEdgeCurve(coedge.EdgeId);
            if (curve.Kind != CurveGeometryKind.Circle3)
            {
                return false;
            }

            if (!body.TryGetEdgeVertices(coedge.EdgeId, out var startVertexId, out var endVertexId) || startVertexId != endVertexId)
            {
                return false;
            }

            if (!body.Bindings.TryGetEdgeBinding(coedge.EdgeId, out var edgeBinding)
                || edgeBinding.TrimInterval is not ParameterInterval trimInterval
                || !double.IsFinite(trimInterval.Start)
                || !double.IsFinite(trimInterval.End)
                || double.Abs((trimInterval.End - trimInterval.Start) - (2d * double.Pi)) > fullWrapTolerance)
            {
                return false;
            }

            var circle = curve.Circle3!.Value;
            var axisDot = circle.Normal.ToVector().Dot(axis);
            if (double.Abs(double.Abs(axisDot) - 1d) > axisAlignmentTolerance)
            {
                return false;
            }

            var centerOffset = circle.Center - cylinder.Origin;
            var axialCenter = centerOffset.Dot(axis);
            var radialOffset = (centerOffset - (axis * axialCenter)).Length;
            if (radialOffset > (cylinder.Radius * centerAxisOffsetTolerance))
            {
                return false;
            }

            if (double.Abs(circle.Radius - cylinder.Radius) > (cylinder.Radius * radiusTolerance))
            {
                return false;
            }

            axialCenters.Add(axialCenter);
        }

        centerAxialDelta = double.Abs(axialCenters[1] - axialCenters[0]);
        return centerAxialDelta > minSpan;
    }
}
