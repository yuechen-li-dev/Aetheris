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


    [Fact]
    public void Tessellate_TrimmedCylinderSurfaceWithHole_RespectsOuterLoopAndHole_Deterministically()
    {
        var cylinder = new CylinderSurface(
            new Point3D(0d, 0d, 0d),
            Direction3D.Create(new Vector3D(0d, 0d, 1d)),
            2d,
            Direction3D.Create(new Vector3D(1d, 0d, 0d)));
        var outerBounds = (UMin: 0.6d, UMax: 2.4d, VMin: 0.5d, VMax: 3.5d);
        var holeBounds = (UMin: 1.2d, UMax: 1.8d, VMin: 1.4d, VMax: 2.4d);
        var body = CreateTrimmedCylinderSurfaceBody(cylinder, outerBounds, holeBounds);
        var options = DisplayTessellationOptions.Create(double.Pi / 8d, 0.15d, minimumSegments: 12, maximumSegments: 12).Value;

        var first = BrepDisplayTessellator.Tessellate(body, options);
        var second = BrepDisplayTessellator.Tessellate(body, options);

        Assert.True(first.IsSuccess);
        Assert.True(second.IsSuccess);
        Assert.DoesNotContain(first.Diagnostics, diagnostic => diagnostic.Source == "Viewer.Tessellation.TrimEvaluationFailed");

        var firstPatch = Assert.Single(first.Value.FacePatches);
        var secondPatch = Assert.Single(second.Value.FacePatches);
        Assert.NotEmpty(firstPatch.TriangleIndices);
        Assert.Equal(firstPatch.Positions, secondPatch.Positions);
        Assert.Equal(firstPatch.Normals, secondPatch.Normals);
        Assert.Equal(firstPatch.TriangleIndices, secondPatch.TriangleIndices);

        var outerMidU = (outerBounds.UMin + outerBounds.UMax) * 0.5d;
        Assert.All(GetTriangleUvCentroids(firstPatch, point => ProjectCylinderUv(cylinder, point, outerMidU)), centroid =>
        {
            Assert.True(IsInsideUvRectangle(centroid, outerBounds));
            Assert.False(IsInsideUvRectangle(centroid, holeBounds));
        });
    }

    [Fact]
    public void Tessellate_TrimmedConeSurfaceWithHole_RespectsOuterLoopAndHole_Deterministically()
    {
        var cone = new ConeSurface(
            placementOrigin: new Point3D(0d, 0d, 1d),
            axis: Direction3D.Create(new Vector3D(0d, 0d, 1d)),
            placementRadius: 1d,
            semiAngleRadians: double.Pi / 6d,
            referenceAxis: Direction3D.Create(new Vector3D(1d, 0d, 0d)));
        var outerBounds = (UMin: 0.5d, UMax: 2.2d, VMin: 1d, VMax: 3.4d);
        var holeBounds = (UMin: 1.0d, UMax: 1.6d, VMin: 1.7d, VMax: 2.5d);
        var body = CreateTrimmedConeSurfaceBody(cone, outerBounds, holeBounds);
        var options = DisplayTessellationOptions.Create(double.Pi / 8d, 0.15d, minimumSegments: 12, maximumSegments: 12).Value;

        var first = BrepDisplayTessellator.Tessellate(body, options);
        var second = BrepDisplayTessellator.Tessellate(body, options);

        Assert.True(first.IsSuccess);
        Assert.True(second.IsSuccess);
        Assert.DoesNotContain(first.Diagnostics, diagnostic => diagnostic.Source == "Viewer.Tessellation.TrimEvaluationFailed");

        var firstPatch = Assert.Single(first.Value.FacePatches);
        var secondPatch = Assert.Single(second.Value.FacePatches);
        Assert.NotEmpty(firstPatch.TriangleIndices);
        Assert.Equal(firstPatch.Positions, secondPatch.Positions);
        Assert.Equal(firstPatch.Normals, secondPatch.Normals);
        Assert.Equal(firstPatch.TriangleIndices, secondPatch.TriangleIndices);

        var outerMidU = (outerBounds.UMin + outerBounds.UMax) * 0.5d;
        Assert.All(GetTriangleUvCentroids(firstPatch, point => ProjectConeUv(cone, point, outerMidU)), centroid =>
        {
            Assert.True(IsInsideUvRectangle(centroid, outerBounds));
            Assert.False(IsInsideUvRectangle(centroid, holeBounds));
        });
    }

    [Fact]
    public void Tessellate_TrimmedCylinderSurfaceAcrossSeam_UsesDeterministicUnwrap()
    {
        var cylinder = new CylinderSurface(
            new Point3D(0d, 0d, 0d),
            Direction3D.Create(new Vector3D(0d, 0d, 1d)),
            2d,
            Direction3D.Create(new Vector3D(1d, 0d, 0d)));
        var outerBounds = (UMin: 5.8d, UMax: 6.7d, VMin: 0.6d, VMax: 3.2d);
        var body = CreateTrimmedCylinderSurfaceBody(cylinder, outerBounds, holeBounds: null);
        var options = DisplayTessellationOptions.Create(double.Pi / 8d, 0.15d, minimumSegments: 12, maximumSegments: 12).Value;

        var result = BrepDisplayTessellator.Tessellate(body, options);

        Assert.True(result.IsSuccess);
        Assert.DoesNotContain(result.Diagnostics, diagnostic => diagnostic.Source == "Viewer.Tessellation.TrimEvaluationFailed");

        var patch = Assert.Single(result.Value.FacePatches);
        Assert.NotEmpty(patch.TriangleIndices);

        var outerMidU = (outerBounds.UMin + outerBounds.UMax) * 0.5d;
        Assert.All(GetTriangleUvCentroids(patch, point => ProjectCylinderUv(cylinder, point, outerMidU)), centroid =>
            Assert.True(IsInsideUvRectangle(centroid, outerBounds)));
    }

    [Fact]
    public void Tessellate_TrimmedCylinderSurface_WhenProjectionFails_EmitsWarningAndSkipsWithoutFallback()
    {
        var cylinder = new CylinderSurface(
            new Point3D(0d, 0d, 0d),
            Direction3D.Create(new Vector3D(0d, 0d, 1d)),
            2d,
            Direction3D.Create(new Vector3D(1d, 0d, 0d)));
        var outerBounds = (UMin: 0.6d, UMax: 2.4d, VMin: 0.5d, VMax: 3.5d);
        var holeBounds = (UMin: 1.2d, UMax: 1.8d, VMin: 1.4d, VMax: 2.4d);
        var body = CreateTrimmedCylinderSurfaceBody(cylinder, outerBounds, holeBounds, topRadiusOffset: 0.35d);

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
    public void Tessellate_SimpleCylinderAndConeFaces_RemainSupported()
    {
        var cylinder = BrepPrimitives.CreateCylinder(1.5d, 4d);
        var cone = BrepRevolve.Create(
            [new ProfilePoint2D(1d, 0d), new ProfilePoint2D(2d, 3d)],
            DefaultFrame,
            DefaultAxis);

        var cylinderResult = BrepDisplayTessellator.Tessellate(cylinder.Value);
        var coneResult = BrepDisplayTessellator.Tessellate(cone.Value);

        Assert.True(cylinderResult.IsSuccess);
        Assert.True(coneResult.IsSuccess);
        Assert.Contains(cylinderResult.Value.FacePatches, patch => patch.TriangleIndices.Count > 0);
        Assert.Contains(coneResult.Value.FacePatches, patch => patch.TriangleIndices.Count > 0);
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

    private static BrepBody CreateTrimmedCylinderSurfaceBody(
        CylinderSurface cylinder,
        (double UMin, double UMax, double VMin, double VMax) outerBounds,
        (double UMin, double UMax, double VMin, double VMax)? holeBounds,
        double topRadiusOffset = 0d)
    {
        var builder = new TopologyBuilder();
        var geometry = new BrepGeometryStore();
        var bindings = new BrepBindingModel();
        var vertexPoints = new Dictionary<VertexId, Point3D>();
        var curveId = 1;
        var loops = new List<LoopId>
        {
            AddCylinderLoop(builder, geometry, bindings, vertexPoints, cylinder, outerBounds, ref curveId, topRadiusOffset),
        };

        if (holeBounds is { } hole)
        {
            loops.Add(AddCylinderLoop(builder, geometry, bindings, vertexPoints, cylinder, hole, ref curveId, topRadiusOffset: 0d));
        }

        var face = builder.AddFace(loops);
        var shell = builder.AddShell([face]);
        builder.AddBody([shell]);

        geometry.AddSurface(new SurfaceGeometryId(1), SurfaceGeometry.FromCylinder(cylinder));
        bindings.AddFaceBinding(new FaceGeometryBinding(face, new SurfaceGeometryId(1)));
        return new BrepBody(builder.Model, geometry, bindings, vertexPoints);
    }

    private static BrepBody CreateTrimmedConeSurfaceBody(
        ConeSurface cone,
        (double UMin, double UMax, double VMin, double VMax) outerBounds,
        (double UMin, double UMax, double VMin, double VMax)? holeBounds)
    {
        var builder = new TopologyBuilder();
        var geometry = new BrepGeometryStore();
        var bindings = new BrepBindingModel();
        var vertexPoints = new Dictionary<VertexId, Point3D>();
        var curveId = 1;
        var loops = new List<LoopId>
        {
            AddConeLoop(builder, geometry, bindings, vertexPoints, cone, outerBounds, ref curveId),
        };

        if (holeBounds is { } hole)
        {
            loops.Add(AddConeLoop(builder, geometry, bindings, vertexPoints, cone, hole, ref curveId));
        }

        var face = builder.AddFace(loops);
        var shell = builder.AddShell([face]);
        builder.AddBody([shell]);

        geometry.AddSurface(new SurfaceGeometryId(1), SurfaceGeometry.FromCone(cone));
        bindings.AddFaceBinding(new FaceGeometryBinding(face, new SurfaceGeometryId(1)));
        return new BrepBody(builder.Model, geometry, bindings, vertexPoints);
    }

    private static LoopId AddCylinderLoop(
        TopologyBuilder builder,
        BrepGeometryStore geometry,
        BrepBindingModel bindings,
        Dictionary<VertexId, Point3D> vertexPoints,
        CylinderSurface cylinder,
        (double UMin, double UMax, double VMin, double VMax) bounds,
        ref int curveId,
        double topRadiusOffset)
        => AddPeriodicRectangleLoop(
            builder,
            geometry,
            bindings,
            vertexPoints,
            bounds,
            ref curveId,
            (u, v) => EvaluateCylinderPoint(cylinder, u, v, v == bounds.VMax ? topRadiusOffset : 0d),
            v => CreateCircleAtCylinderV(cylinder, v, v == bounds.VMax ? topRadiusOffset : 0d));

    private static LoopId AddConeLoop(
        TopologyBuilder builder,
        BrepGeometryStore geometry,
        BrepBindingModel bindings,
        Dictionary<VertexId, Point3D> vertexPoints,
        ConeSurface cone,
        (double UMin, double UMax, double VMin, double VMax) bounds,
        ref int curveId)
        => AddPeriodicRectangleLoop(
            builder,
            geometry,
            bindings,
            vertexPoints,
            bounds,
            ref curveId,
            cone.Evaluate,
            v => CreateCircleAtConeV(cone, v));

    private static LoopId AddPeriodicRectangleLoop(
        TopologyBuilder builder,
        BrepGeometryStore geometry,
        BrepBindingModel bindings,
        Dictionary<VertexId, Point3D> vertexPoints,
        (double UMin, double UMax, double VMin, double VMax) bounds,
        ref int curveId,
        Func<double, double, Point3D> evaluate,
        Func<double, Circle3Curve> createCircleAtV)
    {
        var vertices = new[]
        {
            evaluate(bounds.UMin, bounds.VMin),
            evaluate(bounds.UMax, bounds.VMin),
            evaluate(bounds.UMax, bounds.VMax),
            evaluate(bounds.UMin, bounds.VMax),
        };

        var vertexIds = vertices
            .Select(point =>
            {
                var vertexId = builder.AddVertex();
                vertexPoints[vertexId] = point;
                return vertexId;
            })
            .ToArray();

        var loopId = builder.AllocateLoopId();
        var edgeIds = new[]
        {
            builder.AddEdge(vertexIds[0], vertexIds[1]),
            builder.AddEdge(vertexIds[1], vertexIds[2]),
            builder.AddEdge(vertexIds[2], vertexIds[3]),
            builder.AddEdge(vertexIds[3], vertexIds[0]),
        };

        geometry.AddCurve(new CurveGeometryId(curveId), CurveGeometry.FromCircle(createCircleAtV(bounds.VMin)));
        bindings.AddEdgeBinding(new EdgeGeometryBinding(edgeIds[0], new CurveGeometryId(curveId++), new ParameterInterval(bounds.UMin, bounds.UMax)));

        var rightLine = CreateLine(vertices[1], vertices[2], out var rightLength);
        geometry.AddCurve(new CurveGeometryId(curveId), CurveGeometry.FromLine(rightLine));
        bindings.AddEdgeBinding(new EdgeGeometryBinding(edgeIds[1], new CurveGeometryId(curveId++), new ParameterInterval(0d, rightLength)));

        geometry.AddCurve(new CurveGeometryId(curveId), CurveGeometry.FromCircle(createCircleAtV(bounds.VMax)));
        bindings.AddEdgeBinding(new EdgeGeometryBinding(edgeIds[2], new CurveGeometryId(curveId++), new ParameterInterval(System.Math.Min(bounds.UMin, bounds.UMax), System.Math.Max(bounds.UMin, bounds.UMax))));

        var leftLine = CreateLine(vertices[3], vertices[0], out var leftLength);
        geometry.AddCurve(new CurveGeometryId(curveId), CurveGeometry.FromLine(leftLine));
        bindings.AddEdgeBinding(new EdgeGeometryBinding(edgeIds[3], new CurveGeometryId(curveId++), new ParameterInterval(0d, leftLength)));

        var coedgeIds = new[]
        {
            builder.AllocateCoedgeId(),
            builder.AllocateCoedgeId(),
            builder.AllocateCoedgeId(),
            builder.AllocateCoedgeId(),
        };

        for (var i = 0; i < coedgeIds.Length; i++)
        {
            builder.AddCoedge(new Coedge(
                coedgeIds[i],
                edgeIds[i],
                loopId,
                coedgeIds[(i + 1) % coedgeIds.Length],
                coedgeIds[(i - 1 + coedgeIds.Length) % coedgeIds.Length],
                IsReversed: false));
        }

        builder.AddLoop(new Loop(loopId, coedgeIds));
        return loopId;
    }

    private static Point3D EvaluateCylinderPoint(CylinderSurface cylinder, double u, double v, double radiusOffset)
    {
        var radius = cylinder.Radius + radiusOffset;
        var radial = (cylinder.XAxis.ToVector() * (radius * System.Math.Cos(u))) + (cylinder.YAxis.ToVector() * (radius * System.Math.Sin(u)));
        return cylinder.Origin + (cylinder.Axis.ToVector() * v) + radial;
    }

    private static Circle3Curve CreateCircleAtCylinderV(CylinderSurface cylinder, double v, double radiusOffset)
        => new(
            cylinder.Origin + (cylinder.Axis.ToVector() * v),
            cylinder.Axis,
            cylinder.Radius + radiusOffset,
            cylinder.XAxis);

    private static Circle3Curve CreateCircleAtConeV(ConeSurface cone, double v)
        => new(
            cone.Apex + (cone.Axis.ToVector() * v),
            cone.Axis,
            v * System.Math.Tan(cone.SemiAngleRadians),
            cone.ReferenceAxis);

    private static IEnumerable<(double U, double V)> GetTriangleUvCentroids(
        DisplayFaceMeshPatch patch,
        Func<Point3D, (double U, double V)> projectPoint)
    {
        for (var i = 0; i < patch.TriangleIndices.Count; i += 3)
        {
            var a = projectPoint(patch.Positions[patch.TriangleIndices[i]]);
            var b = projectPoint(patch.Positions[patch.TriangleIndices[i + 1]]);
            var c = projectPoint(patch.Positions[patch.TriangleIndices[i + 2]]);
            yield return ((a.U + b.U + c.U) / 3d, (a.V + b.V + c.V) / 3d);
        }
    }

    private static (double U, double V) ProjectCylinderUv(CylinderSurface cylinder, Point3D point, double referenceU)
    {
        var offset = point - cylinder.Origin;
        var axial = offset.Dot(cylinder.Axis.ToVector());
        var radial = offset - (cylinder.Axis.ToVector() * axial);
        var angle = System.Math.Atan2(radial.Dot(cylinder.YAxis.ToVector()), radial.Dot(cylinder.XAxis.ToVector()));
        return (UnwrapNearReference(angle, referenceU), axial);
    }

    private static (double U, double V) ProjectConeUv(ConeSurface cone, Point3D point, double referenceU)
    {
        var offset = point - cone.Apex;
        var axial = offset.Dot(cone.Axis.ToVector());
        var radial = offset - (cone.Axis.ToVector() * axial);
        var xAxis = Direction3D.Create(cone.ReferenceAxis.ToVector() - (cone.Axis.ToVector() * cone.ReferenceAxis.ToVector().Dot(cone.Axis.ToVector()))).ToVector();
        var yAxis = Direction3D.Create(cone.Axis.ToVector().Cross(xAxis)).ToVector();
        var angle = radial.Length <= 1e-9d ? 0d : System.Math.Atan2(radial.Dot(yAxis), radial.Dot(xAxis));
        return (UnwrapNearReference(angle, referenceU), axial);
    }

    private static double UnwrapNearReference(double angle, double reference)
    {
        var candidate = angle;
        while (candidate < reference - double.Pi)
        {
            candidate += 2d * double.Pi;
        }

        while (candidate > reference + double.Pi)
        {
            candidate -= 2d * double.Pi;
        }

        return candidate;
    }

    private static bool IsInsideUvRectangle(
        (double U, double V) uv,
        (double UMin, double UMax, double VMin, double VMax) bounds)
        => uv.U >= bounds.UMin - 1e-6d
            && uv.U <= bounds.UMax + 1e-6d
            && uv.V >= bounds.VMin - 1e-6d
            && uv.V <= bounds.VMax + 1e-6d;
}
