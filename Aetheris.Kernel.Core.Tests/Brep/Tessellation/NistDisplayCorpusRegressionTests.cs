using Aetheris.Kernel.Core.Brep.Tessellation;
using Aetheris.Kernel.Core.Diagnostics;
using Aetheris.Kernel.Core.Geometry.Surfaces;
using Aetheris.Kernel.Core.Math;
using Aetheris.Kernel.Core.Step242;

namespace Aetheris.Kernel.Core.Tests.Brep.Tessellation;

/// <summary>
/// Every NIST PMI corpus model must import and display without dropping a single face. Each entry here was, at
/// some point, silently missing faces in the viewer (tolerance too tight for fillet edges, weights dropped from
/// rational surfaces, per-loop projection axes, reversed same_sense edges, torus bands, ...).
/// </summary>
public sealed class NistDisplayCorpusRegressionTests
{
    public static TheoryData<string> Files => new()
    {
        "CTC/nist_ctc_01_asme1_ap242-e1.stp",
        "CTC/nist_ctc_02_asme1_ap242-e2.stp",
        "CTC/nist_ctc_03_asme1_ap242-e2.stp",
        "CTC/nist_ctc_04_asme1_ap242-e1.stp",
        "CTC/nist_ctc_05_asme1_ap242-e1.stp",
        "FTC/nist_ftc_07_asme1_ap242-e2.stp",
        "FTC/nist_ftc_08_asme1_ap242-e2.stp",
        "FTC/nist_ftc_09_asme1_ap242-e1.stp",
        "FTC/nist_ftc_10_asme1_ap242-e2.stp",
        "FTC/nist_ftc_11_asme1_ap242-e2.stp",
        "STC/nist_stc_07_asme1_ap242-e3.stp",
        "STC/nist_stc_08_asme1_ap242-e3.stp",
        "STC/nist_stc_09_asme1_ap242-e3.stp",
        "STC/nist_stc_10_asme1_ap242-e2.stp",
    };

    [Theory]
    [MemberData(nameof(Files))]
    [Trait("Category", "SlowCorpus")]
    public void NistModel_DisplaysEveryFace_WithoutTessellationWarnings(string relativePath)
    {
        var import = Step242Importer.ImportBody(File.ReadAllText(Path.Combine(FindRepoRoot(), "testdata", "step242", "nist", relativePath)));
        Assert.True(import.IsSuccess, string.Join(" | ", import.Diagnostics.Select(d => d.Message)));

        var display = DisplayPreparationFallbackBuilder.Build(import.Value, null, null, TimeSpan.FromSeconds(30));

        Assert.True(display.IsSuccess, string.Join(" | ", display.Diagnostics.Select(d => d.Message)));
        var warnings = display.Diagnostics.Where(d => d.Severity != KernelDiagnosticSeverity.Info).Select(d => d.Message).ToArray();
        Assert.True(warnings.Length == 0, string.Join(Environment.NewLine, warnings.Take(5)));
        Assert.DoesNotContain(display.Value.FacePatches, patch => patch.TriangleIndices.Count == 0);
    }

    /// <summary>
    /// The weights decide where the surface actually is, so they have to be read even though the reduction means they
    /// do not survive import: the stored non-rational surface must follow the geometry the file stated, not the
    /// different polynomial surface you get by ignoring them.
    /// </summary>
    [Fact]
    public void RationalBSplineSurface_WeightsAreHonoured_EvenThoughTheyDoNotSurviveImport()
    {
        var stepText = File.ReadAllText(Path.Combine(FindRepoRoot(), "testdata", "step242", "nist", "CTC", "nist_ctc_05_asme1_ap242-e1.stp"));

        BSplineSurfaceWithKnots[] asStated;
        using (Step242Importer.PreserveRationalSurfaces())
        {
            var preserved = Step242Importer.ImportBody(stepText);
            Assert.True(preserved.IsSuccess);
            asStated = SplineSurfaces(preserved.Value);
        }

        var stored = SplineSurfaces(Step242Importer.ImportBody(stepText).Value);

        Assert.NotEmpty(asStated);
        Assert.All(asStated, surface => Assert.True(surface.IsRational));
        Assert.All(stored, surface => Assert.False(surface.IsRational));
        Assert.Equal(asStated.Length, stored.Length);

        // One tenth of the 0.0165 mm accuracy this file declares for its own geometry.
        const double budget = 1.655e-3d;
        foreach (var (rational, reduced) in asStated.Zip(stored))
        {
            for (var indexU = 0; indexU <= 8; indexU++)
            {
                var u = rational.DomainStartU + ((rational.DomainEndU - rational.DomainStartU) * indexU / 8d);
                for (var indexV = 0; indexV <= 8; indexV++)
                {
                    var v = rational.DomainStartV + ((rational.DomainEndV - rational.DomainStartV) * indexV / 8d);
                    Assert.True((rational.Evaluate(u, v) - reduced.Evaluate(u, v)).Length <= budget);
                }
            }
        }
    }

    /// <summary>The debug path has to stay whole: preserved rationals must export and come back as rationals.</summary>
    [Fact]
    public void PreservedRationalSurfaces_SurviveAStepRoundTrip()
    {
        var stepText = File.ReadAllText(Path.Combine(FindRepoRoot(), "testdata", "step242", "nist", "CTC", "nist_ctc_05_asme1_ap242-e1.stp"));

        using var scope = Step242Importer.PreserveRationalSurfaces();
        var import = Step242Importer.ImportBody(stepText);
        Assert.True(import.IsSuccess);

        var exported = Step242Exporter.ExportBody(import.Value);
        Assert.True(exported.IsSuccess, string.Join(" | ", exported.Diagnostics.Select(d => d.Message)));
        Assert.Contains("RATIONAL_B_SPLINE_SURFACE", exported.Value);

        var reimport = Step242Importer.ImportBody(exported.Value);
        Assert.True(reimport.IsSuccess, string.Join(" | ", reimport.Diagnostics.Select(d => d.Message)));
        Assert.Equal(SplineSurfaces(import.Value).Count(surface => surface.IsRational), SplineSurfaces(reimport.Value).Count(surface => surface.IsRational));
    }

    private static BSplineSurfaceWithKnots[] SplineSurfaces(Aetheris.Kernel.Core.Brep.BrepBody body)
        => body.Geometry.Surfaces
            .OrderBy(surface => surface.Key.Value)
            .Select(surface => surface.Value.BSplineSurfaceWithKnots)
            .Where(surface => surface is not null)
            .Select(surface => surface!)
            .ToArray();

    [Fact]
    public void RationalBSplineSurface_EvaluatesExactCircularArc()
    {
        // Quarter cylinder r=1: rational quadratic in u, linear in v.
        var w = double.Sqrt(0.5d);
        IReadOnlyList<IReadOnlyList<Point3D>> net =
        [
            [new Point3D(1, 0, 0), new Point3D(1, 0, 1)],
            [new Point3D(1, 1, 0), new Point3D(1, 1, 1)],
            [new Point3D(0, 1, 0), new Point3D(0, 1, 1)],
        ];
        IReadOnlyList<IReadOnlyList<double>> weights = [[1d, 1d], [w, w], [1d, 1d]];
        var surface = new BSplineSurfaceWithKnots(2, 1, net, "UNSPECIFIED", false, false, false, [3, 3], [2, 2], [0d, 1d], [0d, 1d], "UNSPECIFIED", weights);

        Assert.True(surface.IsRational);
        for (var i = 0; i <= 10; i++)
        {
            var point = surface.Evaluate(i / 10d, 0.5d);
            Assert.Equal(1d, double.Sqrt((point.X * point.X) + (point.Y * point.Y)), 1e-12);
        }

        // The same net without weights is a different (polynomial) surface, which is what dropping weights used to do.
        var polynomial = surface.WithWeights(null).Evaluate(0.5d, 0.5d);
        Assert.True(double.Abs(double.Sqrt((polynomial.X * polynomial.X) + (polynomial.Y * polynomial.Y)) - 1d) > 1e-2);
    }

    [Fact]
    public void UnitWeights_AreCanonicalizedToAPolynomialSurface()
    {
        IReadOnlyList<IReadOnlyList<Point3D>> net = [[new Point3D(0, 0, 0), new Point3D(0, 1, 0)], [new Point3D(1, 0, 0), new Point3D(1, 1, 0)]];
        var surface = new BSplineSurfaceWithKnots(1, 1, net, "UNSPECIFIED", false, false, false, [2, 2], [2, 2], [0d, 1d], [0d, 1d], "UNSPECIFIED", [[1d, 1d], [1d, 1d]]);

        Assert.False(surface.IsRational);
    }

    private static string FindRepoRoot()
    {
        var dir = AppContext.BaseDirectory;
        while (!string.IsNullOrWhiteSpace(dir))
        {
            if (File.Exists(Path.Combine(dir, "Aetheris.slnx"))) return dir;
            dir = Directory.GetParent(dir)?.FullName ?? string.Empty;
        }

        throw new DirectoryNotFoundException("Could not locate repository root.");
    }
}
