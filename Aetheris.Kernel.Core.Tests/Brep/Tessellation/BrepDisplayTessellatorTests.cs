using Aetheris.Kernel.Core.Brep;
using Aetheris.Kernel.Core.Brep.Boolean;
using Aetheris.Kernel.Core.Brep.Features;
using Aetheris.Kernel.Core.Brep.Tessellation;
using Aetheris.Kernel.Core.Diagnostics;
using Aetheris.Kernel.Core.Geometry;
using Aetheris.Kernel.Core.Geometry.Surfaces;
using Aetheris.Kernel.Core.Math;
using Aetheris.Kernel.Core.Topology;

namespace Aetheris.Kernel.Core.Tests.Brep.Tessellation;

public sealed class BrepDisplayTessellatorTests
{
    private static readonly ExtrudeFrame3D DefaultFrame = new(
        Point3D.Origin,
        Direction3D.Create(new Vector3D(0d, 0d, 1d)),
        Direction3D.Create(new Vector3D(1d, 0d, 0d)));

    private static readonly RevolveAxis3D DefaultAxis = new(
        Point3D.Origin,
        new Vector3D(0d, 0d, 1d));

    [Fact]
    public void DisplayTessellationOptions_Default_IsValid()
        => Assert.Empty(DisplayTessellationOptions.Default.Validate());

    [Fact]
    public void DisplayTessellationOptions_Create_WithValidValues_Succeeds()
    {
        var result = DisplayTessellationOptions.Create(double.Pi / 16d, 0.01d, 10, 100);
        Assert.True(result.IsSuccess);
    }

    [Theory]
    [InlineData(0d, 0.1d)]
    [InlineData(-1d, 0.1d)]
    [InlineData(double.NaN, 0.1d)]
    [InlineData(double.PositiveInfinity, 0.1d)]
    [InlineData(double.Pi / 8d, 0d)]
    [InlineData(double.Pi / 8d, -1d)]
    [InlineData(double.Pi / 8d, double.NaN)]
    [InlineData(double.Pi / 8d, double.PositiveInfinity)]
    public void DisplayTessellationOptions_Create_InvalidFloatingValues_Fails(double angularToleranceRadians, double chordTolerance)
    {
        var result = DisplayTessellationOptions.Create(angularToleranceRadians, chordTolerance, 8, 64);
        Assert.False(result.IsSuccess);
        Assert.Contains(result.Diagnostics, d => d.Code == KernelDiagnosticCode.InvalidArgument);
    }

    [Fact]
    public void DisplayTessellationOptions_Create_InvalidSegmentBounds_Fails()
    {
        var result = DisplayTessellationOptions.Create(double.Pi / 8d, 0.1d, minimumSegments: 32, maximumSegments: 8);
        Assert.False(result.IsSuccess);
        Assert.Contains(result.Diagnostics, d => d.Code == KernelDiagnosticCode.InvalidArgument && d.Message.Contains("MinimumSegments", StringComparison.Ordinal));
    }

    [Fact]
    public void Tessellate_BoxBody_SucceedsWithFaceAndEdgeBackreferences()
    {
        var box = BrepPrimitives.CreateBox(3d, 2d, 1d);
        var result = BrepDisplayTessellator.Tessellate(box.Value);

        Assert.True(result.IsSuccess);
        Assert.Equal(6, result.Value.FacePatches.Count);
        Assert.Equal(12, result.Value.EdgePolylines.Count);
        Assert.All(result.Value.FacePatches, patch =>
        {
            Assert.True(patch.FaceId.IsValid);
            Assert.NotEmpty(patch.TriangleIndices);
            Assert.Equal(patch.Positions.Count, patch.Normals.Count);
        });
    }

    [Fact]
    public void Tessellate_ExtrudeRectangle_Succeeds()
    {
        var profile = new PolylineProfile2D(
        [
            new ProfilePoint2D(-1d, -0.5d),
            new ProfilePoint2D(1d, -0.5d),
            new ProfilePoint2D(1d, 0.5d),
            new ProfilePoint2D(-1d, 0.5d),
        ]);

        var extruded = BrepExtrude.Create(profile, DefaultFrame, 2d);
        var result = BrepDisplayTessellator.Tessellate(extruded.Value);

        Assert.True(result.IsSuccess);
        Assert.Equal(extruded.Value.Topology.Faces.Count(), result.Value.FacePatches.Count);
    }

    [Fact]
    public void Tessellate_RevolveSupportedCase_SucceedsAndIncludesCurvedPatch()
    {
        var revolved = BrepRevolve.Create(
            [new ProfilePoint2D(1d, 0d), new ProfilePoint2D(2d, 3d)],
            DefaultFrame,
            DefaultAxis);

        var result = BrepDisplayTessellator.Tessellate(revolved.Value);

        Assert.True(result.IsSuccess);
        Assert.Equal(3, result.Value.FacePatches.Count);
    }

    [Fact]
    public void Tessellate_SpherePrimitive_Succeeds()
    {
        var sphere = BrepPrimitives.CreateSphere(2d);
        var result = BrepDisplayTessellator.Tessellate(sphere.Value);

        Assert.True(result.IsSuccess);
        Assert.Single(result.Value.FacePatches);
        Assert.NotEmpty(result.Value.FacePatches[0].TriangleIndices);
    }

    [Fact]
    public void Tessellate_LineEdges_ProduceTwoPointPolylines()
    {
        var box = BrepPrimitives.CreateBox(2d, 2d, 2d);
        var result = BrepDisplayTessellator.Tessellate(box.Value);

        Assert.True(result.IsSuccess);
        Assert.All(result.Value.EdgePolylines, polyline =>
        {
            Assert.True(polyline.EdgeId.IsValid);
            Assert.Equal(2, polyline.Points.Count);
            Assert.False(polyline.IsClosed);
        });
    }

    [Fact]
    public void Tessellate_CircleEdges_UseDeterministicClosedSampling()
    {
        var cylinder = BrepPrimitives.CreateCylinder(1.5d, 4d);
        var options = DisplayTessellationOptions.Create(double.Pi / 6d, 0.25d, minimumSegments: 12, maximumSegments: 12).Value;

        var result = BrepDisplayTessellator.Tessellate(cylinder.Value, options);

        Assert.True(result.IsSuccess);

        var circlePolylines = result.Value.EdgePolylines.Where(p => p.IsClosed).ToArray();
        Assert.Equal(2, circlePolylines.Length);
        Assert.All(circlePolylines, polyline =>
        {
            Assert.Equal(13, polyline.Points.Count);
            Assert.Equal(polyline.Points[0], polyline.Points[^1]);
            Assert.True(polyline.EdgeId.IsValid);
        });
    }

    [Fact]
    public void Tessellate_UnsupportedPlanarLayout_ReturnsDeterministicNotImplemented()
    {
        var unsupported = CreateSinglePlaneFaceWithoutLoops();

        var result = BrepDisplayTessellator.Tessellate(unsupported);

        Assert.False(result.IsSuccess);
        var diagnostic = Assert.Single(result.Diagnostics.Where(d => d.Code == KernelDiagnosticCode.NotImplemented));
        Assert.Equal("Face 1 planar tessellation requires exactly one loop.", diagnostic.Message);
    }

    [Fact]
    public void Tessellate_M13BooleanBoxOutput_Succeeds()
    {
        var left = BrepBooleanBoxRecognition.CreateBoxFromExtents(new AxisAlignedBoxExtents(0d, 4d, 0d, 4d, 0d, 4d)).Value;
        var right = BrepBooleanBoxRecognition.CreateBoxFromExtents(new AxisAlignedBoxExtents(2d, 6d, 1d, 3d, -1d, 2d)).Value;
        var booleanResult = BrepBoolean.Intersect(left, right);

        var tessellation = BrepDisplayTessellator.Tessellate(booleanResult.Value);

        Assert.True(booleanResult.IsSuccess);
        Assert.True(tessellation.IsSuccess);
        Assert.NotEmpty(tessellation.Value.FacePatches);
    }

    private static BrepBody CreateSinglePlaneFaceWithoutLoops()
    {
        var builder = new TopologyBuilder();
        var face = builder.AddFace([]);
        var shell = builder.AddShell([face]);
        builder.AddBody([shell]);

        var geometry = new BrepGeometryStore();
        geometry.AddSurface(
            new SurfaceGeometryId(1),
            SurfaceGeometry.FromPlane(new PlaneSurface(
                Point3D.Origin,
                Direction3D.Create(new Vector3D(0d, 0d, 1d)),
                Direction3D.Create(new Vector3D(1d, 0d, 0d)))));

        var bindings = new BrepBindingModel();
        bindings.AddFaceBinding(new FaceGeometryBinding(face, new SurfaceGeometryId(1)));

        return new BrepBody(builder.Model, geometry, bindings);
    }
}
