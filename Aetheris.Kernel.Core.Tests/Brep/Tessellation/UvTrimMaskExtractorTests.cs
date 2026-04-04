using Aetheris.Kernel.Core.Brep;
using Aetheris.Kernel.Core.Brep.Tessellation;
using Aetheris.Kernel.Core.Geometry.Curves;
using Aetheris.Kernel.Core.Geometry.Surfaces;
using Aetheris.Kernel.Core.Geometry;
using Aetheris.Kernel.Core.Math;
using Aetheris.Kernel.Core.Step242;
using Aetheris.Kernel.Core.Topology;

namespace Aetheris.Kernel.Core.Tests.Brep.Tessellation;

public sealed class UvTrimMaskExtractorTests
{
    [Fact]
    public void TryExtract_RealBsplineFace_BuildsDeterministicTrimMaskAndSupportsScaffold()
    {
        var body = ImportSingleLoopBsplineBody();
        var face = Assert.Single(body.Topology.Faces);
        Assert.True(body.TryGetFaceSurfaceGeometry(face.Id, out var surface));
        var bspline = surface?.BSplineSurfaceWithKnots;
        Assert.NotNull(bspline);

        var extractor = new UvTrimMaskExtractor();
        var first = extractor.TryExtract(body, face.Id, bspline!);
        var second = extractor.TryExtract(body, face.Id, bspline!);

        Assert.True(first.IsSuccess);
        Assert.True(second.IsSuccess);
        Assert.NotNull(first.TrimMask);
        Assert.NotNull(second.TrimMask);
        Assert.Equal(first.TrimMask!.OuterLoop, second.TrimMask!.OuterLoop);

        var fallback = DisplayPreparationFallbackBuilder.Build(body);
        Assert.True(fallback.IsSuccess);
        var existingPatch = Assert.Single(fallback.Value.FacePatches);

        var builder = new BsplineUvGridScaffoldBuilder();
        var scaffold = builder.Build(
            bspline!,
            new BsplineUvGridScaffoldBuildRequest(
                USegments: 12,
                VSegments: 12,
                TrimMask: first.TrimMask,
                ReferencePositions: existingPatch.Positions,
                ReferenceTriangleCount: existingPatch.TriangleIndices.Count / 3,
                Acceptance: BsplineUvGridScaffoldAcceptanceThresholds.ConservativeDefaults));

        Assert.Equal(BsplineUvGridScaffoldAcceptance.Accepted, scaffold.Acceptance);
        Assert.Equal(BsplineUvGridScaffoldRejectionReason.None, scaffold.RejectionReason);
    }

    [Fact]
    public void TryExtract_RealBsplineFaceWithHole_BuildsDeterministicOuterAndInnerLoops()
    {
        var body = ImportBsplineBodyWithHole();
        var face = Assert.Single(body.Topology.Faces);
        Assert.True(body.TryGetFaceSurfaceGeometry(face.Id, out var surface));
        var bspline = surface?.BSplineSurfaceWithKnots;
        Assert.NotNull(bspline);

        var extractor = new UvTrimMaskExtractor();
        var first = extractor.TryExtract(body, face.Id, bspline!);
        var second = extractor.TryExtract(body, face.Id, bspline!);

        Assert.True(first.IsSuccess);
        Assert.True(second.IsSuccess);
        Assert.NotNull(first.TrimMask);
        Assert.NotNull(second.TrimMask);
        Assert.Equal(first.TrimMask!.OuterLoop, second.TrimMask!.OuterLoop);
        Assert.Equal(first.TrimMask.InnerLoops.Count, second.TrimMask.InnerLoops.Count);
        Assert.Single(first.TrimMask.InnerLoops);
        Assert.Equal(first.TrimMask.InnerLoops[0], second.TrimMask.InnerLoops[0]);
    }

    [Fact]
    public void TryExtract_BsplineFaceWithUnsupportedTrimEdge_IsExplicitlyUnsupported()
    {
        var body = CreateBodyWithUnsupportedTrimEdge(ImportSingleLoopBsplineBody());
        var face = Assert.Single(body.Topology.Faces);
        Assert.True(body.TryGetFaceSurfaceGeometry(face.Id, out var surface));
        var bspline = surface?.BSplineSurfaceWithKnots;
        Assert.NotNull(bspline);

        var extractor = new UvTrimMaskExtractor();
        var result = extractor.TryExtract(body, face.Id, bspline!);

        Assert.False(result.IsSuccess);
        Assert.Equal(UvTrimMaskExtractionFailureReason.UnsupportedEdgeGeometry, result.FailureReason);
        Assert.Null(result.TrimMask);
    }

    internal static BrepBody ImportSingleLoopBsplineBody()
    {
        const string text = "ISO-10303-21;\nHEADER;\nENDSEC;\nDATA;\n#1=MANIFOLD_SOLID_BREP('s',#2);\n#2=CLOSED_SHELL($,(#3));\n#3=ADVANCED_FACE((#4),#5,.T.);\n#4=FACE_OUTER_BOUND($,#6,.T.);\n#5=B_SPLINE_SURFACE_WITH_KNOTS('',3,3,((#100,#101,#102,#103),(#104,#105,#106,#107),(#108,#109,#110,#111),(#112,#113,#114,#115)),.UNSPECIFIED.,.F.,.F.,.F.,(4,4),(4,4),(0.,1.),(0.,1.),.UNSPECIFIED.);\n#6=EDGE_LOOP($,(#10,#11,#12,#13));\n#10=ORIENTED_EDGE($,$,$,#20,.T.);\n#11=ORIENTED_EDGE($,$,$,#21,.T.);\n#12=ORIENTED_EDGE($,$,$,#22,.T.);\n#13=ORIENTED_EDGE($,$,$,#23,.T.);\n#20=EDGE_CURVE($,#30,#31,#40,.T.);\n#21=EDGE_CURVE($,#31,#32,#41,.T.);\n#22=EDGE_CURVE($,#32,#33,#42,.T.);\n#23=EDGE_CURVE($,#33,#30,#43,.T.);\n#30=VERTEX_POINT($,#50);\n#31=VERTEX_POINT($,#51);\n#32=VERTEX_POINT($,#52);\n#33=VERTEX_POINT($,#53);\n#40=LINE($,#50,#80);\n#41=LINE($,#51,#81);\n#42=LINE($,#52,#82);\n#43=LINE($,#53,#83);\n#50=CARTESIAN_POINT($,(0,0,0));\n#51=CARTESIAN_POINT($,(1,0,0));\n#52=CARTESIAN_POINT($,(1,1,0));\n#53=CARTESIAN_POINT($,(0,1,0));\n#80=VECTOR($,#84,1.0);\n#81=VECTOR($,#85,1.0);\n#82=VECTOR($,#86,1.0);\n#83=VECTOR($,#87,1.0);\n#84=DIRECTION($,(1,0,0));\n#85=DIRECTION($,(0,1,0));\n#86=DIRECTION($,(-1,0,0));\n#87=DIRECTION($,(0,-1,0));\n#100=CARTESIAN_POINT($,(0,0,0));\n#101=CARTESIAN_POINT($,(0,0.33,0));\n#102=CARTESIAN_POINT($,(0,0.66,0));\n#103=CARTESIAN_POINT($,(0,1,0));\n#104=CARTESIAN_POINT($,(0.33,0,0));\n#105=CARTESIAN_POINT($,(0.33,0.33,0));\n#106=CARTESIAN_POINT($,(0.33,0.66,0));\n#107=CARTESIAN_POINT($,(0.33,1,0));\n#108=CARTESIAN_POINT($,(0.66,0,0));\n#109=CARTESIAN_POINT($,(0.66,0.33,0));\n#110=CARTESIAN_POINT($,(0.66,0.66,0));\n#111=CARTESIAN_POINT($,(0.66,1,0));\n#112=CARTESIAN_POINT($,(1,0,0));\n#113=CARTESIAN_POINT($,(1,0.33,0));\n#114=CARTESIAN_POINT($,(1,0.66,0));\n#115=CARTESIAN_POINT($,(1,1,0));\nENDSEC;\nEND-ISO-10303-21;";
        var import = Step242Importer.ImportBody(text);
        Assert.True(import.IsSuccess);
        return import.Value;
    }

    internal static BrepBody ImportBsplineBodyWithHole()
    {
        return CreateTrimmedBsplineSurfaceBody(
            outerBounds: (0d, 1d, 0d, 1d),
            holeBounds: (0.3d, 0.7d, 0.3d, 0.7d));
    }

    private static BrepBody CreateTrimmedBsplineSurfaceBody(
        (double UMin, double UMax, double VMin, double VMax) outerBounds,
        (double UMin, double UMax, double VMin, double VMax)? holeBounds)
    {
        var loops = new List<IReadOnlyList<(double U, double V)>> { CreateRectangleLoop(outerBounds) };
        if (holeBounds is { } hole)
        {
            loops.Add(CreateRectangleLoop(hole));
        }

        var builder = new TopologyBuilder();
        var geometry = new BrepGeometryStore();
        var bindings = new BrepBindingModel();
        var vertexPoints = new Dictionary<VertexId, Point3D>();
        var faceLoops = new List<LoopId>(loops.Count);
        var curveId = 1;

        foreach (var loop in loops)
        {
            faceLoops.Add(AddLoop(builder, geometry, bindings, vertexPoints, CreateBsplineLoopVertices(loop), ref curveId));
        }

        var face = builder.AddFace(faceLoops);
        var shell = builder.AddShell([face]);
        builder.AddBody([shell]);

        geometry.AddSurface(new SurfaceGeometryId(1), SurfaceGeometry.FromBSplineSurfaceWithKnots(CreateBilinearBsplineSurface()));
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
        var vertexIds = vertices.Select(point =>
        {
            var vertexId = builder.AddVertex();
            vertexPoints[vertexId] = point;
            return vertexId;
        }).ToArray();

        var loopId = builder.AllocateLoopId();
        var coedgeIds = new List<CoedgeId>(vertices.Count);
        var edgeIds = new List<EdgeId>(vertices.Count);
        for (var i = 0; i < vertices.Count; i++)
        {
            var edgeId = builder.AddEdge(vertexIds[i], vertexIds[(i + 1) % vertices.Count]);
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

    private static Point3D[] CreateBsplineLoopVertices(IReadOnlyList<(double U, double V)> uvLoop)
        => uvLoop.Select(point => new Point3D(point.U, point.V, point.U * point.V)).ToArray();

    private static Line3Curve CreateLine(Point3D start, Point3D end, out double length)
    {
        var delta = end - start;
        length = delta.Length;
        return new Line3Curve(start, Direction3D.Create(delta));
    }

    private static (double U, double V)[] CreateRectangleLoop((double UMin, double UMax, double VMin, double VMax) bounds)
        =>
        [
            (bounds.UMin, bounds.VMin),
            (bounds.UMax, bounds.VMin),
            (bounds.UMax, bounds.VMax),
            (bounds.UMin, bounds.VMax),
        ];

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

    internal static BrepBody CreateBodyWithMissingVertexPoint(BrepBody source)
    {
        var geometry = new BrepGeometryStore();
        foreach (var (id, curve) in source.Geometry.Curves)
        {
            geometry.AddCurve(id, curve);
        }

        foreach (var (id, surface) in source.Geometry.Surfaces)
        {
            geometry.AddSurface(id, surface);
        }

        var bindings = new BrepBindingModel();
        foreach (var edgeBinding in source.Bindings.EdgeBindings)
        {
            bindings.AddEdgeBinding(edgeBinding);
        }

        foreach (var faceBinding in source.Bindings.FaceBindings)
        {
            bindings.AddFaceBinding(faceBinding);
        }

        var removedVertexId = source.Topology.Vertices.OrderBy(vertex => vertex.Id.Value).First().Id;
        var vertexPoints = source.Topology.Vertices
            .Where(vertex => vertex.Id != removedVertexId && source.TryGetVertexPoint(vertex.Id, out _))
            .ToDictionary(vertex => vertex.Id, vertex =>
            {
                source.TryGetVertexPoint(vertex.Id, out var point);
                return point;
            });

        return new BrepBody(source.Topology, geometry, bindings, vertexPoints, source.SafeBooleanComposition, source.ShellRepresentation);
    }

    internal static BrepBody CreateBodyWithUnsupportedTrimEdge(BrepBody source)
    {
        var geometry = new BrepGeometryStore();
        foreach (var (id, curve) in source.Geometry.Curves)
        {
            geometry.AddCurve(id, curve);
        }

        foreach (var (id, surface) in source.Geometry.Surfaces)
        {
            geometry.AddSurface(id, surface);
        }

        var bindings = new BrepBindingModel();
        var firstEdgeId = source.Topology.Edges.OrderBy(edge => edge.Id.Value).First().Id;
        foreach (var edgeBinding in source.Bindings.EdgeBindings)
        {
            if (edgeBinding.EdgeId == firstEdgeId)
            {
                bindings.AddEdgeBinding(edgeBinding with { CurveGeometryId = new CurveGeometryId(999_001) });
            }
            else
            {
                bindings.AddEdgeBinding(edgeBinding);
            }
        }

        foreach (var faceBinding in source.Bindings.FaceBindings)
        {
            bindings.AddFaceBinding(faceBinding);
        }

        geometry.AddCurve(new CurveGeometryId(999_001), CurveGeometry.FromUnsupported("unsupported-trim-edge"));

        var vertexPoints = source.Topology.Vertices
            .Where(vertex => source.TryGetVertexPoint(vertex.Id, out _))
            .ToDictionary(vertex => vertex.Id, vertex =>
            {
                source.TryGetVertexPoint(vertex.Id, out var point);
                return point;
            });

        return new BrepBody(source.Topology, geometry, bindings, vertexPoints, source.SafeBooleanComposition, source.ShellRepresentation);
    }
}
