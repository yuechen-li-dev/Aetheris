using Aetheris.Kernel.Core.Brep;
using Aetheris.Kernel.Core.Brep.Tessellation;
using Aetheris.Kernel.Core.Geometry;
using Aetheris.Kernel.Core.Step242;

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
