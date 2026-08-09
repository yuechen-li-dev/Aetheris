using Aetheris.Kernel.Core.Brep.Tessellation;
using Aetheris.Kernel.Core.Geometry;
using Aetheris.Kernel.Core.Geometry.Curves;
using Aetheris.Kernel.Core.Geometry.Surfaces;
using Aetheris.Kernel.Core.Math;
using Aetheris.Kernel.Core.Topology;

namespace Aetheris.Kernel.Core.Tests.Brep.Tessellation;

public sealed class PlanarDomainPlannerTests
{
    [Fact]
    public void MultipleCircularFeatures_AreDeterministicallyPartitionedIntoConvexCells()
    {
        var vertices = new Dictionary<int, SurfaceMeshVertex>();
        var next = 0;
        int Add(double u, double v) { var id = next++; vertices[id] = new SurfaceMeshVertex(id, new Point3D(u, v, 0d), u, v); return id; }
        var outer = new[] { Add(0d, 0d), Add(40d, 0d), Add(40d, 24d), Add(0d, 24d) };
        var firstHole = Circle(Add, 10d, 12d, 3d, 12);
        var secondHole = Circle(Add, 28d, 12d, 4d, 12);
        var loops = new[]
        {
            Loop(1, outer, vertices),
            Loop(2, firstHole, vertices),
            Loop(3, secondHole, vertices),
        };
        var plane = new PlaneSurface(Point3D.Origin, Direction3D.Create(new Vector3D(0d, 0d, 1d)), Direction3D.Create(new Vector3D(1d, 0d, 0d)));
        var domain = PlanarDomainPlanner.Create(loops);

        Assert.True(PlanarDomainPlanner.TryDecompose(domain, plane, vertices, true, out var raw, out var path));
        var compact = PlanarDomainPlanner.MergeConformingCells(raw, domain.BoundaryVertices, true);
        Assert.Equal("PlanarDomain.FeatureLoops.ConvexPartition", path);
        Assert.All(outer.Concat(firstHole).Concat(secondHole), id => Assert.Contains(compact.SelectMany(cell => cell.VertexIds), candidate => candidate == id));
        Assert.Contains(compact, cell => cell is QuadCell or BoundaryPolygonCell);

        Assert.True(PlanarDomainPlanner.TryDecompose(domain, plane, vertices, true, out var rawAgain, out _));
        var compactAgain = PlanarDomainPlanner.MergeConformingCells(rawAgain, domain.BoundaryVertices, true);
        Assert.Equal(compact.Select(cell => $"{cell.Kind}:{string.Join(',', cell.VertexIds)}"), compactAgain.Select(cell => $"{cell.Kind}:{string.Join(',', cell.VertexIds)}"));
    }

    [Fact]
    public void FeatureBandPlanner_ClassifiesHoleAndSlot_AndKeepsTheirTopologyLocal()
    {
        var vertices = new Dictionary<int, SurfaceMeshVertex>();
        var next = 0;
        int Add(double u, double v) { var id = next++; vertices[id] = new SurfaceMeshVertex(id, new Point3D(u, v, 0d), u, v); return id; }
        var outerIds = new[] { Add(0d, 0d), Add(60d, 0d), Add(60d, 36d), Add(0d, 36d) };
        var holeIds = Circle(Add, 16d, 18d, 4d, 24);
        var slotIds = RoundedSlot(Add, 40d, 18d, 12d, 3d, 8);
        var outer = Loop(10, outerIds, vertices, Spans(outerIds, CurveGeometryKind.Line3, 100));
        var hole = Loop(11, holeIds, vertices, [new SurfaceMeshBoundarySpan(new EdgeId(200), holeIds[0], holeIds[0], CurveGeometryKind.Circle3, holeIds.Length)]);
        var slot = Loop(12, slotIds, vertices, SlotSpans(slotIds, 300));
        var domain = PlanarDomainPlanner.Create([outer, hole, slot]);

        Assert.Equal(PlanarFeatureLoopKind.CircularHole, domain.FeatureLoops.Single(feature => feature.Loop.LoopId.Value == 11).Kind);
        Assert.Equal(PlanarFeatureLoopKind.Slot, domain.FeatureLoops.Single(feature => feature.Loop.LoopId.Value == 12).Kind);
        var mutable = vertices.Values.OrderBy(vertex => vertex.Id).ToList();
        var plane = new PlaneSurface(Point3D.Origin, Direction3D.Create(new Vector3D(0d, 0d, 1d)), Direction3D.Create(new Vector3D(1d, 0d, 0d)));
        Assert.True(PlanarFeatureBandPlanner.TryPlan(domain, plane, mutable, ref next, true, out var cells, out var plan, out var failure), failure);
        Assert.False(plan.UsedM6Fallback);
        Assert.Equal(3, plan.Bands.Count); // outer boundary band plus two feature bands
        Assert.True(plan.MaximumTopologyLocality < 6d);
        Assert.Contains(cells, cell => cell.Provenance == SurfaceMeshCellProvenance.FeatureBand);
        Assert.Contains(cells, cell => cell.Provenance == SurfaceMeshCellProvenance.CoarseRemainder);
    }

    [Fact]
    public void CloseCircularFeatures_ResolveWithoutOverlappingBands_AndRemainDeterministic()
    {
        var first = BuildCloseHolePlan();
        var second = BuildCloseHolePlan();
        Assert.True(first.Success, first.Failure);
        Assert.True(second.Success, second.Failure);
        Assert.Contains(first.Plan.Bands, band => band.CollisionResolution is "ClearanceClamped" || band.CollisionResolution.Contains("Shrunk", StringComparison.Ordinal));
        Assert.Equal(first.Cells.Select(CellKey), second.Cells.Select(CellKey));
        Assert.Equal(first.Plan.Bands.Select(band => (band.LoopId, band.BandWidth, band.CollisionResolution)),
            second.Plan.Bands.Select(band => (band.LoopId, band.BandWidth, band.CollisionResolution)));
    }

    private static (bool Success, IReadOnlyList<SurfaceMeshCell> Cells, PlanarFeatureDecompositionPlan Plan, string? Failure) BuildCloseHolePlan()
    {
        var vertices = new Dictionary<int, SurfaceMeshVertex>(); var next = 0;
        int Add(double u, double v) { var id = next++; vertices[id] = new SurfaceMeshVertex(id, new Point3D(u, v, 0d), u, v); return id; }
        var outerIds = new[] { Add(0d, 0d), Add(40d, 0d), Add(40d, 24d), Add(0d, 24d) };
        var a = Circle(Add, 17d, 12d, 4d, 24); var b = Circle(Add, 25.5d, 12d, 4d, 24);
        var loops = new[]
        {
            Loop(20, outerIds, vertices, Spans(outerIds, CurveGeometryKind.Line3, 400)),
            Loop(21, a, vertices, [new SurfaceMeshBoundarySpan(new EdgeId(500), a[0], a[0], CurveGeometryKind.Circle3, a.Length)]),
            Loop(22, b, vertices, [new SurfaceMeshBoundarySpan(new EdgeId(501), b[0], b[0], CurveGeometryKind.Circle3, b.Length)])
        };
        var plane = new PlaneSurface(Point3D.Origin, Direction3D.Create(new Vector3D(0d, 0d, 1d)), Direction3D.Create(new Vector3D(1d, 0d, 0d)));
        var mutable = vertices.Values.OrderBy(vertex => vertex.Id).ToList();
        var success = PlanarFeatureBandPlanner.TryPlan(PlanarDomainPlanner.Create(loops), plane, mutable, ref next, true, out var cells, out var plan, out var failure);
        return (success, cells, plan, failure);
    }

    private static string CellKey(SurfaceMeshCell cell) => $"{cell.Kind}:{cell.Provenance}:{string.Join(',', cell.VertexIds)}";

    private static SurfaceMeshTrimLoop Loop(int loopId, IReadOnlyList<int> ids, IReadOnlyDictionary<int, SurfaceMeshVertex> vertices, IReadOnlyList<SurfaceMeshBoundarySpan>? spans = null)
    {
        var local = ids.Select(id => (vertices[id].U!.Value, vertices[id].V!.Value)).ToArray();
        var area = Enumerable.Range(0, local.Length).Sum(index => (local[index].Item1 * local[(index + 1) % local.Length].Item2) - (local[(index + 1) % local.Length].Item1 * local[index].Item2)) * 0.5d;
        return new SurfaceMeshTrimLoop(new LoopId(loopId), ids, local, false, area, spans);
    }

    private static int[] Circle(Func<double, double, int> add, double x, double y, double radius, int segments)
        => Enumerable.Range(0, segments).Select(index =>
        {
            var angle = (2d * double.Pi * index) / segments;
            return add(x + (radius * double.Cos(angle)), y + (radius * double.Sin(angle)));
        }).ToArray();

    private static int[] RoundedSlot(Func<double, double, int> add, double x, double y, double halfLength, double radius, int arcSegments)
        => Enumerable.Range(0, arcSegments + 1).Select(index =>
        {
            var angle = -double.Pi / 2d + (double.Pi * index / arcSegments);
            return add(x + halfLength + radius * double.Cos(angle), y + radius * double.Sin(angle));
        }).Concat(Enumerable.Range(0, arcSegments + 1).Select(index =>
        {
            var angle = double.Pi / 2d + (double.Pi * index / arcSegments);
            return add(x - halfLength + radius * double.Cos(angle), y + radius * double.Sin(angle));
        })).ToArray();

    private static IReadOnlyList<SurfaceMeshBoundarySpan> Spans(IReadOnlyList<int> ids, CurveGeometryKind kind, int firstEdge)
        => Enumerable.Range(0, ids.Count).Select(index => new SurfaceMeshBoundarySpan(new EdgeId(firstEdge + index), ids[index], ids[(index + 1) % ids.Count], kind, 2)).ToArray();

    private static IReadOnlyList<SurfaceMeshBoundarySpan> SlotSpans(IReadOnlyList<int> ids, int firstEdge)
    {
        var half = ids.Count / 2;
        return
        [
            new SurfaceMeshBoundarySpan(new EdgeId(firstEdge), ids[0], ids[half - 1], CurveGeometryKind.Circle3, half),
            new SurfaceMeshBoundarySpan(new EdgeId(firstEdge + 1), ids[half - 1], ids[half], CurveGeometryKind.Line3, 2),
            new SurfaceMeshBoundarySpan(new EdgeId(firstEdge + 2), ids[half], ids[^1], CurveGeometryKind.Circle3, half),
            new SurfaceMeshBoundarySpan(new EdgeId(firstEdge + 3), ids[^1], ids[0], CurveGeometryKind.Line3, 2)
        ];
    }
}
