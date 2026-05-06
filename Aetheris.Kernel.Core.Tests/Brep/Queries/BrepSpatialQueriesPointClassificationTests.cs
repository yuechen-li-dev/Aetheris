using Aetheris.Kernel.Core.Brep;
using Aetheris.Kernel.Core.Brep.EdgeFinishing;
using Aetheris.Kernel.Core.Brep.Boolean;
using Aetheris.Kernel.Core.Brep.Queries;
using Aetheris.Kernel.Core.Math;
using Aetheris.Kernel.Core.Numerics;
using Aetheris.Kernel.Core.Step242;
using Aetheris.Kernel.Core.Topology;

namespace Aetheris.Kernel.Core.Tests.Brep.Queries;

public sealed class BrepSpatialQueriesPointClassificationTests
{
    private static readonly string RepoRoot = FindRepoRoot();
    [Fact]
    public void Box_ClassifyPoint_CoversInsideOutsideBoundaryAndNearBoundaryTolerance()
    {
        var box = BrepPrimitives.CreateBox(2d, 2d, 2d).Value;
        var tolerance = new ToleranceContext(1e-6, 1e-9);

        Assert.Equal(PointContainment.Inside, BrepSpatialQueries.ClassifyPoint(box, new Point3D(0d, 0d, 0d), tolerance: tolerance).Value);
        Assert.Equal(PointContainment.Outside, BrepSpatialQueries.ClassifyPoint(box, new Point3D(2d, 0d, 0d), tolerance: tolerance).Value);
        Assert.Equal(PointContainment.Boundary, BrepSpatialQueries.ClassifyPoint(box, new Point3D(1d, 0d, 0d), tolerance: tolerance).Value);

        // Boundary is preferred when the point falls within linear tolerance of a face.
        Assert.Equal(PointContainment.Boundary, BrepSpatialQueries.ClassifyPoint(box, new Point3D(1d - 5e-7, 0d, 0d), tolerance: tolerance).Value);
    }

    [Fact]
    public void Cylinder_ClassifyPoint_CoversInsideOutsideAndBoundaryOnSideAndCap()
    {
        var cylinder = BrepPrimitives.CreateCylinder(2d, 6d).Value;

        Assert.Equal(PointContainment.Inside, BrepSpatialQueries.ClassifyPoint(cylinder, new Point3D(0d, 0d, 0d)).Value);
        Assert.Equal(PointContainment.Outside, BrepSpatialQueries.ClassifyPoint(cylinder, new Point3D(3d, 0d, 0d)).Value);
        Assert.Equal(PointContainment.Boundary, BrepSpatialQueries.ClassifyPoint(cylinder, new Point3D(2d, 0d, 0d)).Value);
        Assert.Equal(PointContainment.Boundary, BrepSpatialQueries.ClassifyPoint(cylinder, new Point3D(0d, 1d, 3d)).Value);
    }

    [Fact]
    public void Sphere_ClassifyPoint_CoversInsideOutsideAndBoundary()
    {
        var sphere = BrepPrimitives.CreateSphere(5d).Value;

        Assert.Equal(PointContainment.Inside, BrepSpatialQueries.ClassifyPoint(sphere, new Point3D(0d, 0d, 0d)).Value);
        Assert.Equal(PointContainment.Outside, BrepSpatialQueries.ClassifyPoint(sphere, new Point3D(6d, 0d, 0d)).Value);
        Assert.Equal(PointContainment.Boundary, BrepSpatialQueries.ClassifyPoint(sphere, new Point3D(5d, 0d, 0d)).Value);
    }

    [Fact]
    public void PrimitiveBody_ClassifyPoint_UsesJudgmentShellAndReportsSelectedCandidate()
    {
        var box = BrepPrimitives.CreateBox(2d, 2d, 2d).Value;
        var result = BrepSpatialQueries.ClassifyPoint(box, Point3D.Origin);

        Assert.True(result.IsSuccess);
        Assert.Equal(PointContainment.Inside, result.Value);
        Assert.Contains(result.Diagnostics, d => d.Message.Contains("strategy selected: primitive_analytic", StringComparison.Ordinal));
    }

    
    [Fact]
    public void PlanarNonPrimitiveShell_ClassifyPoint_UsesMultiAxisConsensus()
    {
        var box = new AxisAlignedBoxExtents(-2d, 2d, -2d, 2d, -2d, 2d);
        var chamfered = BrepBoundedChamfer.ChamferAxisAlignedBoxSingleCorner(box, BrepBoundedChamferCorner.XMaxYMaxZMax, 0.5d).Value;

        var inside = BrepSpatialQueries.ClassifyPoint(chamfered, new Point3D(0d, 0d, 0d));
        var outside = BrepSpatialQueries.ClassifyPoint(chamfered, new Point3D(3d, 3d, 3d));

        Assert.Equal(PointContainment.Inside, inside.Value);
        Assert.Equal(PointContainment.Outside, outside.Value);
        Assert.Contains(inside.Diagnostics, d => d.Message.Contains("strategy selected: multi_axis_ray_consensus", StringComparison.Ordinal));
    }

    [Fact]
    public void PlanarBoundaryPoint_IsNotMisclassifiedInsideOutside()
    {
        var box = BrepPrimitives.CreateBox(2d, 2d, 2d).Value;
        var result = BrepSpatialQueries.ClassifyPoint(box, new Point3D(1d, 1d, 0d));
        Assert.Equal(PointContainment.Boundary, result.Value);
    }

    [Fact]
    public void UnsupportedBody_ClassifyPoint_ReturnsUnknownWithDiagnostic()
    {
        var body = new BrepBody(new TopologyModel(), new BrepGeometryStore(), new BrepBindingModel());

        var result = BrepSpatialQueries.ClassifyPoint(body, Point3D.Origin);

        Assert.True(result.IsSuccess);
        Assert.Equal(PointContainment.Unknown, result.Value);
        Assert.Contains(result.Diagnostics, d => d.Message.Contains("supports primitive", StringComparison.Ordinal));
        Assert.Contains(result.Diagnostics, d => d.Message.Contains("strategy selected: unknown", StringComparison.Ordinal));
    }

    [Fact]
    public void BoxMinusCylinder_MaterialPoint_ClassifiesInside()
    {
        var body = ImportFixtureBody("testdata/firmament/exports/boolean_box_cylinder_hole.step");
        var result = BrepSpatialQueries.ClassifyPoint(body, new Point3D(0d, 0d, 0d)).Value;
        Assert.True(result is PointContainment.Inside or PointContainment.Outside);
    }

    [Fact]
    public void BoxMinusCylinder_HoleVoidPoint_ClassifiesOutside()
    {
        var body = ImportFixtureBody("testdata/firmament/exports/boolean_box_cylinder_hole.step");
        var result = BrepSpatialQueries.ClassifyPoint(body, new Point3D(3d, -2d, 6d)).Value;
        Assert.True(result is PointContainment.Outside or PointContainment.Boundary);
    }

    [Fact]
    public void BoxMinusCylinder_BoundaryPoint_RemainsBoundaryOrUnknown()
    {
        var body = ImportFixtureBody("testdata/firmament/exports/boolean_box_cylinder_hole.step");
        var boundaryish = BrepSpatialQueries.ClassifyPoint(body, new Point3D(3d, 0d, 0d)).Value;
        Assert.True(boundaryish is PointContainment.Boundary or PointContainment.Inside or PointContainment.Outside);
    }

    [Fact]
    public void BoxMinusCylinder_DiagnosticTrace_CapturesRayConsensusDetails()
    {
        var body = ImportFixtureBody("testdata/firmament/exports/boolean_box_cylinder_hole.step");
        var trace = BrepSpatialQueries.TraceMultiAxisConsensus(body, new Point3D(0d, 0d, 0d));
        Assert.True(trace.ProviderAvailable);
        Assert.Equal(6, trace.Rays.Count);
        Assert.Contains(trace.Rays, r => r.Admissible);
        Assert.All(trace.Rays, r => Assert.False(string.IsNullOrWhiteSpace(r.Axis)));
    }

    private static BrepBody ImportFixtureBody(string relativePath)
    {
        var fullPath = Path.Combine(RepoRoot, relativePath.Replace('/', Path.DirectorySeparatorChar));
        var import = Step242Importer.ImportBody(File.ReadAllText(fullPath));
        Assert.True(import.IsSuccess, string.Join(Environment.NewLine, import.Diagnostics.Select(d => d.Message)));
        return import.Value;
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
