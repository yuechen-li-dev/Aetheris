using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Serialization;
using Aetheris.Geometry;
using Aetheris.Kernel.Core.Brep.Tessellation;
using Aetheris.Kernel.Core.Geometry;
using Aetheris.Kernel.Core.Geometry.Curves;
using Aetheris.Kernel.Core.Geometry.Surfaces;
using Aetheris.Kernel.Core.Judgment;
using Aetheris.Kernel.Core.Math;
using Aetheris.Kernel.Core.Topology;

namespace Aetheris.Reconstruction;

public enum RecoveredSeamClassification { Internal, SourceOpenBoundary }
public enum RecoveredSeamOrientation { SameDirection, ReversedDirection }
public enum RecoveredSeamRepresentation { Line, NonRationalBSpline }

public sealed record SeamFitCandidateEvidence(
    string Representation,
    bool Admissible,
    double PositionResidual,
    double ComplexityPenalty,
    double Utility,
    string Reason);

/// <summary>
/// One pre-panel geometric authority for a connected chart-boundary trace.  The source mesh
/// supplies ordered geometric evidence, but both chart sides reference this single recovered
/// curve, parameter domain, orientation contract, and sample identity.
/// </summary>
public sealed record RecoveredSeam(
    string StableId,
    string ChartA,
    string? ChartB,
    RecoveredSeamClassification Classification,
    IReadOnlyList<int> SourceVertexIndices,
    IReadOnlyList<(int A, int B)> SourceEdges,
    IReadOnlyList<int> SourceTriangleProvenance,
    IReadOnlyList<Point3D> SourceBoundarySamples,
    RecoveredSeamRepresentation RepresentationKind,
    RecoveredSeamOrientation LeftOrientation,
    RecoveredSeamOrientation? RightOrientation,
    double ParameterStart,
    double ParameterEnd,
    bool IsClosed,
    double FitResidual,
    double NormalEvidenceDegrees,
    IReadOnlyList<SeamFitCandidateEvidence> JudgmentCandidates,
    string JudgmentWinner,
    string Authority,
    [property: JsonIgnore] BoundedParametricCurve3 Curve)
{
    public int SourceEdgeCount => SourceEdges.Count;
    public double G0Residual => 0d;
}

public sealed record RecoveredJunction(
    string StableId,
    int SourceVertexIndex,
    Point3D Point,
    IReadOnlyList<string> IncidentSeamIds,
    double SourceResidual,
    double IncidentTangentSpreadDegrees,
    string Authority);

public sealed record SourceBoundaryLoopCorrespondence(
    string StableId,
    IReadOnlyList<int> SourceVertexIndices,
    IReadOnlyList<string> RecoveredSeamIds,
    bool IntentionallyOpen,
    string Correspondence);

public sealed record RecoveredSeamNetwork(
    IReadOnlyList<RecoveredSeam> Seams,
    IReadOnlyList<RecoveredJunction> Junctions,
    IReadOnlyList<SourceBoundaryLoopCorrespondence> SourceBoundaryLoops);

public static class RecoveredSeamNetworkBuilder
{
    private sealed record EdgeUse(int Face, int From, int To);
    private sealed record RawEdge(int A, int B, string ChartA, string? ChartB, RecoveredSeamClassification Classification, IReadOnlyList<EdgeUse> Uses);
    private sealed record FitResult(BoundedParametricCurve3 Curve, RecoveredSeamRepresentation Kind, double Residual, IReadOnlyList<SeamFitCandidateEvidence> Candidates, string Winner);

    public static RecoveredSeamNetwork Build(TriangleSurfaceMesh mesh, IReadOnlyList<RecoveredChart> charts, IReadOnlyList<int> faceChart)
    {
        ArgumentNullException.ThrowIfNull(mesh);
        if (faceChart.Count != mesh.Triangles.Count) throw new ArgumentException("Every source triangle must have one chart assignment.", nameof(faceChart));
        return Build(mesh, faceChart.Select(index => charts[index].StableId).ToArray());
    }

    /// <summary>Fixture/tooling entry point when chart decomposition already exists as stable per-face identities.</summary>
    public static RecoveredSeamNetwork Build(TriangleSurfaceMesh mesh, IReadOnlyList<string> faceChartIds)
    {
        ArgumentNullException.ThrowIfNull(mesh);
        if (faceChartIds.Count != mesh.Triangles.Count) throw new ArgumentException("Every source triangle must have one chart assignment.", nameof(faceChartIds));

        var edgeUses = new SortedDictionary<(int A, int B), List<EdgeUse>>();
        for (var face = 0; face < mesh.Triangles.Count; face++)
        {
            foreach (var (from, to) in mesh.Triangles[face].DirectedEdges())
            {
                var key = from < to ? (from, to) : (to, from);
                if (!edgeUses.TryGetValue(key, out var uses)) edgeUses[key] = uses = [];
                uses.Add(new(face, from, to));
            }
        }

        var groups = new SortedDictionary<string, List<RawEdge>>(StringComparer.Ordinal);
        foreach (var (edge, uses) in edgeUses)
        {
            if (uses.Count == 2 && faceChartIds[uses[0].Face] != faceChartIds[uses[1].Face])
            {
                var ca = faceChartIds[uses[0].Face];
                var cb = faceChartIds[uses[1].Face];
                if (StringComparer.Ordinal.Compare(ca, cb) > 0) (ca, cb) = (cb, ca);
                Add($"I:{ca}:{cb}", new(edge.A, edge.B, ca, cb, RecoveredSeamClassification.Internal, uses));
            }
            else if (uses.Count == 1)
            {
                var ca = faceChartIds[uses[0].Face];
                Add($"B:{ca}", new(edge.A, edge.B, ca, null, RecoveredSeamClassification.SourceOpenBoundary, uses));
            }
        }

        var seams = new List<RecoveredSeam>();
        foreach (var (_, raw) in groups)
        {
            foreach (var path in Trace(raw))
            {
                var ordered = Canonicalize(path);
                var firstEdge = Find(raw, ordered[0], ordered[1]);
                var samples = ordered.Select(index => mesh.Vertices[index]).ToArray();
                var idPayload = $"{firstEdge.Classification}|{firstEdge.ChartA}|{firstEdge.ChartB}|{string.Join(',', ordered)}";
                var stableId = "seam-" + Hash(idPayload)[..16];
                var fit = Fit(samples, mesh.Bounds.Size.Length, stableId);
                // Canonical seam order is the left-side contract.  A consistently oriented
                // manifold transition necessarily traverses the same geometric edge in the
                // opposite direction on its right chart.
                var left = RecoveredSeamOrientation.SameDirection;
                RecoveredSeamOrientation? right = firstEdge.ChartB is null ? null : RecoveredSeamOrientation.ReversedDirection;
                var provenance = raw.Where(edge => ContainsPathEdge(ordered, edge.A, edge.B)).SelectMany(edge => edge.Uses.Select(use => use.Face)).Distinct().Order().ToArray();
                var edges = Enumerable.Range(0, ordered.Count - 1).Select(i => (ordered[i], ordered[i + 1])).ToArray();
                seams.Add(new(stableId, firstEdge.ChartA, firstEdge.ChartB, firstEdge.Classification, ordered, edges, provenance, samples,
                    fit.Kind, left, right, 0, 1, ordered[0] == ordered[^1], fit.Residual, NormalDiscontinuity(mesh, firstEdge), fit.Candidates,
                    fit.Winner, "one recovered curve evaluated in canonical t=[0,1]; all incident chart sides consume this identity", fit.Curve));
            }
        }
        seams.Sort((a, b) => StringComparer.Ordinal.Compare(a.StableId, b.StableId));

        var incident = new SortedDictionary<int, SortedSet<string>>();
        foreach (var seam in seams)
        {
            foreach (var vertex in seam.SourceVertexIndices.Distinct())
            {
                if (!incident.TryGetValue(vertex, out var ids)) incident[vertex] = ids = new(StringComparer.Ordinal);
                ids.Add(seam.StableId);
            }
        }
        var byId = seams.ToDictionary(seam => seam.StableId, StringComparer.Ordinal);
        var junctions = incident.Where(pair => pair.Value.Count >= 3 || pair.Value.Any(id => IsEndpoint(byId[id], pair.Key)) && pair.Value.Count >= 2)
            .Select(pair => new RecoveredJunction("junction-" + Hash(pair.Key + "|" + string.Join(',', pair.Value))[..16], pair.Key, mesh.Vertices[pair.Key], pair.Value.ToArray(), 0,
                TangentSpread(mesh.Vertices[pair.Key], pair.Value.Select(id => byId[id])), "one source-supported endpoint point shared by every incident recovered seam"))
            .OrderBy(junction => junction.StableId, StringComparer.Ordinal).ToArray();

        var validation = TriangleSurfaceValidator.Validate(mesh);
        var boundaryLoops = validation.BoundaryLoops.Select((loop, index) =>
        {
            var vertices = loop.ToHashSet();
            var ids = seams.Where(seam => seam.Classification == RecoveredSeamClassification.SourceOpenBoundary && seam.SourceEdges.Any(edge => vertices.Contains(edge.A) && vertices.Contains(edge.B)))
                .Select(seam => seam.StableId).Order(StringComparer.Ordinal).ToArray();
            return new SourceBoundaryLoopCorrespondence($"source-loop-{index:D2}", loop, ids, true, "source boundary edge membership; external side intentionally unmatched");
        }).ToArray();
        return new(seams, junctions, boundaryLoops);

        void Add(string key, RawEdge edge)
        {
            if (!groups.TryGetValue(key, out var list)) groups[key] = list = [];
            list.Add(edge);
        }
    }

    private static IReadOnlyList<IReadOnlyList<int>> Trace(IReadOnlyList<RawEdge> edges)
    {
        var unused = new SortedSet<(int A, int B)>(edges.Select(edge => (edge.A, edge.B)));
        var adjacency = new SortedDictionary<int, SortedSet<int>>();
        foreach (var edge in edges)
        {
            if (!adjacency.TryGetValue(edge.A, out var a)) adjacency[edge.A] = a = [];
            if (!adjacency.TryGetValue(edge.B, out var b)) adjacency[edge.B] = b = [];
            a.Add(edge.B); b.Add(edge.A);
        }
        var result = new List<IReadOnlyList<int>>();
        foreach (var start in adjacency.Where(pair => pair.Value.Count != 2).Select(pair => pair.Key).ToArray())
            while (adjacency[start].Any(next => unused.Contains(Key(start, next)))) result.Add(Walk(start));
        while (unused.Count > 0) result.Add(Walk(unused.Min.A));
        return result;

        IReadOnlyList<int> Walk(int start)
        {
            var path = new List<int> { start }; var previous = -1; var current = start;
            while (true)
            {
                var next = adjacency[current].Where(n => n != previous && unused.Contains(Key(current, n))).DefaultIfEmpty(-1).First();
                if (next < 0) break;
                unused.Remove(Key(current, next)); path.Add(next); previous = current; current = next;
                if (current == start || (path.Count > 1 && adjacency[current].Count != 2)) break;
            }
            return path;
        }
    }

    private static IReadOnlyList<int> Canonicalize(IReadOnlyList<int> path)
    {
        if (path.Count < 2) return path;
        if (path[0] == path[^1])
        {
            var core = path.Take(path.Count - 1).ToArray();
            var min = core.Min(); var candidates = new List<int[]>();
            foreach (var direction in new[] { core, core.Reverse().ToArray() })
            foreach (var index in Enumerable.Range(0, direction.Length).Where(i => direction[i] == min))
                candidates.Add(Enumerable.Range(0, direction.Length).Select(offset => direction[(index + offset) % direction.Length]).Append(min).ToArray());
            return candidates.OrderBy(candidate => string.Join(",", candidate), StringComparer.Ordinal).First();
        }
        var reverse = path.Reverse().ToArray();
        return Lexical(path, reverse) <= 0 ? path.ToArray() : reverse;
    }

    private static FitResult Fit(IReadOnlyList<Point3D> points, double scale, string seamId)
    {
        var parameters = ChordParameters(points);
        var lineAllowed = (points[0] - points[^1]).Length > 1e-14;
        var lineDelta = points[^1] - points[0];
        var line = lineAllowed ? BoundedParametricCurve3.Procedural(seamId + ":curve:line", new ParameterDomain1(0, 1),
            t => (points[0] + lineDelta * t, lineDelta), "shared source trace", representation: GeometryRepresentationKind.ProceduralParametric) : null;
        var lineResidual = lineAllowed ? points.Select((point, index) => (point - line!.Evaluate(parameters[index])).Length).Max() : double.PositiveInfinity;
        var tolerance = double.Max(1e-10, scale * 1e-4);
        var splineGeometry = PolylineSpline(points, parameters);
        var spline = BoundedParametricCurve3.FromCurveGeometry(seamId + ":curve:bspline", CurveGeometry.FromBSpline(splineGeometry), 0, 1, "shared source trace; non-rational degree-one spline");
        var context = new SeamChoiceContext(lineResidual, tolerance, points.Count);
        var candidates = new[]
        {
            new JudgmentCandidate<SeamChoiceContext>("Line", c => double.IsFinite(c.LineResidual) && c.LineResidual <= c.Tolerance, c => -(c.LineResidual / c.Tolerance) - .001,
                c => $"line residual {c.LineResidual:R} exceeds hard tolerance {c.Tolerance:R}", 0),
            new JudgmentCandidate<SeamChoiceContext>("NonRationalBSpline", _ => true, c => -.002 * c.SampleCount, null, 1)
        };
        var result = new JudgmentEngine<SeamChoiceContext>().Evaluate(context, candidates);
        if (!result.IsSuccess) throw new InvalidOperationException("No admissible recovered seam representation.");
        var winner = result.Selection!.Value.Candidate.Name;
        var evidence = candidates.Select(candidate =>
        {
            var admissible = candidate.IsAdmissible(context); var utility = admissible ? candidate.Score(context) : 0d;
            return new SeamFitCandidateEvidence(candidate.Name, admissible, candidate.Name == "Line" && double.IsFinite(lineResidual) ? lineResidual : 0,
                candidate.Name == "Line" ? .001 : .002 * points.Count, utility,
                admissible ? "finite curve, matching authoritative endpoints, bounded residual" : candidate.RejectionReason!(context));
        }).ToArray();
        return winner == "Line"
            ? new(line!, RecoveredSeamRepresentation.Line, lineResidual, evidence, winner)
            : new(spline, RecoveredSeamRepresentation.NonRationalBSpline, 0, evidence, winner);
    }

    private sealed record SeamChoiceContext(double LineResidual, double Tolerance, int SampleCount);

    private static BSpline3Curve PolylineSpline(IReadOnlyList<Point3D> input, IReadOnlyList<double> parameters)
    {
        var points = input.Count == 2 ? input : input.ToArray();
        var knots = parameters.ToArray();
        var multiplicities = Enumerable.Repeat(1, knots.Length).ToArray(); multiplicities[0] = 2; multiplicities[^1] = 2;
        return new(1, points, multiplicities, knots, "POLYLINE", false, false, "PIECEWISE_LINEAR");
    }

    private static double[] ChordParameters(IReadOnlyList<Point3D> points)
    {
        var values = new double[points.Count];
        for (var i = 1; i < points.Count; i++) values[i] = values[i - 1] + (points[i] - points[i - 1]).Length;
        if (values[^1] <= 1e-15) return Enumerable.Range(0, points.Count).Select(i => i / (double)(points.Count - 1)).ToArray();
        for (var i = 1; i < values.Length; i++) values[i] /= values[^1];
        return values;
    }

    private static RawEdge Find(IReadOnlyList<RawEdge> edges, int a, int b) => edges.Single(edge => Key(edge.A, edge.B) == Key(a, b));
    private static bool ContainsPathEdge(IReadOnlyList<int> path, int a, int b) => Enumerable.Range(0, path.Count - 1).Any(i => Key(path[i], path[i + 1]) == Key(a, b));
    private static (int A, int B) Key(int a, int b) => a < b ? (a, b) : (b, a);
    private static int Lexical(IReadOnlyList<int> a, IReadOnlyList<int> b) { for (var i = 0; i < a.Count; i++) { var c = a[i].CompareTo(b[i]); if (c != 0) return c; } return 0; }
    private static string Hash(string value) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value))).ToLowerInvariant();
    private static bool IsEndpoint(RecoveredSeam seam, int vertex) => !seam.IsClosed && (seam.SourceVertexIndices[0] == vertex || seam.SourceVertexIndices[^1] == vertex);
    private static double NormalDiscontinuity(TriangleSurfaceMesh mesh, RawEdge edge)
    {
        if (edge.Uses.Count != 2) return 0;
        Vector3D N(int face) { var t = mesh.Triangles[face]; var n = (mesh.Vertices[t.B] - mesh.Vertices[t.A]).Cross(mesh.Vertices[t.C] - mesh.Vertices[t.A]); return n.TryNormalize(out n) ? n : Vector3D.Zero; }
        return double.Acos(double.Clamp(N(edge.Uses[0].Face).Dot(N(edge.Uses[1].Face)), -1, 1)) * 180 / double.Pi;
    }
    private static double TangentSpread(Point3D point, IEnumerable<RecoveredSeam> seams)
    {
        var tangents = seams.Select(seam =>
        {
            var samples = seam.SourceBoundarySamples;
            var index = Enumerable.Range(0, samples.Count).OrderBy(i => (samples[i] - point).Length).First();
            var neighbor = index == 0 ? 1 : index == samples.Count - 1 ? samples.Count - 2 : index - 1;
            var vector = seam.SourceBoundarySamples[neighbor] - point; return vector.TryNormalize(out vector) ? vector : Vector3D.Zero;
        }).Where(vector => vector.Length > 0).ToArray();
        var max = 0d; for (var i = 0; i < tangents.Length; i++) for (var j = i + 1; j < tangents.Length; j++) max = double.Max(max, double.Acos(double.Clamp(double.Abs(tangents[i].Dot(tangents[j])), -1, 1)) * 180 / double.Pi);
        return max;
    }
}

public sealed record CanonicalReconstructionMesh(
    SurfaceMeshDocument Document,
    int InternalCrackGroups,
    int IntentionalOpenBoundaryLoops,
    int BoundaryEdgeCount,
    int NonManifoldEdgeCount,
    int QuadCount,
    int TriangleCount,
    string ValidationStatus,
    string DeterministicHash);

/// <summary>
/// Canonical topology-conforming lowering. Interior vertices sample recovered Panel supports;
/// seam vertices are evaluated once from RecoveredSeam and reused by both chart patches.
/// It deliberately retains source-cell adjacency as meshing evidence, not semantic geometry.
/// </summary>
public static class SeamAuthoritativeSurfaceMeshLowering
{
    public static CanonicalReconstructionMesh Lower(TriangleSurfaceMesh source, ChartNetwork network)
    {
        var chartByFace = new int[source.Triangles.Count];
        for (var chart = 0; chart < network.Charts.Count; chart++) foreach (var face in network.Charts[chart].SourceTriangles) chartByFace[face] = chart;
        var chartMembership = Enumerable.Range(0, source.Vertices.Count).Select(_ => new SortedSet<int>()).ToArray();
        for (var face = 0; face < source.Triangles.Count; face++) foreach (var vertex in Vertices(source.Triangles[face])) chartMembership[vertex].Add(chartByFace[face]);
        var positions = new Point3D[source.Vertices.Count];
        for (var vertex = 0; vertex < positions.Length; vertex++)
        {
            if (chartMembership[vertex].Count == 0) { positions[vertex] = source.Vertices[vertex]; continue; }
            var chart = network.Charts[chartMembership[vertex].First()]; var d = source.Vertices[vertex] - chart.Origin;
            var u = double.Clamp((d.Dot(chart.UAxis) - chart.UMin) / (chart.UMax - chart.UMin), 0, 1);
            var v = double.Clamp((d.Dot(chart.VAxis) - chart.VMin) / (chart.VMax - chart.VMin), 0, 1);
            positions[vertex] = chart.Patch.EvaluatePoint(u, v);
        }
        foreach (var seam in network.Seams)
        {
            var parameters = ChordParameters(seam.SourceBoundarySamples);
            for (var i = 0; i < seam.SourceVertexIndices.Count; i++) positions[seam.SourceVertexIndices[i]] = seam.Curve.Evaluate(parameters[i]);
        }

        var vertices = positions.Select((point, index) => new SurfaceMeshVertex(index + 1, point)).ToArray();
        var patches = new List<SurfacePatch>(); var quadCount = 0; var triangleCount = 0;
        for (var chartIndex = 0; chartIndex < network.Charts.Count; chartIndex++)
        {
            var chart = network.Charts[chartIndex]; var faces = chart.SourceTriangles.ToHashSet(); var unused = new SortedSet<int>(faces); var cells = new List<SurfaceMeshCell>();
            while (unused.Count > 0)
            {
                var face = unused.Min; unused.Remove(face); var triangle = source.Triangles[face];
                var mate = unused.Where(other => SharedVertexCount(triangle, source.Triangles[other]) == 2).DefaultIfEmpty(-1).First();
                if (mate >= 0 && TryQuad(triangle, source.Triangles[mate], out var quad))
                {
                    unused.Remove(mate); cells.Add(new QuadCell(quad.Select(id => id + 1).ToArray())); quadCount++;
                }
                else { cells.Add(new TriangleCell(Vertices(triangle).Select(id => id + 1).ToArray())); triangleCount++; }
            }
            var support = new SurfaceMeshSupport(SurfaceMeshSupportKind.Plane, new PlaneSurface(chart.Origin, Direction3D.Create(chart.Normal), Direction3D.Create(chart.UAxis)));
            patches.Add(new(new FaceId(chartIndex + 1), support, [], cells, true, "recovered-panel", ChartId: chart.StableId, PlanarPlannerPath: "seam-authoritative topology-conforming sampled Panel lowering"));
        }
        var boundaries = network.Seams.Select((seam, index) =>
        {
            var samples = seam.SourceVertexIndices.Select(id => vertices[id]).ToArray();
            var uses = new List<FaceBoundaryUse> { Use(seam.ChartA, false, index, 0) };
            if (seam.ChartB is not null) uses.Add(Use(seam.ChartB, seam.RightOrientation == RecoveredSeamOrientation.ReversedDirection, index, 1));
            return new SharedEdgeSamplePlan(new EdgeId(index + 1), seam.RepresentationKind == RecoveredSeamRepresentation.Line ? CurveGeometryKind.Line3 : CurveGeometryKind.BSpline3,
                new ParameterInterval(0, 1), samples, uses, seam.IsClosed, seam.FitResidual);
        }).ToArray();

        var topology = CellTopology(patches.SelectMany(patch => patch.Cells), vertices);
        var payload = string.Join('|', vertices.Select(v => $"{v.Id}:{v.Position.X:R},{v.Position.Y:R},{v.Position.Z:R}")) + ";" + string.Join('|', patches.SelectMany(p => p.Cells).Select(c => $"{c.Kind}:{string.Join(',', c.VertexIds)}"));
        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(payload))).ToLowerInvariant();
        var metrics = new SurfaceMeshMetrics(patches.Count, boundaries.Length, quadCount + triangleCount, quadCount, triangleCount, triangleCount, 0,
            network.Seams.Select(s => s.FitResidual).DefaultIfEmpty().Max(), 0, topology.MinLength, topology.MaxLength, hash, 0, topology.NonManifold, triangleCount + quadCount * 2);
        var document = new SurfaceMeshDocument(vertices, patches, boundaries, metrics);
        var valid = SurfaceMeshIrValidator.TryValidate(document, out var failure);
        return new(document, 0, network.SourceBoundaryLoops.Count, topology.Boundary, topology.NonManifold, quadCount, triangleCount,
            valid ? "Pass" : "Fail: " + failure, hash);

        FaceBoundaryUse Use(string chartId, bool reversed, int seamIndex, int side)
        {
            var chartIndex = network.Charts.ToList().FindIndex(chart => chart.StableId == chartId);
            return new(new FaceId(chartIndex + 1), new LoopId(chartIndex + 1), new CoedgeId(seamIndex * 2 + side + 1), reversed);
        }
    }

    private static int[] Vertices(Triangle triangle) => [triangle.A, triangle.B, triangle.C];
    private static int SharedVertexCount(Triangle a, Triangle b) => Vertices(a).Intersect(Vertices(b)).Count();
    private static bool TryQuad(Triangle a, Triangle b, out int[] quad)
    {
        var directed = a.DirectedEdges().Concat(b.DirectedEdges()).ToArray();
        var boundary = directed.Where(edge => directed.Count(other => Key(edge.A, edge.B) == Key(other.A, other.B)) == 1).ToArray();
        if (boundary.Length != 4) { quad = []; return false; }
        var ordered = new List<int> { boundary[0].A, boundary[0].B }; var current = boundary[0].B;
        while (ordered.Count < 4)
        {
            var next = boundary.FirstOrDefault(edge => edge.A == current && !ordered.Contains(edge.B));
            if (next == default) { quad = []; return false; }
            ordered.Add(next.B); current = next.B;
        }
        quad = ordered.ToArray(); return quad.Distinct().Count() == 4;
    }
    private static (int, int) Key(int a, int b) => a < b ? (a, b) : (b, a);
    private static double[] ChordParameters(IReadOnlyList<Point3D> points)
    {
        var values = new double[points.Count]; for (var i = 1; i < points.Count; i++) values[i] = values[i - 1] + (points[i] - points[i - 1]).Length;
        if (values[^1] <= 1e-15) return Enumerable.Range(0, points.Count).Select(i => i / (double)(points.Count - 1)).ToArray();
        for (var i = 1; i < values.Length; i++) values[i] /= values[^1]; return values;
    }
    private static (int Boundary, int NonManifold, double MinLength, double MaxLength) CellTopology(IEnumerable<SurfaceMeshCell> cells, IReadOnlyList<SurfaceMeshVertex> vertices)
    {
        var pointById = vertices.ToDictionary(vertex => vertex.Id, vertex => vertex.Position);
        var edges = new Dictionary<(int, int), (int Count, double Length)>();
        foreach (var cell in cells) for (var i = 0; i < cell.VertexIds.Count; i++)
        {
            var key = Key(cell.VertexIds[i], cell.VertexIds[(i + 1) % cell.VertexIds.Count]);
            edges[key] = (edges.GetValueOrDefault(key).Count + 1, (pointById[key.Item1] - pointById[key.Item2]).Length);
        }
        return (edges.Count(edge => edge.Value.Count == 1), edges.Count(edge => edge.Value.Count > 2), edges.Values.Min(edge => edge.Length), edges.Values.Max(edge => edge.Length));
    }
}
