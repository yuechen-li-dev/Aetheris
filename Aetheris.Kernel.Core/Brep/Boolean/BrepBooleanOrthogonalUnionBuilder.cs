using Aetheris.Kernel.Core.Geometry;
using Aetheris.Kernel.Core.Geometry.Curves;
using Aetheris.Kernel.Core.Geometry.Surfaces;
using Aetheris.Kernel.Core.Math;
using Aetheris.Kernel.Core.Numerics;
using Aetheris.Kernel.Core.Results;
using Aetheris.Kernel.Core.Topology;
using Aetheris.Kernel.Core.Diagnostics;

namespace Aetheris.Kernel.Core.Brep.Boolean;

public static class BrepBooleanOrthogonalUnionBuilder
{
    public static KernelResult<BrepBody> BuildFromCells(IReadOnlyList<AxisAlignedBoxExtents> cells)
    {
        ArgumentNullException.ThrowIfNull(cells);
        if (cells.Count == 0)
        {
            return KernelResult<BrepBody>.Failure([
                new KernelDiagnostic(
                    KernelDiagnosticCode.InvalidArgument,
                    KernelDiagnosticSeverity.Error,
                    "Orthogonal union builder requires at least one occupied cell.",
                    "BrepBooleanOrthogonalUnionBuilder.BuildFromCells"),
            ]);
        }

        var faceRects = CollectBoundaryFaceRectangles(cells);
        faceRects = MergeCoplanarFaceRectangles(faceRects);
        if (faceRects.Count == 0)
        {
            return KernelResult<BrepBody>.Failure([
                new KernelDiagnostic(
                    KernelDiagnosticCode.NotImplemented,
                    KernelDiagnosticSeverity.Error,
                    "Orthogonal union builder could not recover boundary rectangles from occupied cells.",
                    "BrepBooleanOrthogonalUnionBuilder.BuildFromCells"),
            ]);
        }

        var builder = new TopologyBuilder();
        var vertexLookup = new Dictionary<PointKey, VertexId>();
        var vertexPoints = new Dictionary<VertexId, Point3D>();
        var edgeLookup = new Dictionary<EdgeKey, EdgeId>();
        var edgeEndpoints = new Dictionary<EdgeId, EdgeKey>();
        var edgeBindings = new List<EdgeGeometryBinding>();
        var faceBindings = new List<(FaceId FaceId, SurfaceGeometryId SurfaceId)>();
        var geometry = new BrepGeometryStore();

        var curveId = 1;
        var surfaceId = 1;

        foreach (var rect in faceRects.OrderBy(r => r.Axis).ThenBy(r => r.Fixed).ThenBy(r => r.MinA).ThenBy(r => r.MinB))
        {
            var corners = rect.GetOrientedCorners();
            var edges = new EdgeId[4];
            for (var i = 0; i < 4; i++)
            {
                var start = corners[i];
                var end = corners[(i + 1) % 4];
                var startVertex = GetOrCreateVertex(start);
                var endVertex = GetOrCreateVertex(end);
                var edgeKey = EdgeKey.Create(start, end);
                if (!edgeLookup.TryGetValue(edgeKey, out var edgeId))
                {
                    edgeId = builder.AddEdge(startVertex, endVertex);
                    edgeLookup[edgeKey] = edgeId;
                    edgeEndpoints[edgeId] = edgeKey;
                }

                edges[i] = edgeId;
            }

            var uses = new[]
            {
                OrientEdgeUse(edges[0], corners[0], corners[1]),
                OrientEdgeUse(edges[1], corners[1], corners[2]),
                OrientEdgeUse(edges[2], corners[2], corners[3]),
                OrientEdgeUse(edges[3], corners[3], corners[0]),
            };

            var faceId = AddFaceWithLoop(builder, uses);
            var rectSurfaceId = new SurfaceGeometryId(surfaceId);
            geometry.AddSurface(rectSurfaceId, SurfaceGeometry.FromPlane(rect.ToPlaneSurface()));
            faceBindings.Add((faceId, rectSurfaceId));
            surfaceId++;
        }

        foreach (var (edgeId, endpoints) in edgeEndpoints.OrderBy(kvp => kvp.Key.Value))
        {
            var start = endpoints.Start.ToPoint3D();
            var end = endpoints.End.ToPoint3D();
            var direction = new Vector3D(end.X - start.X, end.Y - start.Y, end.Z - start.Z);
            var length = System.Math.Sqrt((direction.X * direction.X) + (direction.Y * direction.Y) + (direction.Z * direction.Z));
            if (length <= 0d)
            {
                return KernelResult<BrepBody>.Failure([
                    new KernelDiagnostic(
                        KernelDiagnosticCode.InternalError,
                        KernelDiagnosticSeverity.Error,
                        "Orthogonal union builder produced a degenerate edge.",
                        "BrepBooleanOrthogonalUnionBuilder.BuildFromCells"),
                ]);
            }

            geometry.AddCurve(new CurveGeometryId(curveId), CurveGeometry.FromLine(new Line3Curve(start, Direction3D.Create(direction))));
            edgeBindings.Add(new EdgeGeometryBinding(edgeId, new CurveGeometryId(curveId), new ParameterInterval(0d, 1d)));
            curveId++;
        }

        var faces = builder.Model.Faces.OrderBy(f => f.Id.Value).Select(f => f.Id).ToArray();
        var shell = builder.AddShell(faces);
        builder.AddBody([shell]);

        var bindings = new BrepBindingModel();
        foreach (var edgeBinding in edgeBindings.OrderBy(binding => binding.EdgeId.Value))
        {
            bindings.AddEdgeBinding(edgeBinding);
        }

        foreach (var (faceId, surfaceGeometryId) in faceBindings.OrderBy(binding => binding.FaceId.Value))
        {
            bindings.AddFaceBinding(new FaceGeometryBinding(faceId, surfaceGeometryId));
        }

        var body = new BrepBody(builder.Model, geometry, bindings, vertexPoints, safeBooleanComposition: null);
        var validation = BrepBindingValidator.Validate(body, requireAllEdgeAndFaceBindings: true);
        return validation.IsSuccess
            ? KernelResult<BrepBody>.Success(body, validation.Diagnostics)
            : KernelResult<BrepBody>.Failure(validation.Diagnostics);

        VertexId GetOrCreateVertex(in PointKey point)
        {
            if (vertexLookup.TryGetValue(point, out var id))
            {
                return id;
            }

            id = builder.AddVertex();
            vertexLookup[point] = id;
            vertexPoints[id] = point.ToPoint3D();
            return id;
        }
    }

    private static EdgeUse OrientEdgeUse(EdgeId edgeId, in PointKey start, in PointKey end)
    {
        var canonical = EdgeKey.Create(start, end);
        return canonical.Start.Equals(start) && canonical.End.Equals(end)
            ? EdgeUse.Forward(edgeId)
            : EdgeUse.Reversed(edgeId);
    }

    private static FaceId AddFaceWithLoop(TopologyBuilder builder, IReadOnlyList<EdgeUse> edgeUses)
    {
        var loopId = builder.AllocateLoopId();
        var coedgeIds = new CoedgeId[edgeUses.Count];
        for (var i = 0; i < edgeUses.Count; i++)
        {
            coedgeIds[i] = builder.AllocateCoedgeId();
        }

        for (var i = 0; i < edgeUses.Count; i++)
        {
            var next = coedgeIds[(i + 1) % edgeUses.Count];
            var prev = coedgeIds[(i + edgeUses.Count - 1) % edgeUses.Count];
            builder.AddCoedge(new Coedge(coedgeIds[i], edgeUses[i].EdgeId, loopId, next, prev, edgeUses[i].IsReversed));
        }

        builder.AddLoop(new Loop(loopId, coedgeIds));
        return builder.AddFace([loopId]);
    }

    private static List<FaceRect> CollectBoundaryFaceRectangles(IReadOnlyList<AxisAlignedBoxExtents> cells)
    {
        var faces = new List<FaceRect>();

        foreach (var cell in cells)
        {
            var key = CellKey.FromExtents(cell);
            TryAddFace(cells, faces, key, AxisKind.X, key.MinX, isPositiveNormal: false);
            TryAddFace(cells, faces, key, AxisKind.X, key.MaxX, isPositiveNormal: true);
            TryAddFace(cells, faces, key, AxisKind.Y, key.MinY, isPositiveNormal: false);
            TryAddFace(cells, faces, key, AxisKind.Y, key.MaxY, isPositiveNormal: true);
            TryAddFace(cells, faces, key, AxisKind.Z, key.MinZ, isPositiveNormal: false);
            TryAddFace(cells, faces, key, AxisKind.Z, key.MaxZ, isPositiveNormal: true);
        }

        return faces;
    }

    private static List<FaceRect> MergeCoplanarFaceRectangles(IReadOnlyList<FaceRect> faces)
    {
        var merged = new List<FaceRect>();
        foreach (var group in faces.GroupBy(face => new PlaneKey(face.Axis, face.Fixed, face.IsPositiveNormal)))
        {
            merged.AddRange(MergePlaneGroup(group.Key, group.ToArray()));
        }

        return merged;
    }

    private static IEnumerable<FaceRect> MergePlaneGroup(PlaneKey plane, IReadOnlyList<FaceRect> faces)
    {
        var aCoordinates = faces.SelectMany(face => new[] { face.MinA, face.MaxA }).Distinct().OrderBy(v => v).ToArray();
        var bCoordinates = faces.SelectMany(face => new[] { face.MinB, face.MaxB }).Distinct().OrderBy(v => v).ToArray();
        if (aCoordinates.Length < 2 || bCoordinates.Length < 2)
        {
            yield break;
        }

        var aIndex = new Dictionary<double, int>(aCoordinates.Length);
        for (var i = 0; i < aCoordinates.Length; i++)
        {
            aIndex[aCoordinates[i]] = i;
        }

        var bIndex = new Dictionary<double, int>(bCoordinates.Length);
        for (var i = 0; i < bCoordinates.Length; i++)
        {
            bIndex[bCoordinates[i]] = i;
        }

        var occupancy = new bool[aCoordinates.Length - 1, bCoordinates.Length - 1];
        foreach (var rect in faces)
        {
            var minA = aIndex[rect.MinA];
            var maxA = aIndex[rect.MaxA];
            var minB = bIndex[rect.MinB];
            var maxB = bIndex[rect.MaxB];
            for (var a = minA; a < maxA; a++)
            {
                for (var b = minB; b < maxB; b++)
                {
                    occupancy[a, b] = true;
                }
            }
        }

        for (var b = 0; b < bCoordinates.Length - 1; b++)
        {
            for (var a = 0; a < aCoordinates.Length - 1; a++)
            {
                if (!occupancy[a, b])
                {
                    continue;
                }

                var maxA = a + 1;
                while (maxA < aCoordinates.Length - 1 && occupancy[maxA, b])
                {
                    maxA++;
                }

                var maxB = b + 1;
                var canGrow = true;
                while (canGrow && maxB < bCoordinates.Length - 1)
                {
                    for (var fillA = a; fillA < maxA; fillA++)
                    {
                        if (!occupancy[fillA, maxB])
                        {
                            canGrow = false;
                            break;
                        }
                    }

                    if (canGrow)
                    {
                        maxB++;
                    }
                }

                for (var fillA = a; fillA < maxA; fillA++)
                {
                    for (var fillB = b; fillB < maxB; fillB++)
                    {
                        occupancy[fillA, fillB] = false;
                    }
                }

                yield return new FaceRect(
                    plane.Axis,
                    plane.Fixed,
                    aCoordinates[a],
                    aCoordinates[maxA],
                    bCoordinates[b],
                    bCoordinates[maxB],
                    plane.IsPositiveNormal);
            }
        }
    }

    private static void TryAddFace(IReadOnlyList<AxisAlignedBoxExtents> cells, List<FaceRect> faces, CellKey cell, AxisKind axis, double fixedValue, bool isPositiveNormal)
    {
        var coveredByNeighbor = cells.Any(other =>
        {
            var otherCell = CellKey.FromExtents(other);
            if (otherCell.Equals(cell))
            {
                return false;
            }

            return axis switch
            {
                AxisKind.X when isPositiveNormal => otherCell.MinX <= fixedValue && fixedValue < otherCell.MaxX
                    && otherCell.MinY <= cell.MinY && otherCell.MaxY >= cell.MaxY
                    && otherCell.MinZ <= cell.MinZ && otherCell.MaxZ >= cell.MaxZ,
                AxisKind.X => otherCell.MinX < fixedValue && fixedValue <= otherCell.MaxX
                    && otherCell.MinY <= cell.MinY && otherCell.MaxY >= cell.MaxY
                    && otherCell.MinZ <= cell.MinZ && otherCell.MaxZ >= cell.MaxZ,
                AxisKind.Y when isPositiveNormal => otherCell.MinY <= fixedValue && fixedValue < otherCell.MaxY
                    && otherCell.MinX <= cell.MinX && otherCell.MaxX >= cell.MaxX
                    && otherCell.MinZ <= cell.MinZ && otherCell.MaxZ >= cell.MaxZ,
                AxisKind.Y => otherCell.MinY < fixedValue && fixedValue <= otherCell.MaxY
                    && otherCell.MinX <= cell.MinX && otherCell.MaxX >= cell.MaxX
                    && otherCell.MinZ <= cell.MinZ && otherCell.MaxZ >= cell.MaxZ,
                AxisKind.Z when isPositiveNormal => otherCell.MinZ <= fixedValue && fixedValue < otherCell.MaxZ
                    && otherCell.MinX <= cell.MinX && otherCell.MaxX >= cell.MaxX
                    && otherCell.MinY <= cell.MinY && otherCell.MaxY >= cell.MaxY,
                _ => otherCell.MinZ < fixedValue && fixedValue <= otherCell.MaxZ
                    && otherCell.MinX <= cell.MinX && otherCell.MaxX >= cell.MaxX
                    && otherCell.MinY <= cell.MinY && otherCell.MaxY >= cell.MaxY,
            };
        });

        if (coveredByNeighbor)
        {
            return;
        }

        faces.Add(axis switch
        {
            AxisKind.X => new FaceRect(axis, fixedValue, cell.MinY, cell.MaxY, cell.MinZ, cell.MaxZ, isPositiveNormal),
            AxisKind.Y => new FaceRect(axis, fixedValue, cell.MinX, cell.MaxX, cell.MinZ, cell.MaxZ, isPositiveNormal),
            _ => new FaceRect(axis, fixedValue, cell.MinX, cell.MaxX, cell.MinY, cell.MaxY, isPositiveNormal),
        });
    }

    private enum AxisKind
    {
        X,
        Y,
        Z,
    }

    private readonly record struct CellKey(double MinX, double MaxX, double MinY, double MaxY, double MinZ, double MaxZ)
    {
        public static CellKey FromExtents(AxisAlignedBoxExtents extents)
            => new(extents.MinX, extents.MaxX, extents.MinY, extents.MaxY, extents.MinZ, extents.MaxZ);
    }

    private readonly record struct PlaneKey(AxisKind Axis, double Fixed, bool IsPositiveNormal);

    private readonly record struct FaceRect(AxisKind Axis, double Fixed, double MinA, double MaxA, double MinB, double MaxB, bool IsPositiveNormal)
    {
        public PointKey[] GetOrientedCorners()
            => Axis switch
            {
                AxisKind.X when IsPositiveNormal => [new PointKey(Fixed, MinA, MinB), new PointKey(Fixed, MaxA, MinB), new PointKey(Fixed, MaxA, MaxB), new PointKey(Fixed, MinA, MaxB)],
                AxisKind.X => [new PointKey(Fixed, MinA, MinB), new PointKey(Fixed, MinA, MaxB), new PointKey(Fixed, MaxA, MaxB), new PointKey(Fixed, MaxA, MinB)],
                AxisKind.Y when IsPositiveNormal => [new PointKey(MinA, Fixed, MinB), new PointKey(MinA, Fixed, MaxB), new PointKey(MaxA, Fixed, MaxB), new PointKey(MaxA, Fixed, MinB)],
                AxisKind.Y => [new PointKey(MinA, Fixed, MinB), new PointKey(MaxA, Fixed, MinB), new PointKey(MaxA, Fixed, MaxB), new PointKey(MinA, Fixed, MaxB)],
                AxisKind.Z when IsPositiveNormal => [new PointKey(MinA, MinB, Fixed), new PointKey(MaxA, MinB, Fixed), new PointKey(MaxA, MaxB, Fixed), new PointKey(MinA, MaxB, Fixed)],
                _ => [new PointKey(MinA, MinB, Fixed), new PointKey(MinA, MaxB, Fixed), new PointKey(MaxA, MaxB, Fixed), new PointKey(MaxA, MinB, Fixed)],
            };

        public PlaneSurface ToPlaneSurface()
            => Axis switch
            {
                AxisKind.X when IsPositiveNormal => new PlaneSurface(new Point3D(Fixed, 0d, 0d), Direction3D.Create(new Vector3D(1d, 0d, 0d)), Direction3D.Create(new Vector3D(0d, 1d, 0d))),
                AxisKind.X => new PlaneSurface(new Point3D(Fixed, 0d, 0d), Direction3D.Create(new Vector3D(-1d, 0d, 0d)), Direction3D.Create(new Vector3D(0d, 1d, 0d))),
                AxisKind.Y when IsPositiveNormal => new PlaneSurface(new Point3D(0d, Fixed, 0d), Direction3D.Create(new Vector3D(0d, 1d, 0d)), Direction3D.Create(new Vector3D(-1d, 0d, 0d))),
                AxisKind.Y => new PlaneSurface(new Point3D(0d, Fixed, 0d), Direction3D.Create(new Vector3D(0d, -1d, 0d)), Direction3D.Create(new Vector3D(1d, 0d, 0d))),
                AxisKind.Z when IsPositiveNormal => new PlaneSurface(new Point3D(0d, 0d, Fixed), Direction3D.Create(new Vector3D(0d, 0d, 1d)), Direction3D.Create(new Vector3D(1d, 0d, 0d))),
                _ => new PlaneSurface(new Point3D(0d, 0d, Fixed), Direction3D.Create(new Vector3D(0d, 0d, -1d)), Direction3D.Create(new Vector3D(1d, 0d, 0d))),
            };
    }

    private readonly record struct PointKey(double X, double Y, double Z)
    {
        public Point3D ToPoint3D() => new(X, Y, Z);
    }

    private readonly record struct EdgeKey(PointKey Start, PointKey End)
    {
        public static EdgeKey Create(in PointKey a, in PointKey b)
            => Compare(a, b) <= 0
                ? new EdgeKey(a, b)
                : new EdgeKey(b, a);

        private static int Compare(in PointKey a, in PointKey b)
        {
            var x = a.X.CompareTo(b.X);
            if (x != 0)
            {
                return x;
            }

            var y = a.Y.CompareTo(b.Y);
            if (y != 0)
            {
                return y;
            }

            return a.Z.CompareTo(b.Z);
        }
    }

    private readonly record struct EdgeUse(EdgeId EdgeId, bool IsReversed)
    {
        public static EdgeUse Forward(EdgeId edgeId) => new(edgeId, IsReversed: false);
        public static EdgeUse Reversed(EdgeId edgeId) => new(edgeId, IsReversed: true);
    }
}
