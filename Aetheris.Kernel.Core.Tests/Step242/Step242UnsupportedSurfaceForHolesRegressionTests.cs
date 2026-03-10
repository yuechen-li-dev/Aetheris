using Aetheris.Kernel.Core.Step242;

namespace Aetheris.Kernel.Core.Tests.Step242;

public sealed class Step242UnsupportedSurfaceForHolesRegressionTests
{
    [Theory]
    [InlineData(
        "testdata/step242/nist/FTC/nist_ftc_08_asme1_ap242-e2.stp",
        "tessellator",
        "Topology.GraphValidator",
        "Viewer.Tessellation.PlanarNonConvexTriangulationFailed",
        "Face 4 planar loop triangulation failed because the polygon is not simple.")]
    [InlineData(
        "testdata/step242/nist/FTC/nist_ftc_11_asme1_ap242-e2.stp",
        "tessellator",
        "Topology.GraphValidator",
        "Viewer.Tessellation.CylinderTrimAxialSpanDegenerate",
        "Cylindrical face tessellation derived a degenerate trim patch due to zero axial span")]

    [InlineData(
        "testdata/step242/nist/STC/nist_stc_06_asme1_ap242-e3.stp",
        "importer-topology",
        "Importer.LoopRole.UnsupportedSurfaceForHoles",
        "Importer.LoopRole.UnsupportedSurfaceForHoles.Cone",
        "Multi-loop hole classification is unsupported for surface type 'Cone'.")]
    public void Step242_NistTargets_AdvancePastGenericUnsupportedSurfaceForHoles_Deterministically(
        string relativePath,
        string expectedLayer,
        string disallowedSource,
        string? expectedSource,
        string? expectedMessagePrefix)
    {
        var entry = new Step242CorpusManifestEntry(
            Id: Path.GetFileNameWithoutExtension(relativePath),
            Path: relativePath,
            Group: "nist-loop-role-regression",
            Notes: "regression",
            ExpectedFirstDiagnostic: null,
            ExpectHashStableAfterCanonicalization: null,
            ExpectTopologyCounts: null,
            ExpectGeometryKinds: null);

        var first = Step242CorpusManifestRunner.RunOne(entry);
        var second = Step242CorpusManifestRunner.RunOne(entry);

        Assert.Equal(expectedLayer, first.FirstFailureLayer);
        Assert.NotEqual(disallowedSource, first.FirstDiagnostic.Source);
        if (expectedSource is not null)
        {
            Assert.Equal(expectedSource, first.FirstDiagnostic.Source);
        }

        if (expectedMessagePrefix is not null)
        {
            Assert.StartsWith(expectedMessagePrefix, first.FirstDiagnostic.MessagePrefix, StringComparison.Ordinal);
        }

        Assert.NotEqual("Importer.LoopRole.UnsupportedSurfaceForHoles", first.FirstDiagnostic.Source);
        Assert.NotEqual("Importer.LoopRole.TorusDegenerateProjection", first.FirstDiagnostic.Source);
        Assert.NotEqual("Viewer.Tessellation.CylinderTrimDegenerate", first.FirstDiagnostic.Source);
        Assert.False(string.IsNullOrWhiteSpace(first.FirstDiagnostic.Source));

        Assert.Equal(first.FirstFailureLayer, second.FirstFailureLayer);
        Assert.Equal(first.FirstDiagnostic.Source, second.FirstDiagnostic.Source);
        Assert.Equal(first.FirstDiagnostic.MessagePrefix, second.FirstDiagnostic.MessagePrefix);
    }
}
