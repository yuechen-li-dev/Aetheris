using Aetheris.Continuum.Backends.Sdf;
using Aetheris.Kernel.Core.Brep;
using Aetheris.Kernel.Core.Tests.Step242;

namespace Aetheris.Continuum.Tests.Backends.Sdf;

public sealed class BrepSdfVolumeLoweringTests
{
    [Fact]
    public void ABodyWithoutResolvedShellEvidenceCannotGrantVolumeAuthority()
    {
        var body = BrepPrimitives.CreateBox(10, 8, 6).Value;
        Assert.Null(body.FaceOrientationReport);
        Assert.False(BrepSdfVolumeLowering.TryLower(body, out var field, out var reason));
        Assert.Null(field);
        Assert.Contains("qualified closed material shell", reason);
    }

    [Fact]
    [Trait("Category", "SlowCorpus")]
    public void Ctc01IsAnUncoveredTrimmedSolidRatherThanAMissingAnalyticSurfaceRecovery()
    {
        var body = Step242Corpus.Body("testdata/step242/nist/CTC/nist_ctc_01_asme1_ap242-e1.stp");
        Assert.Equal(117, body.Topology.Faces.Count());
        Assert.All(body.Topology.Faces, face => Assert.True(body.GetFaceSurface(face.Id).Kind is
            Aetheris.Kernel.Core.Geometry.SurfaceGeometryKind.Plane or
            Aetheris.Kernel.Core.Geometry.SurfaceGeometryKind.Cylinder or
            Aetheris.Kernel.Core.Geometry.SurfaceGeometryKind.Cone));
        Assert.False(BrepSdfVolumeLowering.TryLower(body, out var field, out var reason));
        Assert.Null(field); // Never return an admitted stock-only partial field.
        Assert.Contains("Face ", reason);
        Assert.True(reason.Contains("plane", StringComparison.OrdinalIgnoreCase)
            || reason.Contains("support", StringComparison.OrdinalIgnoreCase), reason);
    }
}
