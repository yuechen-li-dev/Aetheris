using Aetheris.Kernel.Core.Geometry;
using Aetheris.Kernel.Core.Geometry.Surfaces;
using Aetheris.Kernel.Core.Math;
using Aetheris.Kernel.Core.Step242;

namespace Aetheris.Kernel.Core.Tests.Step242;

/// <summary>
/// Aetheris keeps an analytic B-rep, so a spline face that is really a primitive has to be imported as one. These
/// cover the generalized recovery lane: the exact rational sweeps every exporter writes are recovered, and the
/// free-form blends that merely resemble them are not.
/// </summary>
public sealed class Step242BsplineSurfaceRecoveryTests
{
    private static readonly double ArcWeight = double.Sqrt(0.5d);

    [Fact]
    public void ExactRationalCone_IsRecoveredAsAnAnalyticCone()
    {
        var decision = Step242BsplineSurfaceRecoveryLane.Decide(CreateExactRationalCone(baseRadius: 4d, topRadius: 8d, height: 8d));

        Assert.Equal("analytic_cone", decision.CandidateName);
        var cone = Assert.IsType<ConeSurface>(decision.RecoveredSurface!.Cone);
        Assert.Equal(double.Atan(0.5d), cone.SemiAngleRadians, 9);
        Assert.Equal(0d, cone.Axis.ToVector().Cross(new Vector3D(0d, 0d, 1d)).Length, 9);
    }

    [Fact]
    public void ExactRationalSphereOctant_IsRecoveredAsAnAnalyticSphere()
    {
        var decision = Step242BsplineSurfaceRecoveryLane.Decide(CreateExactRationalSphereOctant(radius: 7d));

        Assert.Equal("analytic_sphere", decision.CandidateName);
        var sphere = Assert.IsType<SphereSurface>(decision.RecoveredSurface!.Sphere);
        Assert.Equal(7d, sphere.Radius, 9);
        Assert.Equal(0d, (sphere.Center - Point3D.Origin).Length, 9);
    }

    [Fact]
    public void ExactRationalTorusPatch_IsRecoveredAsAnAnalyticTorus()
    {
        var decision = Step242BsplineSurfaceRecoveryLane.Decide(CreateExactRationalTorusPatch(majorRadius: 20d, minorRadius: 3d));

        Assert.Equal("analytic_torus", decision.CandidateName);
        var torus = Assert.IsType<TorusSurface>(decision.RecoveredSurface!.Torus);
        Assert.Equal(20d, torus.MajorRadius, 8);
        Assert.Equal(3d, torus.MinorRadius, 8);
    }

    /// <summary>
    /// The exactness gate is what separates an encoded primitive from a blend, so a sweep that is a circle only to
    /// within a modelling tolerance must stay a spline however closely it fits.
    /// </summary>
    [Fact]
    public void NearlyCircularSweep_IsRejectedBecauseItsSectionsAreNotExact()
    {
        var nudged = CreateExactRationalTorusPatch(majorRadius: 20d, minorRadius: 3d);
        var controlPoints = nudged.ControlPoints
            .Select((row, rowIndex) => (IReadOnlyList<Point3D>)row
                .Select((point, columnIndex) => rowIndex == 1 && columnIndex == 1
                    ? new Point3D(point.X + 1e-4d, point.Y, point.Z)
                    : point)
                .ToArray())
            .ToArray();
        var perturbed = new BSplineSurfaceWithKnots(
            nudged.DegreeU, nudged.DegreeV, controlPoints, nudged.SurfaceForm, nudged.UClosed, nudged.VClosed,
            nudged.SelfIntersect, nudged.KnotMultiplicitiesU, nudged.KnotMultiplicitiesV, nudged.KnotValuesU,
            nudged.KnotValuesV, nudged.KnotSpec, nudged.Weights);

        var decision = Step242BsplineSurfaceRecoveryLane.Decide(perturbed);

        Assert.Equal("reject", decision.CandidateName);
        Assert.Contains("exact", decision.Reason, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// The free-form blends of CTC-02 fit a torus to a few thousandths of a millimetre, which is close enough that a
    /// tolerance-based test would swap them for one. They are spline faces and must stay spline faces.
    /// </summary>
    [Fact]
    [Trait("Category", "SlowCorpus")]
    public void NistCtc02FreeFormBlends_AreNotRecovered()
    {
        var import = Step242Importer.ImportBody(ReadFixture("testdata/step242/nist/CTC/nist_ctc_02_asme1_ap242-e2.stp"));

        Assert.True(import.IsSuccess);
        Assert.Equal(34, CountFaceSurfaces(import.Value, SurfaceGeometryKind.BSplineSurfaceWithKnots));
        Assert.DoesNotContain(import.Diagnostics, diagnostic => (diagnostic.Source ?? string.Empty).Contains("BsplineSurfaceRecovery", StringComparison.Ordinal));
    }

    /// <summary>
    /// CTC-05's rational faces are five-sided vertex blends, not primitives. Import says which primitives it weighed
    /// and on what measurement it rejected each, rather than reporting an unexplained "unsupported".
    /// </summary>
    [Fact]
    public void NistCtc05VertexBlends_ReportEveryPrimitiveTheyWereMeasuredAgainst()
    {
        var import = Step242Importer.ImportBody(ReadFixture("testdata/step242/nist/CTC/nist_ctc_05_asme1_ap242-e1.stp"));

        Assert.True(import.IsSuccess);
        var rejected = import.Diagnostics
            .Where(diagnostic => (diagnostic.Source ?? string.Empty).Contains("RationalBSplineReduced", StringComparison.Ordinal))
            .ToArray();
        Assert.Equal(4, rejected.Length);
        Assert.All(rejected, diagnostic => Assert.Contains("analytic_torus", diagnostic.Message, StringComparison.Ordinal));
        Assert.All(rejected, diagnostic => Assert.Contains("analytic_cylinder", diagnostic.Message, StringComparison.Ordinal));
    }

    [Fact]
    public void RecoveredSurfaces_ReportWhatTheyReplacedAndHowCloselyTheyMatch()
    {
        var import = Step242Importer.ImportBody(ReadFixture("testdata/step242/OCCT/rod.step"));

        Assert.True(import.IsSuccess);
        var recovered = import.Diagnostics
            .Where(diagnostic => (diagnostic.Source ?? string.Empty).Contains("BsplineSurfaceRecovery", StringComparison.Ordinal))
            .ToArray();
        Assert.Equal(2, recovered.Length);
        Assert.All(recovered, diagnostic => Assert.Contains("reproduces the spline to", diagnostic.Message, StringComparison.Ordinal));
    }

    private static int CountFaceSurfaces(Aetheris.Kernel.Core.Brep.BrepBody body, SurfaceGeometryKind kind)
        => body.Topology.Faces.Count(face => body.TryGetFaceSurfaceGeometry(face.Id, out var geometry) && geometry!.Kind == kind);

    /// <summary>Quarter-circle rings as exact rational quadratics, blended linearly so the radius varies with height.</summary>
    private static BSplineSurfaceWithKnots CreateExactRationalCone(double baseRadius, double topRadius, double height)
        => new(
            degreeU: 1,
            degreeV: 2,
            controlPoints: [QuarterArcRing(baseRadius, 0d), QuarterArcRing(topRadius, height)],
            surfaceForm: "CONICAL_SURF",
            uClosed: false,
            vClosed: false,
            selfIntersect: false,
            knotMultiplicitiesU: [2, 2],
            knotMultiplicitiesV: [3, 3],
            knotValuesU: [0d, 1d],
            knotValuesV: [0d, 1d],
            knotSpec: "PIECEWISE_BEZIER_KNOTS",
            weights: [ArcWeights(), ArcWeights()]);

    /// <summary>A quarter meridian revolved through a quarter turn: the octant every exporter writes for a sphere.</summary>
    private static BSplineSurfaceWithKnots CreateExactRationalSphereOctant(double radius)
    {
        Point3D[] meridian = [new(0d, 0d, radius), new(radius, 0d, radius), new(radius, 0d, 0d)];
        double[] meridianWeights = [1d, ArcWeight, 1d];
        return Revolve(meridian, meridianWeights, degreeU: 2, "SPHERICAL_SURF");
    }

    /// <summary>A quarter of the minor circle revolved through a quarter turn about the torus axis.</summary>
    private static BSplineSurfaceWithKnots CreateExactRationalTorusPatch(double majorRadius, double minorRadius)
    {
        Point3D[] minorArc =
        [
            new(majorRadius + minorRadius, 0d, 0d),
            new(majorRadius + minorRadius, 0d, minorRadius),
            new(majorRadius, 0d, minorRadius)
        ];
        double[] minorWeights = [1d, ArcWeight, 1d];
        return Revolve(minorArc, minorWeights, degreeU: 2, "TOROIDAL_SURF");
    }

    /// <summary>Revolves a profile in the xz plane through a quarter turn about z as an exact rational surface.</summary>
    private static BSplineSurfaceWithKnots Revolve(IReadOnlyList<Point3D> profile, IReadOnlyList<double> profileWeights, int degreeU, string surfaceForm)
    {
        var controlPoints = profile
            .Select(point => (IReadOnlyList<Point3D>)new Point3D[]
            {
                new(point.X, 0d, point.Z),
                new(point.X, point.X, point.Z),
                new(0d, point.X, point.Z)
            })
            .ToArray();
        var weights = profileWeights
            .Select(weight => (IReadOnlyList<double>)new[] { weight, weight * ArcWeight, weight })
            .ToArray();

        return new BSplineSurfaceWithKnots(
            degreeU: degreeU,
            degreeV: 2,
            controlPoints: controlPoints,
            surfaceForm: surfaceForm,
            uClosed: false,
            vClosed: false,
            selfIntersect: false,
            knotMultiplicitiesU: [degreeU + 1, degreeU + 1],
            knotMultiplicitiesV: [3, 3],
            knotValuesU: [0d, 1d],
            knotValuesV: [0d, 1d],
            knotSpec: "PIECEWISE_BEZIER_KNOTS",
            weights: weights);
    }

    private static IReadOnlyList<Point3D> QuarterArcRing(double radius, double z)
        => [new Point3D(radius, 0d, z), new Point3D(radius, radius, z), new Point3D(0d, radius, z)];

    private static IReadOnlyList<double> ArcWeights() => [1d, ArcWeight, 1d];

    private static string ReadFixture(string relativePath)
        => File.ReadAllText(Path.Combine(Step242CorpusManifestRunner.RepoRoot(), relativePath.Replace('/', Path.DirectorySeparatorChar)));
}
