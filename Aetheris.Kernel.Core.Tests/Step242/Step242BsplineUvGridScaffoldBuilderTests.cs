using Aetheris.Kernel.Core.Brep;
using Aetheris.Kernel.Core.Brep.Tessellation;
using Aetheris.Kernel.Core.Geometry;
using Aetheris.Kernel.Core.Geometry.Curves;
using Aetheris.Kernel.Core.Geometry.Surfaces;
using Aetheris.Kernel.Core.Math;
using Aetheris.Kernel.Core.Step242;
using Aetheris.Kernel.Core.Topology;

namespace Aetheris.Kernel.Core.Tests.Step242;

public sealed class Step242BsplineUvGridScaffoldBuilderTests
{
    private static readonly BsplineUvGridScaffoldBuilder Builder = new();

    [Fact]
    public void M18_M17h_RectangularFace_IsAccepted_AndDeterministic()
    {
        var body = ImportRepresentativeBsplineBody();
        var face = Assert.Single(body.Topology.Faces);
        var surface = GetBsplineSurface(body, face.Id);
        var referencePatch = GetReferencePatch(body, face.Id);

        var request = new BsplineUvGridScaffoldBuildRequest(
            USegments: 12,
            VSegments: 12,
            ReferencePositions: referencePatch.Positions,
            ReferenceTriangleCount: referencePatch.TriangleIndices.Count / 3,
            MaxFidelityError: 0.08d,
            MaxTriangleDensityRatioVsFallback: 1.10d);

        var first = Builder.Build(surface, request);
        var second = Builder.Build(surface, request);

        Assert.Equal(BsplineUvGridScaffoldAcceptance.Accepted, first.Acceptance);
        Assert.Equal(BsplineUvGridScaffoldRejectionReason.None, first.RejectionReason);
        Assert.NotNull(first.Mesh);
        Assert.Equal(first.Mesh!.Positions, second.Mesh!.Positions);
        Assert.Equal(first.Mesh.TriangleIndices, second.Mesh.TriangleIndices);
        Assert.Equal(first.Acceptance, second.Acceptance);
        Assert.Equal(first.RejectionReason, second.RejectionReason);
    }

    [Fact]
    public void M18_M17h2_CurvedRectangularFace_IsAccepted()
    {
        var body = CreateHarderCurvedRectangularBsplineBody();
        var face = Assert.Single(body.Topology.Faces);
        var surface = GetBsplineSurface(body, face.Id);
        var referencePatch = GetReferencePatch(body, face.Id);

        var result = Builder.Build(
            surface,
            new BsplineUvGridScaffoldBuildRequest(
                USegments: 16,
                VSegments: 16,
                ReferencePositions: referencePatch.Positions,
                ReferenceTriangleCount: referencePatch.TriangleIndices.Count / 3,
                MaxFidelityError: 0.08d,
                MaxTriangleDensityRatioVsFallback: 1.10d));

        Assert.Equal(BsplineUvGridScaffoldAcceptance.Accepted, result.Acceptance);
        Assert.Equal(BsplineUvGridScaffoldRejectionReason.None, result.RejectionReason);
    }

    [Fact]
    public void M18_M17h3_InnerHoleMaskedCase_IsAccepted_WithoutLeakage()
    {
        var body = CreateTrimmedPlanarBsplineBodyWithRectangularHole(out var outerLoopUv, out var innerHoleUv);
        var face = Assert.Single(body.Topology.Faces);
        var surface = GetBsplineSurface(body, face.Id);
        var referencePatch = GetReferencePatch(body, face.Id);

        var trimMask = new UvTrimMask(outerLoopUv, [innerHoleUv]);

        var result = Builder.Build(
            surface,
            new BsplineUvGridScaffoldBuildRequest(
                USegments: 20,
                VSegments: 20,
                TrimMask: trimMask,
                ReferencePositions: referencePatch.Positions,
                ReferenceTriangleCount: referencePatch.TriangleIndices.Count / 3,
                MaxFidelityError: 0.10d,
                MaxBoundaryDeviationUv: 0.051d,
                MaxTriangleDensityRatioVsFallback: 1.10d));

        Assert.Equal(BsplineUvGridScaffoldAcceptance.Accepted, result.Acceptance);
        Assert.Equal(BsplineUvGridScaffoldRejectionReason.None, result.RejectionReason);
        Assert.Equal(0, result.Metrics.LeakageTriangleCount);
    }

    [Fact]
    public void M18_Rejects_WhenTooDenseVsFallback_AndProvidesReason()
    {
        var body = ImportRepresentativeBsplineBody();
        var face = Assert.Single(body.Topology.Faces);
        var surface = GetBsplineSurface(body, face.Id);
        var referencePatch = GetReferencePatch(body, face.Id);

        var accepted = Builder.Build(
            surface,
            new BsplineUvGridScaffoldBuildRequest(
                USegments: 12,
                VSegments: 12,
                ReferencePositions: referencePatch.Positions,
                ReferenceTriangleCount: referencePatch.TriangleIndices.Count / 3,
                MaxFidelityError: 0.08d,
                MaxTriangleDensityRatioVsFallback: 1.10d));

        var rejected = Builder.Build(
            surface,
            new BsplineUvGridScaffoldBuildRequest(
                USegments: 12,
                VSegments: 12,
                ReferencePositions: referencePatch.Positions,
                ReferenceTriangleCount: referencePatch.TriangleIndices.Count / 3,
                MaxFidelityError: 0.08d,
                MaxTriangleDensityRatioVsFallback: 0.10d));

        Assert.Equal(BsplineUvGridScaffoldAcceptance.Accepted, accepted.Acceptance);
        Assert.Equal(BsplineUvGridScaffoldAcceptance.Rejected, rejected.Acceptance);
        Assert.Equal(BsplineUvGridScaffoldRejectionReason.TooDenseVsFallback, rejected.RejectionReason);
    }

    private static DisplayFaceMeshPatch GetReferencePatch(BrepBody body, FaceId faceId)
    {
        var tessellation = BrepDisplayTessellator.Tessellate(body);
        Assert.True(tessellation.IsSuccess);
        return Assert.Single(tessellation.Value.FacePatches, patch => patch.FaceId == faceId);
    }

    private static BrepBody ImportRepresentativeBsplineBody()
    {
        const string text = "ISO-10303-21;\nHEADER;\nENDSEC;\nDATA;\n#1=MANIFOLD_SOLID_BREP('s',#2);\n#2=CLOSED_SHELL($,(#3));\n#3=ADVANCED_FACE((#4),#5,.T.);\n#4=FACE_OUTER_BOUND($,#6,.T.);\n#5=B_SPLINE_SURFACE_WITH_KNOTS('',3,3,((#100,#101,#102,#103),(#104,#105,#106,#107),(#108,#109,#110,#111),(#112,#113,#114,#115)),.UNSPECIFIED.,.F.,.F.,.F.,(4,4),(4,4),(0.,1.),(0.,1.),.UNSPECIFIED.);\n#6=EDGE_LOOP($,(#10,#11,#12,#13));\n#10=ORIENTED_EDGE($,$,$,#20,.T.);\n#11=ORIENTED_EDGE($,$,$,#21,.T.);\n#12=ORIENTED_EDGE($,$,$,#22,.T.);\n#13=ORIENTED_EDGE($,$,$,#23,.T.);\n#20=EDGE_CURVE($,#30,#31,#40,.T.);\n#21=EDGE_CURVE($,#31,#32,#41,.T.);\n#22=EDGE_CURVE($,#32,#33,#42,.T.);\n#23=EDGE_CURVE($,#33,#30,#43,.T.);\n#30=VERTEX_POINT($,#50);\n#31=VERTEX_POINT($,#51);\n#32=VERTEX_POINT($,#52);\n#33=VERTEX_POINT($,#53);\n#40=LINE($,#50,#80);\n#41=LINE($,#51,#81);\n#42=LINE($,#52,#82);\n#43=LINE($,#53,#83);\n#50=CARTESIAN_POINT($,(0,0,0));\n#51=CARTESIAN_POINT($,(1,0,0));\n#52=CARTESIAN_POINT($,(1,1,0));\n#53=CARTESIAN_POINT($,(0,1,0));\n#80=VECTOR($,#84,1.0);\n#81=VECTOR($,#85,1.0);\n#82=VECTOR($,#86,1.0);\n#83=VECTOR($,#87,1.0);\n#84=DIRECTION($,(1,0,0));\n#85=DIRECTION($,(0,1,0));\n#86=DIRECTION($,(-1,0,0));\n#87=DIRECTION($,(0,-1,0));\n#100=CARTESIAN_POINT($,(0,0,0));\n#101=CARTESIAN_POINT($,(0,0.33,0));\n#102=CARTESIAN_POINT($,(0,0.66,0));\n#103=CARTESIAN_POINT($,(0,1,0));\n#104=CARTESIAN_POINT($,(0.33,0,0));\n#105=CARTESIAN_POINT($,(0.33,0.33,0));\n#106=CARTESIAN_POINT($,(0.33,0.66,0));\n#107=CARTESIAN_POINT($,(0.33,1,0));\n#108=CARTESIAN_POINT($,(0.66,0,0));\n#109=CARTESIAN_POINT($,(0.66,0.33,0));\n#110=CARTESIAN_POINT($,(0.66,0.66,0));\n#111=CARTESIAN_POINT($,(0.66,1,0));\n#112=CARTESIAN_POINT($,(1,0,0));\n#113=CARTESIAN_POINT($,(1,0.33,0));\n#114=CARTESIAN_POINT($,(1,0.66,0));\n#115=CARTESIAN_POINT($,(1,1,0));\nENDSEC;\nEND-ISO-10303-21;";
        var import = Step242Importer.ImportBody(text);
        Assert.True(import.IsSuccess);
        return import.Value;
    }

    private static BSplineSurfaceWithKnots GetBsplineSurface(BrepBody body, FaceId faceId)
    {
        Assert.True(body.TryGetFaceSurfaceGeometry(faceId, out var surface));
        Assert.NotNull(surface);
        Assert.Equal(SurfaceGeometryKind.BSplineSurfaceWithKnots, surface!.Kind);
        return Assert.IsType<BSplineSurfaceWithKnots>(surface.BSplineSurfaceWithKnots);
    }

    private static BrepBody CreateHarderCurvedRectangularBsplineBody()
    {
        IReadOnlyList<UvPoint> outerLoop = [new(0d, 0d), new(1d, 0d), new(1d, 1d), new(0d, 1d)];
        var surface = CreateCurvedBsplineSurface();
        return CreateFaceBody(surface, outerLoop, []);
    }

    private static BrepBody CreateTrimmedPlanarBsplineBodyWithRectangularHole(
        out IReadOnlyList<UvPoint> outerLoopUv,
        out IReadOnlyList<UvPoint> innerHoleUv)
    {
        outerLoopUv = [new(0d, 0d), new(1d, 0d), new(1d, 1d), new(0d, 1d)];
        innerHoleUv = [new(0.35d, 0.35d), new(0.65d, 0.35d), new(0.65d, 0.65d), new(0.35d, 0.65d)];
        return CreateFaceBody(CreatePlanarBsplineSurface(), outerLoopUv, [innerHoleUv]);
    }

    private static BrepBody CreateFaceBody(BSplineSurfaceWithKnots surface, IReadOnlyList<UvPoint> outerLoop, IReadOnlyList<IReadOnlyList<UvPoint>> innerLoops)
    {
        var builder = new TopologyBuilder();
        var geometry = new BrepGeometryStore();
        var bindings = new BrepBindingModel();
        var vertexPoints = new Dictionary<VertexId, Point3D>();
        var curveId = 1;

        var loopIds = new List<LoopId>
        {
            AddUvPolygonLoop(outerLoop, surface, builder, geometry, bindings, vertexPoints, ref curveId)
        };

        foreach (var innerLoop in innerLoops)
        {
            loopIds.Add(AddUvPolygonLoop(innerLoop, surface, builder, geometry, bindings, vertexPoints, ref curveId));
        }

        var faceId = builder.AddFace(loopIds);
        var shell = builder.AddShell([faceId]);
        builder.AddBody([shell]);

        geometry.AddSurface(new SurfaceGeometryId(1), SurfaceGeometry.FromBSplineSurfaceWithKnots(surface));
        bindings.AddFaceBinding(new FaceGeometryBinding(faceId, new SurfaceGeometryId(1)));

        return new BrepBody(builder.Model, geometry, bindings, vertexPoints);
    }

    private static LoopId AddUvPolygonLoop(
        IReadOnlyList<UvPoint> uvPolygon,
        BSplineSurfaceWithKnots surface,
        TopologyBuilder builder,
        BrepGeometryStore geometry,
        BrepBindingModel bindings,
        IDictionary<VertexId, Point3D> vertexPoints,
        ref int nextCurveId)
    {
        var loopId = builder.AllocateLoopId();
        var edgeIds = new List<EdgeId>(uvPolygon.Count);
        var coedgeIds = new List<CoedgeId>(uvPolygon.Count);

        var vertices = uvPolygon.Select(uv => surface.Evaluate(uv.U, uv.V)).ToArray();
        var vertexIds = vertices.Select(point =>
        {
            var vertexId = builder.AddVertex();
            vertexPoints[vertexId] = point;
            return vertexId;
        }).ToArray();

        for (var i = 0; i < uvPolygon.Count; i++)
        {
            var startVertex = vertexIds[i];
            var endVertex = vertexIds[(i + 1) % uvPolygon.Count];
            var edgeId = builder.AddEdge(startVertex, endVertex);
            edgeIds.Add(edgeId);

            var line = CreateLine(vertices[i], vertices[(i + 1) % uvPolygon.Count], out var length);
            var geometryId = new CurveGeometryId(nextCurveId++);
            geometry.AddCurve(geometryId, CurveGeometry.FromLine(line));
            bindings.AddEdgeBinding(new EdgeGeometryBinding(edgeId, geometryId, new ParameterInterval(0d, length)));

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

    private static BSplineSurfaceWithKnots CreateCurvedBsplineSurface()
        => new(
            degreeU: 3,
            degreeV: 3,
            controlPoints:
            [
                [new Point3D(0.00d, 0.00d, 0.00d), new Point3D(0.00d, 0.33d, 0.28d), new Point3D(0.00d, 0.66d, -0.22d), new Point3D(0.00d, 1.00d, 0.10d)],
                [new Point3D(0.33d, 0.00d, 0.42d), new Point3D(0.33d, 0.33d, 0.62d), new Point3D(0.33d, 0.66d, 0.18d), new Point3D(0.33d, 1.00d, -0.10d)],
                [new Point3D(0.66d, 0.00d, -0.20d), new Point3D(0.66d, 0.33d, 0.20d), new Point3D(0.66d, 0.66d, 0.72d), new Point3D(0.66d, 1.00d, 0.34d)],
                [new Point3D(1.00d, 0.00d, 0.12d), new Point3D(1.00d, 0.33d, -0.30d), new Point3D(1.00d, 0.66d, 0.24d), new Point3D(1.00d, 1.00d, 0.52d)],
            ],
            surfaceForm: "UNSPECIFIED",
            uClosed: false,
            vClosed: false,
            selfIntersect: false,
            knotMultiplicitiesU: [4, 4],
            knotMultiplicitiesV: [4, 4],
            knotValuesU: [0d, 1d],
            knotValuesV: [0d, 1d],
            knotSpec: "UNSPECIFIED");

    private static BSplineSurfaceWithKnots CreatePlanarBsplineSurface()
        => new(
            degreeU: 3,
            degreeV: 3,
            controlPoints:
            [
                [new Point3D(0.00d, 0.00d, 0.00d), new Point3D(0.00d, 0.33d, 0.00d), new Point3D(0.00d, 0.66d, 0.00d), new Point3D(0.00d, 1.00d, 0.00d)],
                [new Point3D(0.33d, 0.00d, 0.00d), new Point3D(0.33d, 0.33d, 0.00d), new Point3D(0.33d, 0.66d, 0.00d), new Point3D(0.33d, 1.00d, 0.00d)],
                [new Point3D(0.66d, 0.00d, 0.00d), new Point3D(0.66d, 0.33d, 0.00d), new Point3D(0.66d, 0.66d, 0.00d), new Point3D(0.66d, 1.00d, 0.00d)],
                [new Point3D(1.00d, 0.00d, 0.00d), new Point3D(1.00d, 0.33d, 0.00d), new Point3D(1.00d, 0.66d, 0.00d), new Point3D(1.00d, 1.00d, 0.00d)],
            ],
            surfaceForm: "UNSPECIFIED",
            uClosed: false,
            vClosed: false,
            selfIntersect: false,
            knotMultiplicitiesU: [4, 4],
            knotMultiplicitiesV: [4, 4],
            knotValuesU: [0d, 1d],
            knotValuesV: [0d, 1d],
            knotSpec: "UNSPECIFIED");
}
