using Aetheris.Kernel.Core.Geometry;
using Aetheris.Kernel.Core.Geometry.Surfaces;
using Aetheris.Kernel.Core.Math;
using Aetheris.Kernel.Core.Step242;

namespace Aetheris.Kernel.Core.Tests.Step242;

/// <summary>
/// Blends that no analytic primitive covers still must not enter the kernel as NURBS. These cover the reduction that
/// replaces them with a non-rational spline inside the accuracy the source file declares, and the debug switch that
/// turns it off when a degenerate legacy surface needs to be looked at as the file stated it.
/// </summary>
public sealed class Step242RationalSurfaceReductionTests
{
    public static TheoryData<string> CorpusFiles => new()
    {
        "testdata/step242/nist/CTC/nist_ctc_02_asme1_ap242-e2.stp",
        "testdata/step242/nist/CTC/nist_ctc_05_asme1_ap242-e1.stp",
        "testdata/step242/nist/FTC/nist_ftc_07_asme1_ap242-e2.stp",
        "testdata/step242/nist/FTC/nist_ftc_10_asme1_ap242-e2.stp",
        "testdata/step242/nist/STC/nist_stc_07_asme1_ap242-e3.stp",
        "testdata/step242/nist/STC/nist_stc_10_asme1_ap242-e2.stp",
        "testdata/step242/OCCT/rod.step",
        "testdata/step242/OCCT/bolt001.step",
    };

    /// <summary>The invariant the whole lane exists to hold: an imported body carries no rational surface.</summary>
    [Theory]
    [MemberData(nameof(CorpusFiles))]
    public void ImportedBody_CarriesNoRationalSurface(string relativePath)
    {
        var import = Step242Importer.ImportBody(ReadFixture(relativePath));

        Assert.True(import.IsSuccess);
        Assert.Equal(0, CountRationalSurfaces(import.Value));
    }

    [Theory]
    [MemberData(nameof(CorpusFiles))]
    public void ExportedFile_ContainsNoRationalSurface(string relativePath)
    {
        var import = Step242Importer.ImportBody(ReadFixture(relativePath));
        Assert.True(import.IsSuccess);

        var exported = Step242Exporter.ExportBody(import.Value);

        Assert.True(exported.IsSuccess);
        Assert.DoesNotContain("RATIONAL_B_SPLINE_SURFACE", exported.Value, StringComparison.Ordinal);
    }

    [Fact]
    public void NistVertexBlends_AreReducedAndSayHowCloselyTheyFollowTheOriginal()
    {
        var import = Step242Importer.ImportBody(ReadFixture("testdata/step242/nist/CTC/nist_ctc_05_asme1_ap242-e1.stp"));

        Assert.True(import.IsSuccess);
        var reduced = import.Diagnostics
            .Where(diagnostic => (diagnostic.Source ?? string.Empty).Contains("RationalBSplineReduced", StringComparison.Ordinal))
            .ToArray();
        Assert.Equal(4, reduced.Length);
        Assert.All(reduced, diagnostic => Assert.Contains("at matched parameters", diagnostic.Message, StringComparison.Ordinal));
    }

    /// <summary>
    /// The debug switch has to give back exactly what the file stated, rationals included, and export has to write
    /// them, or it is no use for diagnosing a surface that the reduction mishandled.
    /// </summary>
    [Fact]
    public void PreserveRationalSurfaces_KeepsTheFileAsStated_AndExportsItRational()
    {
        var stepText = ReadFixture("testdata/step242/nist/CTC/nist_ctc_05_asme1_ap242-e1.stp");

        using (Step242Importer.PreserveRationalSurfaces())
        {
            var preserved = Step242Importer.ImportBody(stepText);
            Assert.True(preserved.IsSuccess);
            Assert.Equal(4, CountRationalSurfaces(preserved.Value));

            var exported = Step242Exporter.ExportBody(preserved.Value);
            Assert.True(exported.IsSuccess);
            Assert.Contains("RATIONAL_B_SPLINE_SURFACE", exported.Value, StringComparison.Ordinal);
        }

        var afterScope = Step242Importer.ImportBody(stepText);
        Assert.True(afterScope.IsSuccess);
        Assert.Equal(0, CountRationalSurfaces(afterScope.Value));
    }

    /// <summary>
    /// Reduction is measured at matched parameters, not closest point, so the parameter map moves no further than the
    /// geometry does and trim intervals keep meaning what they meant.
    /// </summary>
    [Fact]
    public void Reduction_FollowsTheRationalSurfaceAtTheSameParameters_AndKeepsItsDomain()
    {
        var rational = Step242OcctCylinderRecoveryTests.CreateExactRationalCylinder(radius: 5d, height: 10d);
        // Twenty nanometres on a 5 mm radius: two orders below any CAD accuracy, and far inside what the refinement
        // cap can reach for a quadratic section.
        const double tolerance = 1e-4d;

        Assert.True(BSplineSurfaceRationalReduction.TryReduce(rational, tolerance, out var reduced, out var deviation, out _));

        Assert.NotNull(reduced);
        Assert.False(reduced!.IsRational);
        Assert.True(deviation <= tolerance, $"deviation {deviation} exceeded {tolerance}");
        Assert.Equal(rational.DomainStartU, reduced.DomainStartU, 12);
        Assert.Equal(rational.DomainEndU, reduced.DomainEndU, 12);
        Assert.Equal(rational.DomainStartV, reduced.DomainStartV, 12);
        Assert.Equal(rational.DomainEndV, reduced.DomainEndV, 12);

        for (var indexU = 0; indexU <= 20; indexU++)
        {
            var u = rational.DomainStartU + ((rational.DomainEndU - rational.DomainStartU) * indexU / 20d);
            for (var indexV = 0; indexV <= 20; indexV++)
            {
                var v = rational.DomainStartV + ((rational.DomainEndV - rational.DomainStartV) * indexV / 20d);
                Assert.True((rational.Evaluate(u, v) - reduced.Evaluate(u, v)).Length <= tolerance);
            }
        }
    }

    /// <summary>A tighter budget has to buy a closer fit, or the refinement is not doing anything.</summary>
    [Fact]
    public void Reduction_TightensTheFitWhenTheBudgetTightens()
    {
        var rational = Step242OcctCylinderRecoveryTests.CreateExactRationalCylinder(radius: 5d, height: 10d);

        Assert.True(BSplineSurfaceRationalReduction.TryReduce(rational, 1e-3d, out var loose, out var looseDeviation, out _));
        Assert.True(BSplineSurfaceRationalReduction.TryReduce(rational, 1e-5d, out var tight, out var tightDeviation, out _));

        Assert.True(tightDeviation < looseDeviation);
        Assert.True(tight!.ControlPoints[0].Count > loose!.ControlPoints[0].Count);

        // Only the rational direction earns the extra spans; the ruled direction is already exact.
        Assert.Equal(rational.ControlPoints.Count, tight.ControlPoints.Count);
    }

    [Fact]
    public void ReductionBudget_IsOneTenthOfTheAccuracyTheFileDeclares()
    {
        var rational = Step242OcctCylinderRecoveryTests.CreateExactRationalCylinder(radius: 5d, height: 10d);

        Assert.Equal(5e-4d, BSplineSurfaceRationalReduction.ResolveTolerance(rational, 5e-3d), 12);
    }

    private static int CountRationalSurfaces(Aetheris.Kernel.Core.Brep.BrepBody body)
        => body.Geometry.Surfaces.Count(surface => surface.Value.BSplineSurfaceWithKnots is { IsRational: true });

    private static string ReadFixture(string relativePath)
        => File.ReadAllText(Path.Combine(Step242CorpusManifestRunner.RepoRoot(), relativePath.Replace('/', Path.DirectorySeparatorChar)));
}
