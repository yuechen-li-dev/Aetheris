using Aetheris.Kernel.Core.Brep;
using Aetheris.Kernel.Core.Brep.Boolean;
using Aetheris.Kernel.Core.Brep.Features;
using Aetheris.Kernel.Core.Brep.Tessellation;
using Aetheris.Kernel.Core.Diagnostics;
using Aetheris.Kernel.Core.Geometry;
using Aetheris.Kernel.Core.Geometry.Curves;
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
        var profile = PolylineProfile2D.Create(
        [
            new ProfilePoint2D(-1d, -0.5d),
            new ProfilePoint2D(1d, -0.5d),
            new ProfilePoint2D(1d, 0.5d),
            new ProfilePoint2D(-1d, 0.5d),
        ]).Value;

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
            Assert.Equal(37, polyline.Points.Count);
            AssertPointEqualWithinTolerance(polyline.Points[0], polyline.Points[^1], 12);
            Assert.True(polyline.EdgeId.IsValid);
        });
    }

    [Fact]
    public void Tessellate_UnsupportedPlanarLayout_ReturnsDeterministicNotImplemented()
    {
        var unsupported = CreateSinglePlaneFaceWithoutLoops();

        var result = BrepDisplayTessellator.Tessellate(unsupported);

        Assert.False(result.IsSuccess);
        var diagnostic = Assert.Single(result.Diagnostics, d => d.Code == KernelDiagnosticCode.NotImplemented);
        Assert.Equal("Face 1 planar tessellation requires at least one loop.", diagnostic.Message);
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

    [Fact]
    public void Tessellate_TrimmedBsplineSurface_RespectsOuterLoopBounds()
    {
        var body = CreateTrimmedBsplineSurfaceBody(
            outerBounds: (0.2d, 0.8d, 0.1d, 0.9d),
            holeBounds: null);
        var options = DisplayTessellationOptions.Create(double.Pi / 8d, 0.1d, minimumSegments: 12, maximumSegments: 12).Value;

        var result = BrepDisplayTessellator.Tessellate(body, options);

        Assert.True(result.IsSuccess);
        Assert.Empty(result.Diagnostics);

        var patch = Assert.Single(result.Value.FacePatches);
        Assert.NotEmpty(patch.TriangleIndices);
        Assert.All(GetTriangleCentroids(patch), centroid =>
        {
            Assert.InRange(centroid.X, 0.2d - 1e-6d, 0.8d + 1e-6d);
            Assert.InRange(centroid.Y, 0.1d - 1e-6d, 0.9d + 1e-6d);
        });
    }

    [Fact]
    public void Tessellate_TrimmedBsplineSurfaceWithHole_PreservesHole()
    {
        var body = CreateTrimmedBsplineSurfaceBody(
            outerBounds: (0.1d, 0.9d, 0.1d, 0.9d),
            holeBounds: (0.35d, 0.65d, 0.3d, 0.7d));
        var options = DisplayTessellationOptions.Create(double.Pi / 8d, 0.1d, minimumSegments: 12, maximumSegments: 12).Value;

        var result = BrepDisplayTessellator.Tessellate(body, options);

        Assert.True(result.IsSuccess);
        Assert.Empty(result.Diagnostics);

        var patch = Assert.Single(result.Value.FacePatches);
        Assert.NotEmpty(patch.TriangleIndices);
        Assert.All(GetTriangleCentroids(patch), centroid =>
            Assert.False(IsInsideRectangle(centroid, 0.35d, 0.65d, 0.3d, 0.7d)));
    }

    [Fact]
    public void Tessellate_TrimmedBsplineSurface_IsDeterministic()
    {
        var body = CreateTrimmedBsplineSurfaceBody(
            outerBounds: (0.1d, 0.9d, 0.1d, 0.9d),
            holeBounds: (0.35d, 0.65d, 0.3d, 0.7d));
        var options = DisplayTessellationOptions.Create(double.Pi / 8d, 0.1d, minimumSegments: 12, maximumSegments: 12).Value;

        var first = BrepDisplayTessellator.Tessellate(body, options);
        var second = BrepDisplayTessellator.Tessellate(body, options);

        Assert.True(first.IsSuccess);
        Assert.True(second.IsSuccess);

        var firstPatch = Assert.Single(first.Value.FacePatches);
        var secondPatch = Assert.Single(second.Value.FacePatches);
        Assert.Equal(firstPatch.Positions, secondPatch.Positions);
        Assert.Equal(firstPatch.Normals, secondPatch.Normals);
        Assert.Equal(firstPatch.TriangleIndices, secondPatch.TriangleIndices);
    }

    [Fact]
    public void Tessellate_TrimmedBsplineSurface_WhenUvProjectionFails_EmitsWarningAndSkipsFace()
    {
        var body = CreateTrimmedBsplineSurfaceBody(
            outerBounds: (0.1d, 0.9d, 0.1d, 0.9d),
            holeBounds: null,
            offsetOuterTopEdgeZ: 0.25d);

        var result = BrepDisplayTessellator.Tessellate(body);

        Assert.True(result.IsSuccess);
        var diagnostic = Assert.Single(result.Diagnostics);
        Assert.Equal(KernelDiagnosticCode.ValidationFailed, diagnostic.Code);
        Assert.Equal("Viewer.Tessellation.TrimEvaluationFailed", diagnostic.Source);

        var patch = Assert.Single(result.Value.FacePatches);
        Assert.Empty(patch.Positions);
        Assert.Empty(patch.TriangleIndices);
    }

    [Fact]
    public void Tessellate_PlanarFacesRemainUnchanged()
    {
        var box = BrepPrimitives.CreateBox(2d, 3d, 4d);

        var result = BrepDisplayTessellator.Tessellate(box.Value);

        Assert.True(result.IsSuccess);
        Assert.Equal(6, result.Value.FacePatches.Count);
        Assert.DoesNotContain(result.Diagnostics, diagnostic => diagnostic.Source == "Viewer.Tessellation.TrimEvaluationFailed");
        Assert.All(result.Value.FacePatches, patch => Assert.NotEmpty(patch.TriangleIndices));
    }

    private static void AssertPointEqualWithinTolerance(Point3D expected, Point3D actual, int precision)
    {
        Assert.Equal(expected.X, actual.X, precision);
        Assert.Equal(expected.Y, actual.Y, precision);
        Assert.Equal(expected.Z, actual.Z, precision);
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

    private static IEnumerable<Point3D> GetTriangleCentroids(DisplayFaceMeshPatch patch)
    {
        for (var i = 0; i < patch.TriangleIndices.Count; i += 3)
        {
            var a = patch.Positions[patch.TriangleIndices[i]];
            var b = patch.Positions[patch.TriangleIndices[i + 1]];
            var c = patch.Positions[patch.TriangleIndices[i + 2]];
            yield return new Point3D(
                (a.X + b.X + c.X) / 3d,
                (a.Y + b.Y + c.Y) / 3d,
                (a.Z + b.Z + c.Z) / 3d);
        }
    }

    private static bool IsInsideRectangle(Point3D point, double minX, double maxX, double minY, double maxY)
        => point.X > minX - 1e-6d
            && point.X < maxX + 1e-6d
            && point.Y > minY - 1e-6d
            && point.Y < maxY + 1e-6d;

    private static BrepBody CreateTrimmedBsplineSurfaceBody(
        (double UMin, double UMax, double VMin, double VMax) outerBounds,
        (double UMin, double UMax, double VMin, double VMax)? holeBounds,
        double offsetOuterTopEdgeZ = 0d)
    {
        var builder = new TopologyBuilder();
        var geometry = new BrepGeometryStore();
        var bindings = new BrepBindingModel();
        var vertexPoints = new Dictionary<VertexId, Point3D>();

        var faceLoops = new List<LoopId>();
        var curveId = 1;

        var outerVertices = CreateRectangleVertices(outerBounds, offsetTopEdgeZ: offsetOuterTopEdgeZ);
        faceLoops.Add(AddLoop(builder, geometry, bindings, vertexPoints, outerVertices, ref curveId));

        if (holeBounds is { } hole)
        {
            var innerVertices = CreateRectangleVertices(hole, offsetTopEdgeZ: 0d);
            faceLoops.Add(AddLoop(builder, geometry, bindings, vertexPoints, innerVertices, ref curveId));
        }

        var face = builder.AddFace(faceLoops);
        var shell = builder.AddShell([face]);
        builder.AddBody([shell]);

        geometry.AddSurface(
            new SurfaceGeometryId(1),
            SurfaceGeometry.FromBSplineSurfaceWithKnots(CreateBilinearBsplineSurface()));
        bindings.AddFaceBinding(new FaceGeometryBinding(face, new SurfaceGeometryId(1)));

        return new BrepBody(builder.Model, geometry, bindings, vertexPoints);
    }

    private static LoopId AddLoop(
        TopologyBuilder builder,
        BrepGeometryStore geometry,
        BrepBindingModel bindings,
        Dictionary<VertexId, Point3D> vertexPoints,
        IReadOnlyList<Point3D> vertices,
        ref int curveId)
    {
        var vertexIds = vertices
            .Select(point =>
            {
                var vertexId = builder.AddVertex();
                vertexPoints[vertexId] = point;
                return vertexId;
            })
            .ToArray();

        var loopId = builder.AllocateLoopId();
        var coedgeIds = new List<CoedgeId>(vertices.Count);
        var edgeIds = new List<EdgeId>(vertices.Count);

        for (var i = 0; i < vertices.Count; i++)
        {
            var start = vertexIds[i];
            var end = vertexIds[(i + 1) % vertices.Count];
            var edgeId = builder.AddEdge(start, end);
            edgeIds.Add(edgeId);

            var line = CreateLine(vertices[i], vertices[(i + 1) % vertices.Count], out var length);
            var geometryId = new CurveGeometryId(curveId++);
            geometry.AddCurve(geometryId, CurveGeometry.FromLine(line));
            bindings.AddEdgeBinding(new EdgeGeometryBinding(edgeId, geometryId, new ParameterInterval(0d, length)));
        }

        for (var i = 0; i < edgeIds.Count; i++)
        {
            coedgeIds.Add(builder.AllocateCoedgeId());
        }

        for (var i = 0; i < edgeIds.Count; i++)
        {
            var next = coedgeIds[(i + 1) % coedgeIds.Count];
            var prev = coedgeIds[(i - 1 + coedgeIds.Count) % coedgeIds.Count];
            builder.AddCoedge(new Coedge(coedgeIds[i], edgeIds[i], loopId, next, prev, IsReversed: false));
        }

        builder.AddLoop(new Loop(loopId, coedgeIds));
        return loopId;
    }

    private static Line3Curve CreateLine(Point3D start, Point3D end, out double length)
    {
        var delta = end - start;
        length = delta.Length;
        return new Line3Curve(start, Direction3D.Create(delta));
    }

    private static Point3D[] CreateRectangleVertices(
        (double UMin, double UMax, double VMin, double VMax) bounds,
        double offsetTopEdgeZ)
    {
        var lowerLeft = EvaluateBilinearPoint(bounds.UMin, bounds.VMin, 0d);
        var lowerRight = EvaluateBilinearPoint(bounds.UMax, bounds.VMin, 0d);
        var upperRight = EvaluateBilinearPoint(bounds.UMax, bounds.VMax, offsetTopEdgeZ);
        var upperLeft = EvaluateBilinearPoint(bounds.UMin, bounds.VMax, offsetTopEdgeZ);
        return [lowerLeft, lowerRight, upperRight, upperLeft];
    }

    private static Point3D EvaluateBilinearPoint(double u, double v, double zOffset)
        => new(u, v, (u * v) + zOffset);

    private static BSplineSurfaceWithKnots CreateBilinearBsplineSurface()
        => new(
            degreeU: 1,
            degreeV: 1,
            controlPoints:
            [
                [new Point3D(0d, 0d, 0d), new Point3D(0d, 1d, 0d)],
                [new Point3D(1d, 0d, 0d), new Point3D(1d, 1d, 1d)],
            ],
            surfaceForm: "UNSPECIFIED",
            uClosed: false,
            vClosed: false,
            selfIntersect: false,
            knotMultiplicitiesU: [2, 2],
            knotMultiplicitiesV: [2, 2],
            knotValuesU: [0d, 1d],
            knotValuesV: [0d, 1d],
            knotSpec: "UNSPECIFIED");
}
