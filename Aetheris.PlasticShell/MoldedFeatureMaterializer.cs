using System.Security.Cryptography;
using System.Text;
using Aetheris.Kernel.Core.Brep;
using Aetheris.Kernel.Core.Geometry;
using Aetheris.Kernel.Core.Geometry.Curves;
using Aetheris.Kernel.Core.Geometry.Surfaces;
using Aetheris.Kernel.Core.Math;
using Aetheris.Kernel.Core.Topology;
using Aetheris.Surfacing;

namespace Aetheris.PlasticShell;

internal sealed record MoldedFeatureMaterializationResult(bool IsSuccess, BrepBody? Body, MoldedMaterializationEvidence? Evidence, IReadOnlyList<PlasticDiagnostic> Diagnostics);

/// <summary>
/// Exact bounded B-rep graft for annular standoffs and constant-thickness wall ribs.
/// The cavity floor owns holes matching the connected feature footprint. Rib sides,
/// flat tops, boss cylinders, and floor share edges; no mesh or feature solid is used.
/// </summary>
internal static class MoldedFeatureMaterializer
{
    private const double Tol = 1e-8;

    public static MoldedFeatureMaterializationResult Materialize(PlasticShellIr intent, AutoRibJudgmentEvidence? ribJudgment, BrepBody? preGraftBody = null)
    {
        if (intent.Standoffs.Count == 0 && ribJudgment?.SelectedCandidate is null) return new(true, null, null, []);
        var selected = ribJudgment?.Candidates.SingleOrDefault(c => c.CandidateId == ribJudgment.SelectedCandidate);
        var ribs = selected?.Edges ?? [];
        var ribHeight = intent.AutoRib is null ? 0d : (intent.AutoRib.Policy.MinimumHeight + intent.AutoRib.Policy.MaximumHeight) / 2d;
        var ribThickness = intent.WallPolicy.NominalThickness;
        try
        {
            var built = new Builder(intent, ribs, ribHeight, ribThickness).Build();
            var preflight = BrepExportPreflight.Validate(built.Body);
            if (!preflight.IsValid)
            {
                var detail = string.Join("; ", preflight.Diagnostics.Where(d => d.Severity == BrepExportPreflightSeverity.Error).Select(d => $"{d.Code}:{d.Context}"));
                return Failure($"Exact molded-wall B-rep failed preflight: {detail}");
            }
            var beforeFingerprint = preGraftBody is null ? "not-available" : ExteriorFingerprint(preGraftBody);
            var afterFingerprint = ExteriorFingerprint(built.Body);
            if (preGraftBody is not null && beforeFingerprint != afterFingerprint)
                return Failure("Independent pre/post analytic exterior fingerprint comparison failed.", PlasticDiagnosticCodes.MaterializedFeatureOutsideAuthorizedRegion);

            var features = new List<MoldedFeatureEvidence>();
            foreach (var standoff in intent.Standoffs)
            {
                var key = $"Standoff:{standoff.StandoffId}";
                if (!built.FeatureFaces.TryGetValue(key, out var faces)) return Failure($"Standoff '{standoff.StandoffId}' produced no boundary faces.");
                var wall = (standoff.OuterDiameter - (standoff.HoleDiameter ?? 0d)) / 2d;
                features.Add(new(key, "AnalyticAnnularStandoff", faces, standoff.Height, wall, wall, wall / intent.WallPolicy.NominalThickness,
                    new(standoff.Position.X - standoff.OuterDiameter / 2d, standoff.Position.Y - standoff.OuterDiameter / 2d, intent.WallPolicy.NominalThickness,
                        standoff.Position.X + standoff.OuterDiameter / 2d, standoff.Position.Y + standoff.OuterDiameter / 2d, intent.WallPolicy.NominalThickness + standoff.Height),
                    0d, PlasticEvidenceStrength.ExactAnalytic));
            }
            foreach (var edge in ribs)
            {
                var key = $"Rib:{edge.From}->{edge.To}";
                if (!built.FeatureFaces.TryGetValue(key, out var faces)) return Failure($"Rib '{edge.From}->{edge.To}' produced no boundary faces.");
                features.Add(new(key, "ConstantThicknessWallRib", faces, ribHeight, ribThickness, ribThickness, 1d,
                    RibEnvelope(intent, edge, ribHeight, ribThickness), 0d, PlasticEvidenceStrength.ExactAnalytic));
            }
            var junctions = BuildJunctionEvidence(intent, ribs, ribThickness);
            if (junctions.Any(j => !j.WithinLimit)) return Failure("A molded junction exceeds the bounded 2.0x nominal-wall material-accumulation limit.", PlasticDiagnosticCodes.MaterialAccumulation);
            var evidence = new MoldedMaterializationEvidence(features, junctions, 0d, beforeFingerprint, afterFingerprint, 1,
                built.Body.Topology.Shells.Count(), built.Body.Topology.Faces.Count(), false,
                "Exact B-rep wall graft: analytic annular cylinders, constant-thickness planar rib sides, one flat planar top per rib, and a planar cavity floor with the connected union footprint removed. No product mesh and no separate feature solids.",
                PlasticEvidenceStrength.ExactAnalytic);
            return new(true, built.Body, evidence, []);
        }
        catch (Exception ex) { return Failure($"Exact molded-wall construction failed: {ex.Message}"); }

        MoldedFeatureMaterializationResult Failure(string message, string code = PlasticDiagnosticCodes.MaterializationFailed) =>
            new(false, null, null, [new(code, PlasticDiagnosticSeverity.Error, message, intent.PlasticShellId)]);
    }

    private static IReadOnlyList<MoldedJunctionEvidence> BuildJunctionEvidence(PlasticShellIr intent, IReadOnlyList<RibEdge> ribs, double ribThickness)
    {
        var result = new List<MoldedJunctionEvidence>();
        foreach (var s in intent.Standoffs)
        {
            var incident = ribs.Where(e => e.From == s.StandoffId || e.To == s.StandoffId).ToArray();
            if (incident.Length == 0) continue;
            var bossWall = (s.OuterDiameter - (s.HoleDiameter ?? 0d)) / 2d;
            var ratio = double.Max(bossWall, ribThickness) / intent.WallPolicy.NominalThickness;
            result.Add(new($"Junction:{s.StandoffId}", [s.StandoffId, .. incident.Select(e => $"{e.From}->{e.To}")], ratio, ratio <= 2d + Tol,
                "Exact chord-to-cylinder B-rep junction; disjoint angular openings do not stack wall sections and no coincident interface face is retained."));
        }
        return result;
    }

    private static SpatialInfluenceEnvelope RibEnvelope(PlasticShellIr intent, RibEdge edge, double height, double thickness)
    {
        var a = intent.Standoffs.Single(s => s.StandoffId == edge.From).Position; var b = intent.Standoffs.Single(s => s.StandoffId == edge.To).Position; var half = thickness / 2d;
        return new(double.Min(a.X, b.X) - half, double.Min(a.Y, b.Y) - half, intent.WallPolicy.NominalThickness,
            double.Max(a.X, b.X) + half, double.Max(a.Y, b.Y) + half, intent.WallPolicy.NominalThickness + height);
    }

    private static string ExteriorFingerprint(BrepBody body)
    {
        var surfaces = body.Geometry.Surfaces.Select(p => p.Value).ToArray();
        var cone = surfaces.Where(s => s.Cone.HasValue).Select(s => s.Cone!.Value).OrderBy(c => c.Apex.Z).First();
        var planes = surfaces.Where(s => s.Plane.HasValue).Select(s => s.Plane!.Value).ToArray(); var bottom = planes.OrderBy(p => p.Origin.Z).First(); var rim = planes.OrderByDescending(p => p.Origin.Z).First();
        var canonical = $"outer-cone|{cone.Apex.X:R}|{cone.Apex.Y:R}|{cone.Apex.Z:R}|{cone.Axis.X:R}|{cone.Axis.Y:R}|{cone.Axis.Z:R}|{cone.SemiAngleRadians:R}|outer-bottom|{bottom.Origin.Z:R}|{bottom.Normal.X:R}|{bottom.Normal.Y:R}|{bottom.Normal.Z:R}|rim|{rim.Origin.Z:R}|{rim.Normal.X:R}|{rim.Normal.Y:R}|{rim.Normal.Z:R}";
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(canonical))).ToLowerInvariant();
    }

    private sealed record BuildResult(BrepBody Body, IReadOnlyDictionary<string, IReadOnlyList<int>> FeatureFaces);

    private sealed class Builder
    {
        private static readonly Direction3D PlusX = Direction3D.Create(new Vector3D(1, 0, 0));
        private static readonly Direction3D PlusZ = Direction3D.Create(new Vector3D(0, 0, 1));
        private static readonly Direction3D MinusZ = Direction3D.Create(new Vector3D(0, 0, -1));
        private readonly PlasticShellIr _intent; private readonly IReadOnlyList<RibEdge> _ribs; private readonly double _ribHeight; private readonly double _ribThickness; private readonly double _floor;
        private readonly TopologyBuilder _topology = new(); private readonly BrepGeometryStore _geometry = new(); private readonly BrepBindingModel _bindings = new(); private readonly Dictionary<VertexId, Point3D> _points = [];
        private readonly Dictionary<string, List<int>> _featureFaces = new(StringComparer.Ordinal); private readonly Dictionary<string, Node> _nodes;
        private readonly Dictionary<(string Node, string Rib, bool End, bool Top), VertexId> _openingVertices = []; private readonly Dictionary<(string Node, string Rib, bool End), EdgeId> _verticalEdges = [];

        public Builder(PlasticShellIr intent, IReadOnlyList<RibEdge> ribs, double ribHeight, double ribThickness)
        {
            _intent = intent; _ribs = ribs; _ribHeight = ribHeight; _ribThickness = ribThickness; _floor = intent.WallPolicy.NominalThickness;
            _nodes = intent.Standoffs.ToDictionary(s => s.StandoffId, s => new Node(s), StringComparer.Ordinal);
            foreach (var edge in ribs)
            {
                var a = _nodes[edge.From]; var b = _nodes[edge.To]; var half = ribThickness / 2d;
                if (ribHeight <= 0 || ribHeight >= double.Min(a.Source.Height, b.Source.Height) - Tol) throw new InvalidOperationException($"Rib {edge.From}->{edge.To} must terminate below both standoff tops.");
                if (half >= a.Radius - Tol || half >= b.Radius - Tol) throw new InvalidOperationException($"Rib {edge.From}->{edge.To} is too thick for its standoff chord junction.");
                a.Openings.Add(new(edge, Math.Atan2(b.Y - a.Y, b.X - a.X), Math.Asin(half / a.Radius)));
                b.Openings.Add(new(edge, Math.Atan2(a.Y - b.Y, a.X - b.X), Math.Asin(half / b.Radius)));
            }
            foreach (var node in _nodes.Values) node.SortAndValidateOpenings();
        }

        public BuildResult Build()
        {
            var k = (_intent.Exterior.TopRadius - _intent.Exterior.BottomRadius) / _intent.Exterior.Height; var scale = double.Sqrt(1d + k * k);
            var innerBottom = _intent.Exterior.BottomRadius + k * _floor - _floor * scale; var innerTop = _intent.Exterior.TopRadius - _floor * scale;
            foreach (var node in _nodes.Values)
            foreach (var opening in node.Openings)
            foreach (var end in new[] { false, true })
            {
                var angle = end ? opening.End : opening.Start; var bottom = AddPoint(node.X + node.Radius * Math.Cos(angle), node.Y + node.Radius * Math.Sin(angle), _floor); var top = AddPoint(node.X + node.Radius * Math.Cos(angle), node.Y + node.Radius * Math.Sin(angle), _floor + _ribHeight);
                _openingVertices[(node.Id, opening.RibId, end, false)] = bottom; _openingVertices[(node.Id, opening.RibId, end, true)] = top; _verticalEdges[(node.Id, opening.RibId, end)] = AddLine(bottom, top);
            }

            var floorBoundary = new List<Segment>(); var topByNode = _nodes.Keys.ToDictionary(k => k, _ => new List<Segment>(), StringComparer.Ordinal);
            var retainedByNode = _nodes.Keys.ToDictionary(k => k, _ => new List<Band>(), StringComparer.Ordinal); var isolatedFloorCircles = new List<EdgeId>();
            foreach (var node in _nodes.Values)
            {
                if (node.Openings.Count == 0)
                {
                    var v = AddPoint(node.X + node.Radius, node.Y, _floor); var circle = AddCircle(v, node.X, node.Y, node.Radius, _floor); isolatedFloorCircles.Add(circle); BuildIsolatedStandoff(node, circle); continue;
                }
                for (var i = 0; i < node.Openings.Count; i++)
                {
                    var current = node.Openings[i]; var next = node.Openings[(i + 1) % node.Openings.Count]; var retainedStart = current.End; var retainedEnd = UnwrapAfter(next.Start, retainedStart);
                    var bottom = AddArc(node, current, true, false, next, false, false, retainedStart, retainedEnd, "retained");
                    var top = AddArc(node, current, true, true, next, false, true, retainedStart, retainedEnd, "retained");
                    floorBoundary.Add(bottom); topByNode[node.Id].Add(top); retainedByNode[node.Id].Add(new(bottom, top, current, next));
                    var openingArc = AddArc(node, next, false, true, next, true, true, next.Start, UnwrapAfter(next.End, next.Start), "opening"); topByNode[node.Id].Add(openingArc);
                }
            }

            var ribGeometry = new Dictionary<string, RibGeometry>(StringComparer.Ordinal);
            foreach (var rib in _ribs)
            {
                var id = RibId(rib); var a = _nodes[rib.From]; var b = _nodes[rib.To]; var ao = a.Openings.Single(o => o.RibId == id); var bo = b.Openings.Single(o => o.RibId == id);
                var baseLeft = AddLineSegment(V(a, ao, true, false), V(b, bo, false, false)); var baseRight = AddLineSegment(V(a, ao, false, false), V(b, bo, true, false));
                var topLeft = AddLineSegment(V(a, ao, true, true), V(b, bo, false, true)); var topRight = AddLineSegment(V(a, ao, false, true), V(b, bo, true, true));
                floorBoundary.Add(baseLeft); floorBoundary.Add(baseRight); ribGeometry[id] = new(baseLeft, baseRight, topLeft, topRight);
            }

            var rb = _intent.Exterior.BottomRadius; var rt = _intent.Exterior.TopRadius; var h = _intent.Exterior.Height;
            var outerBottomV = AddPoint(rb, 0, 0); var outerTopV = AddPoint(rt, 0, h); var innerBottomV = AddPoint(innerBottom, 0, _floor); var innerTopV = AddPoint(innerTop, 0, h);
            var outerBottomE = AddCircle(outerBottomV, 0, 0, rb, 0); var outerTopE = AddCircle(outerTopV, 0, 0, rt, h); var innerBottomE = AddCircle(innerBottomV, 0, 0, innerBottom, _floor); var innerTopE = AddCircle(innerTopV, 0, 0, innerTop, h);
            var outerCone = AddFace([Forward([outerBottomE]), Reverse([outerTopE])]); var innerCone = AddFace([Forward([innerBottomE]), Reverse([innerTopE])]); var outerBottomFace = AddFace([Reverse([outerBottomE])]); var rim = AddFace([Forward([outerTopE]), Reverse([innerTopE])]);
            BindSurface(outerCone, SurfaceGeometry.FromCone(Cone(rb, k))); BindSurface(innerCone, SurfaceGeometry.FromCone(Cone(rb - _floor * scale, k)), false); BindPlane(outerBottomFace, new(0, 0, 0), MinusZ); BindPlane(rim, new(0, 0, h), PlusZ);
            var floorLoops = new List<IReadOnlyList<Use>> { Forward([innerBottomE]) }; foreach (var cycle in TraverseCycles(floorBoundary)) floorLoops.Add(ReverseUses(cycle)); foreach (var circle in isolatedFloorCircles) floorLoops.Add(Reverse([circle]));
            var floorFace = AddFace(floorLoops); BindPlane(floorFace, new(0, 0, _floor), PlusZ); var faces = new List<FaceId> { outerCone, innerCone, outerBottomFace, rim, floorFace };

            foreach (var node in _nodes.Values)
            {
                if (node.Openings.Count == 0) { faces.AddRange(node.PendingFaces); continue; }
                foreach (var band in retainedByNode[node.Id])
                {
                    var face = AddFace([[new(band.Bottom.Edge, false), new(Vertical(node, band.Next, false), false), new(band.Top.Edge, true), new(Vertical(node, band.Current, true), true)]]);
                    BindSurface(face, SurfaceGeometry.FromCylinder(new CylinderSurface(new(node.X, node.Y, _floor), PlusZ, node.Radius, PlusX))); AddFeatureFace(node.FeatureId, face); faces.Add(face);
                }
                var ordered = OrderCircleSegments(topByNode[node.Id]); var topV = AddPoint(node.X + node.Radius, node.Y, _floor + node.Source.Height); var topE = AddCircle(topV, node.X, node.Y, node.Radius, _floor + node.Source.Height);
                var upper = AddFace([ordered.Select(s => new Use(s.Edge, false)).ToArray(), Reverse([topE])]); BindSurface(upper, SurfaceGeometry.FromCylinder(new CylinderSurface(new(node.X, node.Y, _floor), PlusZ, node.Radius, PlusX))); AddFeatureFace(node.FeatureId, upper); faces.Add(upper); faces.AddRange(BuildHoleAndTop(node, topE));
            }

            foreach (var rib in _ribs)
            {
                var id = RibId(rib); var a = _nodes[rib.From]; var b = _nodes[rib.To]; var ao = a.Openings.Single(o => o.RibId == id); var bo = b.Openings.Single(o => o.RibId == id); var g = ribGeometry[id];
                var left = AddFace([[new(g.BaseLeft.Edge, false), new(Vertical(b, bo, false), false), new(g.TopLeft.Edge, true), new(Vertical(a, ao, true), true)]]);
                var right = AddFace([[new(g.BaseRight.Edge, false), new(Vertical(b, bo, true), false), new(g.TopRight.Edge, true), new(Vertical(a, ao, false), true)]]); BindFacePlane(left); BindFacePlane(right);
                var bOpening = topByNode[b.Id].Single(s => s.Kind == "opening" && s.RibId == id); var aOpening = topByNode[a.Id].Single(s => s.Kind == "opening" && s.RibId == id);
                var top = AddFace([[new(g.TopLeft.Edge, false), new(bOpening.Edge, false), new(g.TopRight.Edge, true), new(aOpening.Edge, false)]]); BindPlane(top, _points[g.TopLeft.Start], PlusZ);
                foreach (var face in new[] { left, right, top }) AddFeatureFace($"Rib:{rib.From}->{rib.To}", face); faces.AddRange([left, right, top]);
            }
            var shell = _topology.AddShell(faces); _topology.AddBody([shell]); return new(new BrepBody(_topology.Model, _geometry, _bindings, _points), _featureFaces.ToDictionary(p => p.Key, p => (IReadOnlyList<int>)p.Value.Order().ToArray(), StringComparer.Ordinal));
        }

        private void BuildIsolatedStandoff(Node node, EdgeId floorCircle)
        {
            var topV = AddPoint(node.X + node.Radius, node.Y, _floor + node.Source.Height); var topE = AddCircle(topV, node.X, node.Y, node.Radius, _floor + node.Source.Height); var outer = AddFace([Forward([floorCircle]), Reverse([topE])]);
            BindSurface(outer, SurfaceGeometry.FromCylinder(new CylinderSurface(new(node.X, node.Y, _floor), PlusZ, node.Radius, PlusX))); AddFeatureFace(node.FeatureId, outer); node.PendingFaces.Add(outer); node.PendingFaces.AddRange(BuildHoleAndTop(node, topE));
        }

        private IReadOnlyList<FaceId> BuildHoleAndTop(Node node, EdgeId outerTop)
        {
            var result = new List<FaceId>(); var hole = node.HoleRadius;
            if (hole <= Tol) { var top = AddFace([Forward([outerTop])]); BindPlane(top, new(node.X, node.Y, _floor + node.Source.Height), PlusZ); AddFeatureFace(node.FeatureId, top); result.Add(top); return result; }
            var bottomV = AddPoint(node.X + hole, node.Y, _floor); var topV = AddPoint(node.X + hole, node.Y, _floor + node.Source.Height); var bottomE = AddCircle(bottomV, node.X, node.Y, hole, _floor); var topE = AddCircle(topV, node.X, node.Y, hole, _floor + node.Source.Height);
            var inner = AddFace([Forward([bottomE]), Reverse([topE])]); BindSurface(inner, SurfaceGeometry.FromCylinder(new CylinderSurface(new(node.X, node.Y, _floor), PlusZ, hole, PlusX)), false); var annulus = AddFace([Forward([outerTop]), Reverse([topE])]); BindPlane(annulus, new(node.X, node.Y, _floor + node.Source.Height), PlusZ); var holeFloor = AddFace([Forward([bottomE])]); BindPlane(holeFloor, new(node.X, node.Y, _floor), PlusZ);
            foreach (var f in new[] { inner, annulus, holeFloor }) AddFeatureFace(node.FeatureId, f); result.AddRange([inner, annulus, holeFloor]); return result;
        }

        private Segment AddArc(Node node, Opening startOpening, bool startAtEnd, bool topStart, Opening endOpening, bool endAtEnd, bool topEnd, double startAngle, double endAngle, string kind)
        {
            var start = V(node, startOpening, startAtEnd, topStart); var end = V(node, endOpening, endAtEnd, topEnd); var z = topStart ? _floor + _ribHeight : _floor; var edge = _topology.AddEdge(start, end);
            AddCurve(edge, CurveGeometry.FromCircle(new Circle3Curve(new(node.X, node.Y, z), PlusZ, node.Radius, Direction3D.Create(new Vector3D(Math.Cos(startAngle), Math.Sin(startAngle), 0)))), 0, endAngle - startAngle); return new(edge, start, end, kind, startOpening.RibId);
        }

        private IReadOnlyList<Segment> OrderCircleSegments(IReadOnlyList<Segment> segments)
        {
            var result = new List<Segment>(); var current = segments[0]; result.Add(current);
            while (result.Count < segments.Count) { var next = segments.FirstOrDefault(s => !result.Contains(s) && s.Start == current.End) ?? throw new InvalidOperationException("Standoff cylinder segmentation is not a closed ordered circle."); result.Add(next); current = next; }
            if (result[^1].End != result[0].Start) throw new InvalidOperationException("Standoff cylinder segmentation does not close."); return result;
        }

        private IReadOnlyList<IReadOnlyList<Use>> TraverseCycles(IReadOnlyList<Segment> segments)
        {
            var pending = new HashSet<EdgeId>(segments.Select(s => s.Edge)); var byVertex = segments.SelectMany(s => new[] { (s.Start, s), (s.End, s) }).GroupBy(x => x.Item1).ToDictionary(g => g.Key, g => g.Select(x => x.s).ToArray()); var cycles = new List<IReadOnlyList<Use>>();
            while (pending.Count > 0) { var first = segments.First(s => pending.Contains(s.Edge)); var start = first.Start; var current = start; var uses = new List<Use>(); do { var segment = byVertex[current].FirstOrDefault(s => pending.Contains(s.Edge)) ?? throw new InvalidOperationException("Feature footprint boundary is open or branched."); var reverse = segment.End == current; uses.Add(new(segment.Edge, reverse)); pending.Remove(segment.Edge); current = reverse ? segment.Start : segment.End; } while (current != start); cycles.Add(uses); }
            return cycles;
        }

        private VertexId V(Node node, Opening opening, bool end, bool top) => _openingVertices[(node.Id, opening.RibId, end, top)]; private EdgeId Vertical(Node node, Opening opening, bool end) => _verticalEdges[(node.Id, opening.RibId, end)];
        private static double UnwrapAfter(double value, double after) { while (value <= after + Tol) value += 2d * Math.PI; return value; } private static string RibId(RibEdge rib) => $"{rib.From}->{rib.To}";
        private Segment AddLineSegment(VertexId start, VertexId end) { var edge = AddLine(start, end); return new(edge, start, end, "line", string.Empty); }
        private VertexId AddPoint(double x, double y, double z) { var v = _topology.AddVertex(); _points[v] = new(x, y, z); return v; }
        private EdgeId AddLine(VertexId start, VertexId end) { var edge = _topology.AddEdge(start, end); var a = _points[start]; var b = _points[end]; var d = b - a; AddCurve(edge, CurveGeometry.FromLine(new Line3Curve(a, Direction3D.Create(d))), 0, d.Length); return edge; }
        private EdgeId AddCircle(VertexId vertex, double cx, double cy, double radius, double z) { var edge = _topology.AddEdge(vertex, vertex); AddCurve(edge, CurveGeometry.FromCircle(new Circle3Curve(new(cx, cy, z), PlusZ, radius, PlusX)), 0, 2d * Math.PI); return edge; }
        private ConeSurface Cone(double intercept, double slope) => new(new Point3D(0, 0, -intercept / slope), slope > 0 ? PlusZ : MinusZ, Math.Atan(Math.Abs(slope)), PlusX);
        private void AddCurve(EdgeId edge, CurveGeometry curve, double start, double end) { var id = new CurveGeometryId(_geometry.Curves.Count() + 1); _geometry.AddCurve(id, curve); _bindings.AddEdgeBinding(new(edge, id, new(start, end))); }
        private void BindSurface(FaceId face, SurfaceGeometry surface, bool sameSense = true) { var id = new SurfaceGeometryId(_geometry.Surfaces.Count() + 1); _geometry.AddSurface(id, surface); _bindings.AddFaceBinding(new(face, id, sameSense)); }
        private void BindPlane(FaceId face, Point3D origin, Direction3D normal) => BindSurface(face, SurfaceGeometry.FromPlane(new PlaneSurface(origin, normal, Math.Abs(normal.X) < .9 ? PlusX : Direction3D.Create(new Vector3D(0, 1, 0)))));
        private void BindFacePlane(FaceId face) { var loop = _topology.Model.GetLoop(_topology.Model.GetFace(face).LoopIds[0]); var vertices = loop.CoedgeIds.Select(id => _topology.Model.GetEdge(_topology.Model.GetCoedge(id).EdgeId).StartVertexId).Distinct().Take(3).ToArray(); var a = _points[vertices[0]]; var b = _points[vertices[1]]; var c = _points[vertices[2]]; BindPlane(face, a, Direction3D.Create((b - a).Cross(c - a))); }
        private FaceId AddFace(IReadOnlyList<IReadOnlyList<Use>> loops) { var ids = new List<LoopId>(); foreach (var uses in loops) { var loop = _topology.AllocateLoopId(); var coedges = uses.Select(_ => _topology.AllocateCoedgeId()).ToArray(); for (var i = 0; i < coedges.Length; i++) _topology.AddCoedge(new Coedge(coedges[i], uses[i].Edge, loop, coedges[(i + 1) % coedges.Length], coedges[(i + coedges.Length - 1) % coedges.Length], uses[i].Reverse)); _topology.AddLoop(new Loop(loop, coedges)); ids.Add(loop); } return _topology.AddFace(ids); }
        private void AddFeatureFace(string id, FaceId face) { if (!_featureFaces.TryGetValue(id, out var faces)) _featureFaces[id] = faces = []; faces.Add(face.Value); }
        private static Use[] Forward(IReadOnlyList<EdgeId> edges) => edges.Select(e => new Use(e, false)).ToArray(); private static Use[] Reverse(IReadOnlyList<EdgeId> edges) => edges.Reverse().Select(e => new Use(e, true)).ToArray(); private static IReadOnlyList<Use> ReverseUses(IReadOnlyList<Use> uses) => uses.Reverse().Select(u => new Use(u.Edge, !u.Reverse)).ToArray();

        private sealed class Node
        {
            public Node(PlasticStandoff source) { Source = source; Id = source.StandoffId; X = source.Position.X; Y = source.Position.Y; Radius = source.OuterDiameter / 2d; HoleRadius = (source.HoleDiameter ?? 0d) / 2d; }
            public PlasticStandoff Source { get; } public string Id { get; } public string FeatureId => $"Standoff:{Id}"; public double X { get; } public double Y { get; } public double Radius { get; } public double HoleRadius { get; } public List<Opening> Openings { get; } = []; public List<FaceId> PendingFaces { get; } = [];
            public void SortAndValidateOpenings() { Openings.Sort((a, b) => a.NormalizedCenter.CompareTo(b.NormalizedCenter)); for (var i = 0; i < Openings.Count; i++) { var a = Openings[i]; var b = Openings[(i + 1) % Openings.Count]; var nextCenter = b.NormalizedCenter + (i == Openings.Count - 1 ? 2d * Math.PI : 0d); if (nextCenter - a.NormalizedCenter <= a.HalfAngle + b.HalfAngle + Tol) throw new InvalidOperationException($"Rib openings overlap at standoff '{Id}'."); } }
        }
        private sealed record Opening(RibEdge Rib, double Center, double HalfAngle) { public string RibId => Builder.RibId(Rib); public double NormalizedCenter { get { var a = Center % (2d * Math.PI); return a < 0 ? a + 2d * Math.PI : a; } } public double Start => NormalizedCenter - HalfAngle; public double End => NormalizedCenter + HalfAngle; }
        private sealed record Segment(EdgeId Edge, VertexId Start, VertexId End, string Kind, string RibId); private sealed record Band(Segment Bottom, Segment Top, Opening Current, Opening Next); private sealed record RibGeometry(Segment BaseLeft, Segment BaseRight, Segment TopLeft, Segment TopRight); private readonly record struct Use(EdgeId Edge, bool Reverse);
    }
}
