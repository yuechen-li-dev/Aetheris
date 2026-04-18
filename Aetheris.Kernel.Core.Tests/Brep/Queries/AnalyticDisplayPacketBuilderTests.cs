using Aetheris.Kernel.Core.Brep;
using Aetheris.Kernel.Core.Brep.Boolean;
using Aetheris.Kernel.Core.Brep.Features;
using Aetheris.Kernel.Core.Brep.Queries;
using Aetheris.Kernel.Core.Geometry;
using Aetheris.Kernel.Core.Geometry.Curves;
using Aetheris.Kernel.Core.Geometry.Surfaces;
using Aetheris.Kernel.Core.Math;
using Aetheris.Kernel.Core.Step242;
using Aetheris.Kernel.Core.Tests.Step242;

namespace Aetheris.Kernel.Core.Tests.Brep.Queries;

public sealed class AnalyticDisplayPacketBuilderTests
{
    [Fact]
    public void Build_BoxBody_ProducesPlanarAnalyticEntriesOnly()
    {
        var body = BrepPrimitives.CreateBox(2d, 2d, 2d).Value;

        var packet = AnalyticDisplayPacketBuilder.Build(body);

        Assert.Equal(6, packet.AnalyticFaces.Count);
        Assert.Empty(packet.FallbackFaces);
        Assert.All(packet.AnalyticFaces, face => Assert.Equal(SurfaceGeometryKind.Plane, face.SurfaceKind));
    }

    [Fact]
    public void Build_BoxBody_PopulatesPlanarOuterBoundaryWithRealFaceExtent()
    {
        var body = BrepPrimitives.CreateBox(2d, 2d, 2d).Value;

        var packet = AnalyticDisplayPacketBuilder.Build(body);

        var planarFacesWithBoundary = packet.AnalyticFaces
            .Where(face => face.SurfaceKind == SurfaceGeometryKind.Plane && face.PlanarOuterBoundary is { Count: >= 3 })
            .ToArray();

        Assert.NotEmpty(planarFacesWithBoundary);

        var boundary = planarFacesWithBoundary[0].PlanarOuterBoundary!;
        Assert.Equal(4, boundary.Count);
        Assert.Equal(-1d, boundary.Min(vertex => vertex.X), 9);
        Assert.Equal(1d, boundary.Max(vertex => vertex.X), 9);
        Assert.Equal(-1d, boundary.Min(vertex => vertex.Y), 9);
        Assert.Equal(1d, boundary.Max(vertex => vertex.Y), 9);
    }


    [Fact]
    public void Build_RotatedPlanarPolygonBody_KeepsNonAxisPlanarPolygonFaceAnalytic()
    {
        var imported = ImportFromCorpus("testdata/step242/tessellation-robustness/planar-nonconvex-single-loop.step");
        var body = TransformBody(imported, Transform3D.CreateRotationX(double.Pi / 4d));

        var packet = AnalyticDisplayPacketBuilder.Build(body);

        Assert.Contains(packet.AnalyticFaces, face => face.SurfaceKind == SurfaceGeometryKind.Plane);
    }

    [Fact]
    public void Build_RotatedPlanarCurvedTrimBody_RoutesNonAxisUnsupportedPlanarTrimToFallback()
    {
        var imported = ImportFromCorpus("testdata/step242/tessellation-robustness/planar-rect-with-filleted-corners.step");
        var body = TransformBody(imported, Transform3D.CreateRotationX(double.Pi / 6d));

        var packet = AnalyticDisplayPacketBuilder.Build(body);

        Assert.Contains(packet.FallbackFaces, face => face.SurfaceKind == SurfaceGeometryKind.Plane && face.Reason == AnalyticDisplayFallbackReason.UnsupportedTrim);
    }

    [Fact]
    public void Build_BoxCylinderThroughHole_ProducesPlanarAndCylindricalAnalyticEntries()
    {
        var body = CreateBoxCylinderThroughHole();

        var packet = AnalyticDisplayPacketBuilder.Build(body);

        Assert.NotEmpty(packet.AnalyticFaces);
        Assert.Contains(packet.AnalyticFaces, face => face.SurfaceKind == SurfaceGeometryKind.Plane);
        Assert.Contains(packet.AnalyticFaces, face => face.SurfaceKind == SurfaceGeometryKind.Cylinder);
        Assert.Empty(packet.FallbackFaces);
    }

    [Fact]
    public void Build_BoxConeThroughHole_ProducesPlanarAndConicalAnalyticEntries()
    {
        var body = CreateBoxConeThroughHole();

        var packet = AnalyticDisplayPacketBuilder.Build(body);

        Assert.NotEmpty(packet.AnalyticFaces);
        Assert.Contains(packet.AnalyticFaces, face => face.SurfaceKind == SurfaceGeometryKind.Plane);
        Assert.Contains(packet.AnalyticFaces, face => face.SurfaceKind == SurfaceGeometryKind.Cone);
        Assert.Empty(packet.FallbackFaces);
    }

    [Fact]
    public void Build_BoxSphereCavity_ReflectsOuterAndInnerVoidShellRoles()
    {
        var body = CreateBoxSphereCavity();

        var packet = AnalyticDisplayPacketBuilder.Build(body);

        Assert.Contains(packet.AnalyticFaces, face => face.SurfaceKind == SurfaceGeometryKind.Plane && face.ShellRole == AnalyticDisplayShellRole.Outer);
        Assert.Contains(packet.AnalyticFaces, face => face.SurfaceKind == SurfaceGeometryKind.Sphere && face.ShellRole == AnalyticDisplayShellRole.InnerVoid);
        Assert.Empty(packet.FallbackFaces);
    }

    [Fact]
    public void Build_TrimmedSphereFace_RoutesToUnsupportedTrimFallback()
    {
        var body = CreateTrimmedSphereBody();

        var sphereFaceIds = body.Topology.Faces
            .Select(face => face.Id)
            .Where(faceId => body.GetFaceSurface(faceId).Kind == SurfaceGeometryKind.Sphere)
            .ToArray();
        Assert.NotEmpty(sphereFaceIds);
        Assert.All(sphereFaceIds, faceId => Assert.NotEmpty(body.Topology.GetFace(faceId).LoopIds));

        var packet = AnalyticDisplayPacketBuilder.Build(body);

        Assert.DoesNotContain(packet.AnalyticFaces, face => face.SurfaceKind == SurfaceGeometryKind.Sphere);
        Assert.Contains(packet.FallbackFaces, face => face.SurfaceKind == SurfaceGeometryKind.Sphere && face.Reason == AnalyticDisplayFallbackReason.UnsupportedTrim);
    }

    [Fact]
    public void Build_TrimmedTorusFace_RoutesToUnsupportedTrimFallback()
    {
        var body = CreateTrimmedTorusBody();

        var torusFaceIds = body.Topology.Faces
            .Select(face => face.Id)
            .Where(faceId => body.GetFaceSurface(faceId).Kind == SurfaceGeometryKind.Torus)
            .ToArray();
        Assert.NotEmpty(torusFaceIds);
        Assert.All(torusFaceIds, faceId => Assert.NotEmpty(body.Topology.GetFace(faceId).LoopIds));

        var packet = AnalyticDisplayPacketBuilder.Build(body);

        Assert.DoesNotContain(packet.AnalyticFaces, face => face.SurfaceKind == SurfaceGeometryKind.Torus);
        Assert.Contains(packet.FallbackFaces, face => face.SurfaceKind == SurfaceGeometryKind.Torus && face.Reason == AnalyticDisplayFallbackReason.UnsupportedTrim);
    }

    [Fact]
    public void Build_WholePrimitiveSphereAndTorus_RemainAnalytic()
    {
        var sphere = BrepPrimitives.CreateSphere(2d).Value;
        var spherePacket = AnalyticDisplayPacketBuilder.Build(sphere);
        Assert.Contains(spherePacket.AnalyticFaces, face => face.SurfaceKind == SurfaceGeometryKind.Sphere);
        Assert.DoesNotContain(spherePacket.FallbackFaces, face => face.SurfaceKind == SurfaceGeometryKind.Sphere);

        var torus = BrepPrimitives.CreateTorus(4d, 1d).Value;
        var torusPacket = AnalyticDisplayPacketBuilder.Build(torus);
        Assert.Contains(torusPacket.AnalyticFaces, face => face.SurfaceKind == SurfaceGeometryKind.Torus);
        Assert.DoesNotContain(torusPacket.FallbackFaces, face => face.SurfaceKind == SurfaceGeometryKind.Torus);
    }

    [Fact]
    public void Build_UnsupportedBsplineFace_RoutesDeterministicFallbackReason()
    {
        var body = CreateBoxWithUnsupportedBsplineFace();

        var packet = AnalyticDisplayPacketBuilder.Build(body);

        Assert.Single(packet.FallbackFaces);
        var fallback = packet.FallbackFaces[0];
        Assert.Equal(AnalyticDisplayFallbackReason.UnsupportedSurfaceKind, fallback.Reason);
        Assert.Equal(SurfaceGeometryKind.BSplineSurfaceWithKnots, fallback.SurfaceKind);
    }

    [Fact]
    public void Build_SameBodyTwice_ProducesDeterministicPacketContentAndOrdering()
    {
        var body = CreateBoxConeThroughHole();

        var first = AnalyticDisplayPacketBuilder.Build(body);
        var second = AnalyticDisplayPacketBuilder.Build(body);

        Assert.Equal(PacketSignature(first), PacketSignature(second));
    }

    [Fact]
    public void Build_SupportedNativeBody_DoesNotRequireSpatialOrTessellationLaneToClassify()
    {
        var body = CreateBoxCylinderThroughHole();
        var openingRay = new Ray3D(new Point3D(3d, -2d, 20d), Direction3D.Create(new Vector3D(0d, 0d, -1d)));

        var spatial = BrepSpatialQueries.Raycast(body, openingRay);
        var packet = AnalyticDisplayPacketBuilder.Build(body);

        Assert.False(spatial.IsSuccess);
        Assert.Contains(packet.AnalyticFaces, face => face.SurfaceKind == SurfaceGeometryKind.Cylinder);
        Assert.Empty(packet.FallbackFaces);
    }

    private static string PacketSignature(AnalyticDisplayPacket packet)
    {
        var analytic = string.Join("|", packet.AnalyticFaces.Select(face => $"{face.FaceId.Value}:{face.ShellId.Value}:{face.ShellRole}:{face.SurfaceKind}:{face.LoopCount}:{face.DomainHint?.MinV}:{face.DomainHint?.MaxV}"));
        var fallback = string.Join("|", packet.FallbackFaces.Select(face => $"{face.FaceId.Value}:{face.ShellId.Value}:{face.ShellRole}:{face.Reason}:{face.SurfaceKind}"));
        return $"{packet.BodyId.Value};A[{analytic}];F[{fallback}]";
    }

    private static BrepBody CreateBoxWithUnsupportedBsplineFace()
    {
        var box = BrepPrimitives.CreateBox(8d, 8d, 8d).Value;
        var firstFaceId = box.Topology.Faces.OrderBy(face => face.Id.Value).First().Id;
        var replacementSurfaceId = new SurfaceGeometryId(1000);

        var geometry = new BrepGeometryStore();
        foreach (var curve in box.Geometry.Curves)
        {
            geometry.AddCurve(curve.Key, curve.Value);
        }

        foreach (var surface in box.Geometry.Surfaces)
        {
            geometry.AddSurface(surface.Key, surface.Value);
        }

        geometry.AddSurface(replacementSurfaceId, SurfaceGeometry.FromBSplineSurfaceWithKnots(CreateBilinearBsplineSurface()));

        var bindings = new BrepBindingModel();
        foreach (var edgeBinding in box.Bindings.EdgeBindings)
        {
            bindings.AddEdgeBinding(edgeBinding);
        }

        foreach (var faceBinding in box.Bindings.FaceBindings.OrderBy(binding => binding.FaceId.Value))
        {
            var remapped = faceBinding.FaceId == firstFaceId
                ? new FaceGeometryBinding(faceBinding.FaceId, replacementSurfaceId)
                : faceBinding;
            bindings.AddFaceBinding(remapped);
        }

        return new BrepBody(box.Topology, geometry, bindings, vertexPoints: null, safeBooleanComposition: box.SafeBooleanComposition, shellRepresentation: box.ShellRepresentation);
    }

    private static BSplineSurfaceWithKnots CreateBilinearBsplineSurface()
    {
        return new BSplineSurfaceWithKnots(
            degreeU: 1,
            degreeV: 1,
            controlPoints:
            [
                [new Point3D(0d, 0d, 0d), new Point3D(0d, 10d, 0d)],
                [new Point3D(10d, 0d, 0d), new Point3D(10d, 10d, 0d)],
            ],
            surfaceForm: "PLANE_SURF",
            uClosed: false,
            vClosed: false,
            selfIntersect: false,
            knotMultiplicitiesU: [2, 2],
            knotMultiplicitiesV: [2, 2],
            knotValuesU: [0d, 1d],
            knotValuesV: [0d, 1d],
            knotSpec: "UNSPECIFIED");
    }

    private static BrepBody CreateBoxCylinderThroughHole()
    {
        var left = BrepBooleanBoxRecognition.CreateBoxFromExtents(new AxisAlignedBoxExtents(-20d, 20d, -15d, 15d, 0d, 12d)).Value;
        var right = TransformBody(BrepPrimitives.CreateCylinder(4d, 20d).Value, Transform3D.CreateTranslation(new Vector3D(3d, -2d, 6d)));
        var result = BrepBoolean.Subtract(left, right);
        Assert.True(result.IsSuccess);
        return result.Value;
    }

    private static BrepBody CreateBoxConeThroughHole()
    {
        var left = BrepBooleanBoxRecognition.CreateBoxFromExtents(new AxisAlignedBoxExtents(-12d, 12d, -10d, 10d, 0d, 10d)).Value;
        var right = CreateCone(bottomRadius: 6d, topRadius: 0d, height: 20d);
        var result = BrepBoolean.Subtract(left, right);
        Assert.True(result.IsSuccess);
        return result.Value;
    }

    private static BrepBody CreateBoxSphereCavity()
    {
        var left = BrepBooleanBoxRecognition.CreateBoxFromExtents(new AxisAlignedBoxExtents(-10d, 10d, -10d, 10d, -10d, 10d)).Value;
        var right = TransformBody(BrepPrimitives.CreateSphere(6d).Value, Transform3D.CreateTranslation(new Vector3D(0d, 0d, 0d)));
        var result = BrepBoolean.Subtract(left, right);
        Assert.True(result.IsSuccess);
        return result.Value;
    }

    private static BrepBody CreateTrimmedSphereBody()
    {
        return ImportFromCorpus("testdata/step242/nist/STC/nist_stc_06_asme1_ap242-e3.stp");
    }

    private static BrepBody CreateTrimmedTorusBody()
    {
        return ImportFromCorpus("testdata/step242/nist/FTC/nist_ftc_11_asme1_ap242-e2.stp");
    }

    private static BrepBody ImportFromCorpus(string relativePath)
    {
        var absolutePath = Path.Combine(Step242CorpusManifestRunner.RepoRoot(), relativePath.Replace('/', Path.DirectorySeparatorChar));
        var text = File.ReadAllText(absolutePath);
        var import = Step242Importer.ImportBody(text);
        Assert.True(import.IsSuccess);
        return import.Value;
    }

    private static BrepBody CreateCone(double bottomRadius, double topRadius, double height)
    {
        var frame = new ExtrudeFrame3D(
            origin: Point3D.Origin,
            normal: Direction3D.Create(new Vector3D(0d, 0d, 1d)),
            uAxis: Direction3D.Create(new Vector3D(1d, 0d, 0d)));
        var axis = new RevolveAxis3D(Point3D.Origin, new Vector3D(0d, 0d, 1d));

        var result = BrepRevolve.Create(
            [
                new ProfilePoint2D(bottomRadius, 0d),
                new ProfilePoint2D(topRadius, height)
            ],
            frame,
            axis);

        Assert.True(result.IsSuccess);
        return result.Value;
    }

    private static BrepBody TransformBody(BrepBody body, Transform3D transform)
    {
        var geometry = new BrepGeometryStore();
        foreach (var curveEntry in body.Geometry.Curves)
        {
            geometry.AddCurve(curveEntry.Key, curveEntry.Value.Kind switch
            {
                CurveGeometryKind.Line3 => CurveGeometry.FromLine(new Line3Curve(
                    transform.Apply(curveEntry.Value.Line3!.Value.Origin),
                    transform.Apply(curveEntry.Value.Line3.Value.Direction))),
                CurveGeometryKind.Circle3 => CurveGeometry.FromCircle(new Circle3Curve(
                    transform.Apply(curveEntry.Value.Circle3!.Value.Center),
                    transform.Apply(curveEntry.Value.Circle3.Value.Normal),
                    curveEntry.Value.Circle3.Value.Radius,
                    transform.Apply(curveEntry.Value.Circle3.Value.XAxis))),
                _ => curveEntry.Value
            });
        }

        foreach (var surfaceEntry in body.Geometry.Surfaces)
        {
            geometry.AddSurface(surfaceEntry.Key, surfaceEntry.Value.Kind switch
            {
                SurfaceGeometryKind.Plane => SurfaceGeometry.FromPlane(new PlaneSurface(
                    transform.Apply(surfaceEntry.Value.Plane!.Value.Origin),
                    transform.Apply(surfaceEntry.Value.Plane.Value.Normal),
                    transform.Apply(surfaceEntry.Value.Plane.Value.UAxis))),
                SurfaceGeometryKind.Cylinder => SurfaceGeometry.FromCylinder(new CylinderSurface(
                    transform.Apply(surfaceEntry.Value.Cylinder!.Value.Origin),
                    transform.Apply(surfaceEntry.Value.Cylinder.Value.Axis),
                    surfaceEntry.Value.Cylinder.Value.Radius,
                    transform.Apply(surfaceEntry.Value.Cylinder.Value.XAxis))),
                SurfaceGeometryKind.Cone => SurfaceGeometry.FromCone(new ConeSurface(
                    transform.Apply(surfaceEntry.Value.Cone!.Value.PlacementOrigin),
                    transform.Apply(surfaceEntry.Value.Cone.Value.Axis),
                    surfaceEntry.Value.Cone.Value.PlacementRadius,
                    surfaceEntry.Value.Cone.Value.SemiAngleRadians,
                    transform.Apply(surfaceEntry.Value.Cone.Value.ReferenceAxis))),
                SurfaceGeometryKind.Torus => SurfaceGeometry.FromTorus(new TorusSurface(
                    transform.Apply(surfaceEntry.Value.Torus!.Value.Center),
                    transform.Apply(surfaceEntry.Value.Torus.Value.Axis),
                    surfaceEntry.Value.Torus.Value.MajorRadius,
                    surfaceEntry.Value.Torus.Value.MinorRadius,
                    transform.Apply(surfaceEntry.Value.Torus.Value.XAxis))),
                SurfaceGeometryKind.Sphere => SurfaceGeometry.FromSphere(new SphereSurface(
                    transform.Apply(surfaceEntry.Value.Sphere!.Value.Center),
                    transform.Apply(surfaceEntry.Value.Sphere.Value.Axis),
                    surfaceEntry.Value.Sphere.Value.Radius,
                    transform.Apply(surfaceEntry.Value.Sphere.Value.XAxis))),
                _ => surfaceEntry.Value
            });
        }

        var points = new Dictionary<Aetheris.Kernel.Core.Topology.VertexId, Point3D>();
        foreach (var vertex in body.Topology.Vertices)
        {
            if (body.TryGetVertexPoint(vertex.Id, out var point))
            {
                points[vertex.Id] = transform.Apply(point);
            }
        }

        return new BrepBody(body.Topology, geometry, body.Bindings, points, body.SafeBooleanComposition, body.ShellRepresentation);
    }
}
