using Aetheris.Kernel.Core.Brep;
using Aetheris.Kernel.Core.Math;
using Aetheris.Kernel.Core.Geometry;
using Aetheris.Kernel.Core.Geometry.Surfaces;
using Aetheris.Kernel.Core.Brep.Boolean;
using Xunit;

namespace Aetheris.Geometry.Tests;

public sealed class BrepMeasurementQueryTests
{
    private static MeasurementPick Pick(string id, double x, double y, double z, double dx, double dy, double dz)
        => new(id, null, [x,y,z], [dx,dy,dz]);

    [Fact]
    public void Point_picks_preserve_world_deltas_and_do_not_claim_face_clearance()
    {
        MeasurementTarget[] targets = [new("box", BrepPrimitives.CreateBox(10,20,6).Value, Transform3D.Identity)];
        var result = BrepMeasurementQuery.Measure(targets, new("points", Pick("box",1,2,30,0,0,-1), Pick("box",4,6,-30,0,0,1)));
        Assert.Equal(double.Sqrt(61), result.DistanceMm!.Value, 10);
        Assert.Equal(new double[] {3,4,-6}, result.DeltaMm!);
        Assert.Equal("Analytic BRep", result.Quality);
        Assert.Contains("not minimum clearance", result.Explanation);
    }

    [Fact]
    public void Parallel_support_planes_remove_tangential_pick_offset()
    {
        MeasurementTarget[] targets = [new("box", BrepPrimitives.CreateBox(10,20,6).Value, Transform3D.Identity)];
        var result = BrepMeasurementQuery.Measure(targets, new("planes", Pick("box",1,2,30,0,0,-1), Pick("box",4,6,-30,0,0,1)));
        Assert.Equal(6, result.DistanceMm!.Value, 10);
        Assert.Equal(new double[] {0,0,-6}, result.DeltaMm!);
        Assert.Equal(0, result.AngleDegrees);
        var angled = BrepMeasurementQuery.Measure(targets, new("planes", Pick("box",1,2,30,0,0,-1), Pick("box",30,2,0,-1,0,0)));
        Assert.Null(angled.DistanceMm); Assert.Equal(90, angled.AngleDegrees);
    }

    [Fact]
    public void Repeated_rotated_occurrences_keep_their_own_world_geometry()
    {
        var box = BrepPrimitives.CreateBox(10,20,6).Value;
        var moved = Transform3D.CreateRotationZ(double.Pi/2) * Transform3D.CreateTranslation(new(40,5,0));
        MeasurementTarget[] targets = [new("a", box, Transform3D.Identity), new("b", box, moved)];
        var result = BrepMeasurementQuery.Measure(targets, new("points", Pick("a",0,0,30,0,0,-1), Pick("b",40,5,30,0,0,-1)));
        Assert.Equal(double.Sqrt(1625), result.DistanceMm!.Value, 10);
        Assert.Equal("a", result.First.OccurrenceId); Assert.Equal("b", result.Second!.OccurrenceId);
        Assert.Equal(new double[] {40,5,0}, result.DeltaMm!);
    }

    [Fact]
    public void Cylinder_axis_spacing_and_diameters_are_not_surface_gap()
    {
        var cylinder = BrepPrimitives.CreateCylinder(3,10).Value;
        MeasurementTarget[] targets = [new("a", cylinder, Transform3D.Identity), new("b", cylinder, Transform3D.CreateTranslation(new(20,0,0)))];
        var result = BrepMeasurementQuery.Measure(targets, new("axes", Pick("a",0,-20,0,0,1,0), Pick("b",20,-20,0,0,1,0)));
        Assert.Equal(20, result.DistanceMm!.Value, 10); Assert.Equal(6, result.First.DiameterMm);
        Assert.Equal(0, result.AngleDegrees); Assert.Contains("infinite", result.Explanation);
    }

    [Fact]
    public void Skew_axes_report_the_common_perpendicular_and_acute_angle()
    {
        var cylinder = BrepPrimitives.CreateCylinder(3,10).Value;
        MeasurementTarget[] targets = [new("a", cylinder, Transform3D.Identity), new("b", cylinder,
            Transform3D.CreateRotationX(double.Pi/2) * Transform3D.CreateTranslation(new(20,0,0)))];
        var result = BrepMeasurementQuery.Measure(targets, new("axes", Pick("a",0,-20,0,0,1,0), Pick("b",20,0,20,0,0,-1)));
        Assert.Equal(20, result.DistanceMm!.Value, 10); Assert.Equal(90, result.AngleDegrees!.Value, 10);
        Assert.InRange(double.Abs(result.DeltaMm![1]), 0, 1e-10); Assert.InRange(double.Abs(result.DeltaMm[2]), 0, 1e-10);
    }

    [Fact]
    public void Unsupported_foreground_surface_cannot_be_skipped_for_an_exact_back_face()
    {
        var box = BrepPrimitives.CreateBox(10,20,6).Value;
        var top = box.Topology.Faces.Single(f => box.GetFaceSurface(f.Id).Plane is { } p && p.Origin.Z == 3);
        var replacement = new BSplineSurfaceWithKnots(1,1,
            [[new(-5,-10,3),new(-5,10,3)],[new(5,-10,3),new(5,10,3)]], "PLANE_SURF", false,false,false,[2,2],[2,2],[0,1],[0,1],"UNSPECIFIED");
        var geometry = new BrepGeometryStore();
        var replacedId = box.Bindings.GetFaceBinding(top.Id).SurfaceGeometryId;
        foreach (var curve in box.Geometry.Curves) geometry.AddCurve(curve.Key, curve.Value);
        foreach (var surface in box.Geometry.Surfaces) geometry.AddSurface(surface.Key, surface.Key == replacedId ? SurfaceGeometry.FromBSplineSurfaceWithKnots(replacement) : surface.Value);
        var unsupported = new BrepBody(box.Topology, geometry, box.Bindings);
        MeasurementTarget[] targets = [new("box", unsupported, Transform3D.Identity)];
        var request = new MeasurementRequest("points", Pick("box",0,0,30,0,0,-1));
        Assert.Equal("measurement-surface-unsupported", Assert.Throws<MeasurementException>(() => BrepMeasurementQuery.Measure(targets, request)).Code);
        var approximate = BrepMeasurementQuery.Measure(targets, request with { AllowApproximate = true });
        Assert.Equal("Approximate display", approximate.Quality); Assert.Equal(3, approximate.First.Point[2], 10);
        Assert.Equal(top.Id.Value, approximate.First.FaceId);
    }

    [Fact]
    public void Through_hole_void_does_not_become_a_surface_pick()
    {
        var box = BrepPrimitives.CreateBox(40,30,12).Value;
        var hole = BrepPrimitives.CreateCylinder(4,20).Value;
        var subtracted = BrepBoolean.Subtract(box, hole);
        Assert.True(subtracted.IsSuccess);
        MeasurementTarget[] targets = [new("plate", subtracted.Value, Transform3D.Identity)];
        Assert.Equal("measurement-pick-missed", Assert.Throws<MeasurementException>(() => BrepMeasurementQuery.Measure(targets,
            new("points", Pick("plate",0,0,30,0,0,-1)))).Code);
        var walls = BrepMeasurementQuery.Measure(targets, new("axes", Pick("plate",0,0,0,1,0,0)));
        Assert.Equal(8, walls.First.DiameterMm);
    }

    [Fact]
    public void Missed_stale_and_wrong_kind_references_are_diagnostics_not_zero()
    {
        MeasurementTarget[] targets = [new("box", BrepPrimitives.CreateBox(10,20,6).Value, Transform3D.Identity)];
        Assert.Equal("measurement-pick-missed", Assert.Throws<MeasurementException>(() => BrepMeasurementQuery.Measure(targets,
            new("points", Pick("old",0,0,30,0,0,-1)))).Code);
        Assert.Equal("measurement-axis-required", Assert.Throws<MeasurementException>(() => BrepMeasurementQuery.Measure(targets,
            new("axes", Pick("box",0,0,30,0,0,-1)))).Code);
        Assert.Equal("measurement-ray-invalid", Assert.Throws<MeasurementException>(() => BrepMeasurementQuery.Measure(targets,
            new("points", Pick("box",0,0,30,0,0,0)))).Code);
        Assert.Equal("measurement-pick-missed", Assert.Throws<MeasurementException>(() => BrepMeasurementQuery.Measure(targets,
            new("points", Pick("box",0,0,30,0,0,-1) with { FaceId = int.MaxValue }))).Code);
    }
}
