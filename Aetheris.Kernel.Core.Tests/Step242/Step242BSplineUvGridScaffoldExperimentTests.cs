using Aetheris.Kernel.Core.Brep;
using Aetheris.Kernel.Core.Brep.Tessellation;
using Aetheris.Kernel.Core.Geometry;
using Aetheris.Kernel.Core.Geometry.Surfaces;
using Aetheris.Kernel.Core.Math;
using Aetheris.Kernel.Core.Step242;
using Aetheris.Kernel.Core.Topology;

namespace Aetheris.Kernel.Core.Tests.Step242;

public sealed class Step242BSplineUvGridScaffoldExperimentTests
{
    private const int USegments = 12;
    private const int VSegments = 12;

    [Fact]
    public void BspineUvGridScaffold_RepresentativeFace_IsDeterministic_AndComparableToCurrentTessellationPath()
    {
        var body = ImportRepresentativeBsplineBody();

        var face = Assert.Single(body.Topology.Faces);
        var surface = GetBsplineSurface(body, face.Id);

        var first = BuildUvGridScaffold(surface, USegments, VSegments, smoothInteriorOnce: false);
        var second = BuildUvGridScaffold(surface, USegments, VSegments, smoothInteriorOnce: false);

        Assert.Equal(first.Positions, second.Positions);
        Assert.Equal(first.TriangleIndices, second.TriangleIndices);

        Assert.Equal((USegments + 1) * (VSegments + 1), first.Positions.Count);
        Assert.Equal(USegments * VSegments * 2, first.TriangleIndices.Count / 3);

        AssertBoundaryLooksReasonableForRepresentativeFace(first, body, face.Id);

        var tessellation = BrepDisplayTessellator.Tessellate(body);
        Assert.True(tessellation.IsSuccess);

        var referencePatch = Assert.Single(tessellation.Value.FacePatches, patch => patch.FaceId == face.Id);
        Assert.NotEmpty(referencePatch.Positions);
        Assert.NotEmpty(referencePatch.TriangleIndices);

        var scaffoldToReference = ComputeMeanNearestDistance(first.Positions, referencePatch.Positions);
        var referenceToScaffold = ComputeMeanNearestDistance(referencePatch.Positions, first.Positions);

        Assert.True(scaffoldToReference <= 0.08d, $"Scaffold->tessellation mean nearest distance too large: {scaffoldToReference:0.########}");
        Assert.True(referenceToScaffold <= 0.08d, $"Tessellation->scaffold mean nearest distance too large: {referenceToScaffold:0.########}");
    }

    [Fact]
    public void BspineUvGridScaffold_OneStepSmoothing_ImprovesInteriorLaplacianResidual_WithoutMovingBoundary()
    {
        var body = ImportRepresentativeBsplineBody();

        var face = Assert.Single(body.Topology.Faces);
        var surface = GetBsplineSurface(body, face.Id);

        var unsmoothed = BuildUvGridScaffold(surface, USegments, VSegments, smoothInteriorOnce: false);
        var smoothed = BuildUvGridScaffold(surface, USegments, VSegments, smoothInteriorOnce: true);

        Assert.Equal(unsmoothed.TriangleIndices, smoothed.TriangleIndices);

        var unsmoothedResidual = ComputeInteriorLaplacianResidual(unsmoothed.Positions, USegments, VSegments);
        var smoothedResidual = ComputeInteriorLaplacianResidual(smoothed.Positions, USegments, VSegments);

        Assert.True(smoothedResidual <= unsmoothedResidual,
            $"Expected one-step smoothing residual <= baseline. baseline={unsmoothedResidual:0.########}, smoothed={smoothedResidual:0.########}");

        AssertBoundaryUnchanged(unsmoothed.Positions, smoothed.Positions, USegments, VSegments);
    }

    private static BrepBody ImportRepresentativeBsplineBody()
    {
        // Representative case choice for M17h experiment:
        // a single-face B_SPLINE_SURFACE_WITH_KNOTS patch with a simple rectangular trim.
        // It is genuinely B-spline and intentionally non-pathological so scaffold behavior is easy to inspect.
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
        Assert.NotNull(surface.BSplineSurfaceWithKnots);
        return surface.BSplineSurfaceWithKnots!;
    }

    private static ScaffoldMesh BuildUvGridScaffold(BSplineSurfaceWithKnots surface, int uSegments, int vSegments, bool smoothInteriorOnce)
    {
        var rows = uSegments + 1;
        var cols = vSegments + 1;
        var positions = new Point3D[rows * cols];

        for (var uIndex = 0; uIndex < rows; uIndex++)
        {
            var tu = (double)uIndex / uSegments;
            var u = Lerp(surface.DomainStartU, surface.DomainEndU, tu);
            for (var vIndex = 0; vIndex < cols; vIndex++)
            {
                var tv = (double)vIndex / vSegments;
                var v = Lerp(surface.DomainStartV, surface.DomainEndV, tv);
                positions[GridIndex(uIndex, vIndex, cols)] = surface.Evaluate(u, v);
            }
        }

        if (smoothInteriorOnce)
        {
            positions = SmoothInteriorOnce(positions, uSegments, vSegments);
        }

        var triangles = new List<int>(uSegments * vSegments * 6);
        for (var uIndex = 0; uIndex < uSegments; uIndex++)
        {
            for (var vIndex = 0; vIndex < vSegments; vIndex++)
            {
                var bottomLeft = GridIndex(uIndex, vIndex, cols);
                var bottomRight = GridIndex(uIndex + 1, vIndex, cols);
                var topLeft = GridIndex(uIndex, vIndex + 1, cols);
                var topRight = GridIndex(uIndex + 1, vIndex + 1, cols);

                triangles.Add(bottomLeft);
                triangles.Add(bottomRight);
                triangles.Add(topRight);

                triangles.Add(bottomLeft);
                triangles.Add(topRight);
                triangles.Add(topLeft);
            }
        }

        return new ScaffoldMesh(positions, triangles);
    }

    private static Point3D[] SmoothInteriorOnce(IReadOnlyList<Point3D> positions, int uSegments, int vSegments)
    {
        var rows = uSegments + 1;
        var cols = vSegments + 1;
        var smoothed = positions.ToArray();

        for (var uIndex = 1; uIndex < rows - 1; uIndex++)
        {
            for (var vIndex = 1; vIndex < cols - 1; vIndex++)
            {
                var left = positions[GridIndex(uIndex - 1, vIndex, cols)];
                var right = positions[GridIndex(uIndex + 1, vIndex, cols)];
                var down = positions[GridIndex(uIndex, vIndex - 1, cols)];
                var up = positions[GridIndex(uIndex, vIndex + 1, cols)];
                var averaged = new Point3D(
                    (left.X + right.X + down.X + up.X) * 0.25d,
                    (left.Y + right.Y + down.Y + up.Y) * 0.25d,
                    (left.Z + right.Z + down.Z + up.Z) * 0.25d);

                smoothed[GridIndex(uIndex, vIndex, cols)] = averaged;
            }
        }

        return smoothed;
    }

    private static double ComputeInteriorLaplacianResidual(IReadOnlyList<Point3D> positions, int uSegments, int vSegments)
    {
        var cols = vSegments + 1;
        var sum = 0d;
        var count = 0;

        for (var uIndex = 1; uIndex < uSegments; uIndex++)
        {
            for (var vIndex = 1; vIndex < vSegments; vIndex++)
            {
                var center = positions[GridIndex(uIndex, vIndex, cols)];
                var left = positions[GridIndex(uIndex - 1, vIndex, cols)];
                var right = positions[GridIndex(uIndex + 1, vIndex, cols)];
                var down = positions[GridIndex(uIndex, vIndex - 1, cols)];
                var up = positions[GridIndex(uIndex, vIndex + 1, cols)];
                var average = new Point3D(
                    (left.X + right.X + down.X + up.X) * 0.25d,
                    (left.Y + right.Y + down.Y + up.Y) * 0.25d,
                    (left.Z + right.Z + down.Z + up.Z) * 0.25d);

                var delta = center - average;
                sum += delta.Length;
                count++;
            }
        }

        return count == 0 ? 0d : sum / count;
    }

    private static void AssertBoundaryUnchanged(IReadOnlyList<Point3D> first, IReadOnlyList<Point3D> second, int uSegments, int vSegments)
    {
        var cols = vSegments + 1;
        for (var uIndex = 0; uIndex <= uSegments; uIndex++)
        {
            Assert.Equal(first[GridIndex(uIndex, 0, cols)], second[GridIndex(uIndex, 0, cols)]);
            Assert.Equal(first[GridIndex(uIndex, vSegments, cols)], second[GridIndex(uIndex, vSegments, cols)]);
        }

        for (var vIndex = 0; vIndex <= vSegments; vIndex++)
        {
            Assert.Equal(first[GridIndex(0, vIndex, cols)], second[GridIndex(0, vIndex, cols)]);
            Assert.Equal(first[GridIndex(uSegments, vIndex, cols)], second[GridIndex(uSegments, vIndex, cols)]);
        }
    }

    private static void AssertBoundaryLooksReasonableForRepresentativeFace(ScaffoldMesh scaffold, BrepBody body, FaceId faceId)
    {
        var face = body.Topology.GetFace(faceId);
        var loopId = Assert.Single(face.LoopIds);
        var loop = body.Topology.GetLoop(loopId);

        var boundaryVertexPoints = loop.CoedgeIds
            .Select(coedgeId => body.Topology.GetCoedge(coedgeId))
            .Select(coedge =>
            {
                var edge = body.Topology.GetEdge(coedge.EdgeId);
                var vertexId = coedge.IsReversed ? edge.EndVertexId : edge.StartVertexId;
                var found = body.TryGetVertexPoint(vertexId, out var point);
                Assert.True(found);
                return point;
            })
            .Distinct()
            .ToArray();

        Assert.Equal(4, boundaryVertexPoints.Length);

        var cols = VSegments + 1;
        var corners = new[]
        {
            scaffold.Positions[GridIndex(0, 0, cols)],
            scaffold.Positions[GridIndex(USegments, 0, cols)],
            scaffold.Positions[GridIndex(USegments, VSegments, cols)],
            scaffold.Positions[GridIndex(0, VSegments, cols)]
        };

        foreach (var corner in corners)
        {
            var nearestDistance = boundaryVertexPoints.Min(vertex => (vertex - corner).Length);
            Assert.True(nearestDistance <= 1e-9d);
        }

        foreach (var point in EnumerateBoundaryPoints(scaffold.Positions, USegments, VSegments))
        {
            Assert.True(point.X >= -1e-12d && point.X <= 1d + 1e-12d);
            Assert.True(point.Y >= -1e-12d && point.Y <= 1d + 1e-12d);
            Assert.True(double.Abs(point.Z) <= 1e-12d);
        }
    }

    private static IEnumerable<Point3D> EnumerateBoundaryPoints(IReadOnlyList<Point3D> positions, int uSegments, int vSegments)
    {
        var cols = vSegments + 1;

        for (var uIndex = 0; uIndex <= uSegments; uIndex++)
        {
            yield return positions[GridIndex(uIndex, 0, cols)];
            yield return positions[GridIndex(uIndex, vSegments, cols)];
        }

        for (var vIndex = 1; vIndex < vSegments; vIndex++)
        {
            yield return positions[GridIndex(0, vIndex, cols)];
            yield return positions[GridIndex(uSegments, vIndex, cols)];
        }
    }

    private static double ComputeMeanNearestDistance(IReadOnlyList<Point3D> source, IReadOnlyList<Point3D> target)
    {
        Assert.NotEmpty(source);
        Assert.NotEmpty(target);

        var sum = 0d;
        foreach (var sourcePoint in source)
        {
            var nearest = target.Min(targetPoint => (targetPoint - sourcePoint).Length);
            sum += nearest;
        }

        return sum / source.Count;
    }

    private static int GridIndex(int uIndex, int vIndex, int columns)
        => (uIndex * columns) + vIndex;

    private static double Lerp(double min, double max, double t)
        => min + ((max - min) * t);

    private sealed record ScaffoldMesh(
        IReadOnlyList<Point3D> Positions,
        IReadOnlyList<int> TriangleIndices);
}
