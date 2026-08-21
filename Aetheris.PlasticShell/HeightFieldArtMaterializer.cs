using Aetheris.Kernel.Core.Brep;
using Aetheris.Kernel.Core.Geometry;
using Aetheris.Kernel.Core.Geometry.Curves;
using Aetheris.Kernel.Core.Geometry.Surfaces;
using Aetheris.Kernel.Core.Math;
using Aetheris.Kernel.Core.Step242;
using Aetheris.Kernel.Core.Topology;
using Aetheris.Surfacing;
using System.Security.Cryptography;
using System.Text;

namespace Aetheris.PlasticShell;

public sealed record HeightFieldArtExportResult(
    bool IsSuccess,
    string? Step,
    PlasticStepSurfaceInventory Inventory,
    int BoundaryFaces,
    IReadOnlyList<PlasticDiagnostic> Diagnostics);

/// <summary>
/// Explicitly non-manufacturing export of the former polar height-field experiment.
/// This is mathematical computer art, not a PlasticShell product realization.
/// </summary>
public static class PlasticShellHeightFieldArt
{
    public static HeightFieldArtExportResult Export(PlasticShellBodyState state, string productName)
    {
        ArgumentNullException.ThrowIfNull(state);
        var art = HeightFieldArtMaterializer.Generate(state.Intent, state.Evidence.RibNetwork);
        if (!art.IsSuccess || art.Body is null)
            return new(false, null, Empty(), 0, art.Diagnostics);

        var pmi = new Step242SemanticPmi[]
        {
            new Step242SemanticPmiNote(
                "height-field-art:happy-little-accident",
                "NonManufacturingArtwork",
                "Polar height-field mathematical computer art. Explicitly not manufacturable CAD and not a PlasticShell product definition.")
        };
        var exported = Step242Exporter.ExportBody(art.Body, pmi, new Step242ExportOptions
        {
            BrepExportPreflightMode = BrepExportPreflightMode.Enforce,
            ProductName = $"{productName}-height-field-art"
        });
        if (!exported.IsSuccess)
            return new(false, null, Empty(), art.Body.Topology.Faces.Count(), exported.Diagnostics.Select(d =>
                new PlasticDiagnostic("height-field-art-step-export-failed", PlasticDiagnosticSeverity.Error, d.Message)).ToArray());

        var step = exported.Value;
        var inventory = new PlasticStepSurfaceInventory(
            Count(step, "=PLANE("), Count(step, "=CYLINDRICAL_SURFACE("), Count(step, "=CONICAL_SURFACE("),
            Count(step, "=SPHERICAL_SURFACE("), Count(step, "=TOROIDAL_SURFACE("), Count(step, "=B_SPLINE_SURFACE_WITH_KNOTS("),
            Count(step, "RATIONAL_B_SPLINE_SURFACE"));
        return new(true, step, inventory, art.Body.Topology.Faces.Count(), []);
    }

    private static int Count(string text, string value) => (text.Length - text.Replace(value, string.Empty, StringComparison.Ordinal).Length) / value.Length;
    private static PlasticStepSurfaceInventory Empty() => new(0, 0, 0, 0, 0, 0, 0);
}

internal sealed record HeightFieldArtResult(
    bool IsSuccess,
    BrepBody? Body,
    MoldedMaterializationEvidence? Evidence,
    IReadOnlyList<PlasticDiagnostic> Diagnostics);

/// <summary>
/// Generates a polar height-field artwork from PlasticShell landmarks. This deliberately
/// preserves the old faceted experiment as an Easter egg. It must never be used as product
/// geometry or manufacturing evidence.
/// </summary>
internal static class HeightFieldArtMaterializer
{
    private const int AngularSegments = 96;
    private const int RadialSegments = 48;
    private const double Tol = 1e-9;
    private static readonly Direction3D PlusX = Direction3D.Create(new Vector3D(1, 0, 0));
    private static readonly Direction3D PlusZ = Direction3D.Create(new Vector3D(0, 0, 1));
    private static readonly Direction3D MinusZ = Direction3D.Create(new Vector3D(0, 0, -1));

    public static HeightFieldArtResult Generate(PlasticShellIr intent, AutoRibJudgmentEvidence? ribJudgment, BrepBody? preGraftBody = null)
    {
        if (intent.Standoffs.Count == 0 && ribJudgment?.SelectedCandidate is null)
            return new(true, null, null, []);

        var selected = ribJudgment?.Candidates.SingleOrDefault(c => c.CandidateId == ribJudgment.SelectedCandidate);
        var ribs = selected?.Edges ?? [];
        var k = (intent.Exterior.TopRadius - intent.Exterior.BottomRadius) / intent.Exterior.Height;
        var normalScale = double.Sqrt(1d + k * k);
        var innerBottomRadius = intent.Exterior.BottomRadius + k * intent.WallPolicy.NominalThickness - intent.WallPolicy.NominalThickness * normalScale;
        var innerTopRadius = intent.Exterior.TopRadius - intent.WallPolicy.NominalThickness * normalScale;
        var floor = intent.WallPolicy.NominalThickness;
        var ribHeight = intent.AutoRib is null ? 0d : (intent.AutoRib.Policy.MinimumHeight + intent.AutoRib.Policy.MaximumHeight) / 2d;
        var constructionDraft = double.Max(intent.MinimumDraftAngleDegrees + 1d, intent.AutoRib?.Policy.DraftAngleDegrees ?? 0d);

        HeightSample Sample(double x, double y)
        {
            var best = new HeightSample(floor, "InnerBottom");
            foreach (var standoff in intent.Standoffs)
            {
                var height = StandoffHeight(standoff, x, y, floor, constructionDraft, intent.AutoRib?.Policy.BaseBlendRadius ?? .6d);
                if (height > best.Z + Tol) best = new(height, $"Standoff:{standoff.StandoffId}");
            }
            foreach (var edge in ribs)
            {
                var a = intent.Standoffs.Single(s => s.StandoffId == edge.From);
                var b = intent.Standoffs.Single(s => s.StandoffId == edge.To);
                // A rib may merge into the annular boss but may never cap its core hole.
                if (intent.Standoffs.Any(s => s.HoleDiameter is { } hole && Distance(x, y, s.Position.X, s.Position.Y) < hole / 2d)) continue;
                var height = RibHeight(intent, a, b, x, y, floor, ribHeight, constructionDraft);
                if (height > best.Z + Tol) best = new(height, $"Rib:{edge.From}->{edge.To}");
            }
            return best;
        }

        try
        {
            var builder = new Builder(intent, innerBottomRadius, innerTopRadius, k, Sample);
            var built = builder.Build();
            var preflight = BrepExportPreflight.Validate(built.Body);
            if (!preflight.IsValid)
            {
                var detail = string.Join("; ", preflight.Diagnostics.Where(d => d.Severity == BrepExportPreflightSeverity.Error).Select(d => $"{d.Code}:{d.Context}"));
                return Failure($"Materialized one-body boundary failed BRep preflight: {detail}");
            }
            var beforeFingerprint = preGraftBody is null ? "not-available" : ExteriorFingerprint(preGraftBody);
            var afterFingerprint = ExteriorFingerprint(built.Body);
            if (preGraftBody is not null && !string.Equals(beforeFingerprint, afterFingerprint, StringComparison.Ordinal))
                return Failure("Independent pre/post analytic exterior fingerprint comparison failed.", PlasticDiagnosticCodes.MaterializedFeatureOutsideAuthorizedRegion);
            var featureEvidence = built.FeatureFaces.OrderBy(p => p.Key, StringComparer.Ordinal).Select(pair =>
            {
                var kind = pair.Key.StartsWith("Standoff:", StringComparison.Ordinal) ? "ArtisticAnnularPeak" : "ArtisticRidge";
                var standoff = kind == "ArtisticAnnularPeak" ? intent.Standoffs.Single(s => pair.Key == $"Standoff:{s.StandoffId}") : null;
                var edge = kind == "ArtisticRidge" ? ribs.Single(e => pair.Key == $"Rib:{e.From}->{e.To}") : null;
                var height = standoff?.Height ?? ribHeight;
                var baseThickness = standoff is null ? intent.WallPolicy.NominalThickness * intent.AutoRib!.Policy.ThicknessRatio : (standoff.OuterDiameter - (standoff.HoleDiameter ?? 0d)) / 2d;
                var topThickness = standoff is null
                    ? double.Max(.3d, baseThickness - 2d * (height - double.Min(intent.AutoRib!.Policy.BaseBlendRadius, height / 3d)) * double.Tan(constructionDraft * double.Pi / 180d))
                    : double.Max(.2d, baseThickness - height * double.Tan(constructionDraft * double.Pi / 180d));
                var envelope = standoff is not null
                    ? new SpatialInfluenceEnvelope(standoff.Position.X - standoff.OuterDiameter / 2d - (intent.AutoRib?.Policy.BaseBlendRadius ?? .6d), standoff.Position.Y - standoff.OuterDiameter / 2d - (intent.AutoRib?.Policy.BaseBlendRadius ?? .6d), floor,
                        standoff.Position.X + standoff.OuterDiameter / 2d + (intent.AutoRib?.Policy.BaseBlendRadius ?? .6d), standoff.Position.Y + standoff.OuterDiameter / 2d + (intent.AutoRib?.Policy.BaseBlendRadius ?? .6d), floor + height)
                    : RibEnvelope(intent, edge!, floor, height);
                return new MoldedFeatureEvidence(pair.Key, kind, pair.Value.Order().ToArray(), height, baseThickness, topThickness,
                    baseThickness / intent.WallPolicy.NominalThickness, envelope,
                    built.MinimumFeatureDraftDegrees.GetValueOrDefault(pair.Key, constructionDraft), PlasticEvidenceStrength.SampledConservative);
            }).ToArray();
            var junctions = BuildJunctionEvidence(intent, ribs);

            var evidence = new MoldedMaterializationEvidence(featureEvidence, junctions, 0d, beforeFingerprint, afterFingerprint, 1,
                built.Body.Topology.Shells.Count(), built.Body.Topology.Faces.Count(), false,
                $"Non-manufacturing polar height-field artwork ({AngularSegments} angular x {RadialSegments} radial cells). Former PlasticShell landmarks seed peaks and ridges; manufacturing gates intentionally do not apply.",
                PlasticEvidenceStrength.CertifiedBounded);
            return new(true, built.Body, evidence, []);
        }
        catch (Exception ex)
        {
            return Failure($"Bounded molded-feature construction failed: {ex.Message}");
        }

        HeightFieldArtResult Failure(string message, string code = PlasticDiagnosticCodes.MaterializationFailed) =>
            new(false, null, null, [new(code, PlasticDiagnosticSeverity.Error, message, intent.PlasticShellId)]);
    }

    private static string ExteriorFingerprint(BrepBody body)
    {
        var surfaces = body.Geometry.Surfaces.Select(p => p.Value).ToArray();
        var cone = surfaces.Where(s => s.Cone.HasValue).Select(s => s.Cone!.Value).OrderBy(c => c.Apex.Z).First();
        var planes = surfaces.Where(s => s.Plane.HasValue).Select(s => s.Plane!.Value).ToArray();
        var bottom = planes.OrderBy(p => p.Origin.Z).First();
        var rim = planes.OrderByDescending(p => p.Origin.Z).First();
        var canonical = $"outer-cone|{cone.Apex.X:R}|{cone.Apex.Y:R}|{cone.Apex.Z:R}|{cone.Axis.X:R}|{cone.Axis.Y:R}|{cone.Axis.Z:R}|{cone.SemiAngleRadians:R}|outer-bottom|{bottom.Origin.Z:R}|{bottom.Normal.X:R}|{bottom.Normal.Y:R}|{bottom.Normal.Z:R}|rim|{rim.Origin.Z:R}|{rim.Normal.X:R}|{rim.Normal.Y:R}|{rim.Normal.Z:R}";
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(canonical))).ToLowerInvariant();
    }

    private static SpatialInfluenceEnvelope RibEnvelope(PlasticShellIr intent, RibEdge edge, double floor, double height)
    {
        var a = intent.Standoffs.Single(s => s.StandoffId == edge.From).Position;
        var b = intent.Standoffs.Single(s => s.StandoffId == edge.To).Position;
        var half = intent.WallPolicy.NominalThickness * intent.AutoRib!.Policy.ThicknessRatio / 2d + intent.AutoRib.Policy.BaseBlendRadius;
        return new(double.Min(a.X, b.X) - half, double.Min(a.Y, b.Y) - half, floor, double.Max(a.X, b.X) + half, double.Max(a.Y, b.Y) + half, floor + height);
    }

    private static IReadOnlyList<MoldedJunctionEvidence> BuildJunctionEvidence(PlasticShellIr intent, IReadOnlyList<RibEdge> ribs)
    {
        var result = new List<MoldedJunctionEvidence>();
        foreach (var s in intent.Standoffs)
        {
            var incident = ribs.Where(e => e.From == s.StandoffId || e.To == s.StandoffId).ToArray();
            if (incident.Length == 0) continue;
            var bossWall = (s.OuterDiameter - (s.HoleDiameter ?? 0d)) / 2d;
            var ribContribution = incident.Length * intent.WallPolicy.NominalThickness * intent.AutoRib!.Policy.ThicknessRatio / 2d;
            var ratio = (bossWall + ribContribution) / intent.WallPolicy.NominalThickness;
            result.Add(new($"Junction:{s.StandoffId}", [s.StandoffId, .. incident.Select(e => $"{e.From}->{e.To}")], ratio, ratio <= 2d + Tol,
                $"Drafted max-envelope union with {intent.AutoRib.Policy.BaseBlendRadius:R} mm linear base flare; no coincident or internal interface faces."));
        }
        return result;
    }

    private static double StandoffHeight(PlasticStandoff s, double x, double y, double floor, double draftDegrees, double blend)
    {
        var d = Distance(x, y, s.Position.X, s.Position.Y);
        var h = s.Height;
        var blendHeight = double.Min(blend, h / 3d);
        var outerShoulder = s.OuterDiameter / 2d;
        var outerBase = outerShoulder + blend;
        var outerTop = double.Max((s.HoleDiameter ?? 0d) / 2d + .35d, outerShoulder - (h - blendHeight) * double.Tan(draftDegrees * double.Pi / 180d));
        double outer;
        if (d >= outerBase) outer = 0d;
        else if (d >= outerShoulder) outer = blendHeight * (outerBase - d) / (outerBase - outerShoulder);
        else if (d >= outerTop) outer = blendHeight + (h - blendHeight) * (outerShoulder - d) / (outerShoulder - outerTop);
        else outer = h;

        var holeTop = (s.HoleDiameter ?? 0d) / 2d;
        if (holeTop <= Tol) return floor + outer;
        var holeBottom = double.Max(.2d, holeTop - h * double.Tan(draftDegrees * double.Pi / 180d));
        var inner = d <= holeBottom ? 0d : d >= holeTop ? h : h * (d - holeBottom) / (holeTop - holeBottom);
        return floor + double.Min(outer, inner);
    }

    private static double RibHeight(PlasticShellIr intent, PlasticStandoff a, PlasticStandoff b, double x, double y, double floor, double h, double draftDegrees)
    {
        var policy = intent.AutoRib!.Policy;
        var shoulder = intent.WallPolicy.NominalThickness * policy.ThicknessRatio / 2d;
        var blendHeight = double.Min(policy.BaseBlendRadius, h / 3d);
        var baseHalf = shoulder + policy.BaseBlendRadius;
        var topHalf = double.Max(.15d, shoulder - (h - blendHeight) * double.Tan(draftDegrees * double.Pi / 180d));
        var d = DistanceToSegment(x, y, a.Position.X, a.Position.Y, b.Position.X, b.Position.Y);
        double rise;
        if (d >= baseHalf) rise = 0d;
        else if (d >= shoulder) rise = blendHeight * (baseHalf - d) / (baseHalf - shoulder);
        else if (d >= topHalf) rise = blendHeight + (h - blendHeight) * (shoulder - d) / (shoulder - topHalf);
        else rise = h;
        return floor + rise;
    }

    private static double Distance(double x1, double y1, double x2, double y2) => double.Hypot(x1 - x2, y1 - y2);
    private static double DistanceToSegment(double x, double y, double ax, double ay, double bx, double by)
    {
        var dx = bx - ax; var dy = by - ay; var l2 = dx * dx + dy * dy;
        var t = l2 <= Tol ? 0d : double.Clamp(((x - ax) * dx + (y - ay) * dy) / l2, 0d, 1d);
        return Distance(x, y, ax + t * dx, ay + t * dy);
    }

    private readonly record struct HeightSample(double Z, string FeatureId);
    private sealed record BuildResult(BrepBody Body, IReadOnlyDictionary<string, IReadOnlyList<int>> FeatureFaces, IReadOnlyDictionary<string, double> MinimumFeatureDraftDegrees);

    private sealed class Builder(PlasticShellIr intent, double innerBottom, double innerTop, double slope, Func<double, double, HeightSample> sample)
    {
        private readonly TopologyBuilder _topology = new();
        private readonly BrepGeometryStore _geometry = new();
        private readonly BrepBindingModel _bindings = new();
        private readonly Dictionary<VertexId, Point3D> _points = [];
        private readonly Dictionary<(int A, int B), EdgeId> _edges = [];
        private readonly Dictionary<string, List<int>> _featureFaces = new(StringComparer.Ordinal);
        private readonly Dictionary<string, double> _minimumFeatureDraft = new(StringComparer.Ordinal);

        public BuildResult Build()
        {
            var floor = intent.WallPolicy.NominalThickness;
            var rings = new VertexId[RadialSegments + 1, AngularSegments];
            var center = _topology.AddVertex(); _points[center] = new Point3D(0, 0, sample(0, 0).Z);
            for (var ring = 1; ring <= RadialSegments; ring++)
            {
                var radius = innerBottom * ring / RadialSegments;
                for (var i = 0; i < AngularSegments; i++)
                {
                    var angle = 2d * double.Pi * i / AngularSegments;
                    var x = radius * double.Cos(angle); var y = radius * double.Sin(angle);
                    var v = _topology.AddVertex(); _points[v] = new Point3D(x, y, ring == RadialSegments ? floor : sample(x, y).Z); rings[ring, i] = v;
                }
            }

            var boundary = new EdgeId[AngularSegments];
            for (var i = 0; i < AngularSegments; i++)
            {
                var next = (i + 1) % AngularSegments;
                var edge = _topology.AddEdge(rings[RadialSegments, i], rings[RadialSegments, next]);
                var angle = 2d * double.Pi * i / AngularSegments;
                AddCurve(edge, CurveGeometry.FromCircle(new Circle3Curve(new Point3D(0, 0, floor), PlusZ, innerBottom,
                    Direction3D.Create(new Vector3D(double.Cos(angle), double.Sin(angle), 0)))), 0, 2d * double.Pi / AngularSegments);
                _edges[Key(rings[RadialSegments, i], rings[RadialSegments, next])] = edge; boundary[i] = edge;
            }

            var terrainFaces = new List<FaceId>();
            for (var i = 0; i < AngularSegments; i++) terrainFaces.Add(AddTerrainFace(center, rings[1, i], rings[1, (i + 1) % AngularSegments]));
            for (var ring = 1; ring < RadialSegments; ring++)
            for (var i = 0; i < AngularSegments; i++)
            {
                var next = (i + 1) % AngularSegments;
                terrainFaces.Add(AddTerrainFace(rings[ring, i], rings[ring + 1, i], rings[ring + 1, next]));
                terrainFaces.Add(AddTerrainFace(rings[ring, i], rings[ring + 1, next], rings[ring, next]));
            }

            var rb = intent.Exterior.BottomRadius; var rt = intent.Exterior.TopRadius; var h = intent.Exterior.Height;
            var outerBottomVertex = AddPoint(rb, 0, 0); var outerTopVertex = AddPoint(rt, 0, h); var innerTopVertex = AddPoint(innerTop, 0, h);
            var outerBottomEdge = AddCircle(outerBottomVertex, rb, 0); var outerTopEdge = AddCircle(outerTopVertex, rt, h); var innerTopEdge = AddCircle(innerTopVertex, innerTop, h);
            var outerCone = AddFace([Forward([outerBottomEdge]), Reverse([outerTopEdge])]);
            var innerCone = AddFace([Forward(boundary), Reverse([innerTopEdge])]);
            var outerBottom = AddFace([Reverse([outerBottomEdge])]);
            var rim = AddFace([Forward([outerTopEdge]), Reverse([innerTopEdge])]);
            BindSurface(outerCone, SurfaceGeometry.FromCone(Cone(rb, slope)));
            BindSurface(innerCone, SurfaceGeometry.FromCone(Cone(rb - intent.WallPolicy.NominalThickness * double.Sqrt(1d + slope * slope), slope)), false);
            BindPlane(outerBottom, new Point3D(0, 0, 0), MinusZ);
            BindPlane(rim, new Point3D(0, 0, h), PlusZ);
            var faces = new List<FaceId> { outerCone, innerCone, outerBottom, rim }; faces.AddRange(terrainFaces);
            var shell = _topology.AddShell(faces); _topology.AddBody([shell]);
            return new(new BrepBody(_topology.Model, _geometry, _bindings, _points),
                _featureFaces.ToDictionary(p => p.Key, p => (IReadOnlyList<int>)p.Value), _minimumFeatureDraft);
        }

        private FaceId AddTerrainFace(VertexId a, VertexId b, VertexId c)
        {
            var face = AddFace([[UseEdge(a, b), UseEdge(b, c), UseEdge(c, a)]]);
            var pa = _points[a]; var pb = _points[b]; var pc = _points[c];
            var normal = Direction3D.Create((pb - pa).Cross(pc - pa));
            BindPlane(face, pa, normal);
            var cx = (pa.X + pb.X + pc.X) / 3d; var cy = (pa.Y + pb.Y + pc.Y) / 3d;
            var feature = sample(cx, cy).FeatureId;
            if (feature != "InnerBottom")
            {
                if (!_featureFaces.TryGetValue(feature, out var ids)) _featureFaces[feature] = ids = [];
                ids.Add(face.Value);
                var zRange = double.Max(pa.Z, double.Max(pb.Z, pc.Z)) - double.Min(pa.Z, double.Min(pb.Z, pc.Z));
                if (zRange > 1e-7)
                {
                    var draft = double.Asin(double.Clamp(double.Abs(normal.Z), 0d, 1d)) * 180d / double.Pi;
                    _minimumFeatureDraft[feature] = double.Min(_minimumFeatureDraft.GetValueOrDefault(feature, double.PositiveInfinity), draft);
                }
            }
            return face;
        }

        private Use UseEdge(VertexId start, VertexId end)
        {
            var key = Key(start, end);
            if (!_edges.TryGetValue(key, out var edge))
            {
                edge = _topology.AddEdge(start, end); _edges[key] = edge;
                var a = _points[start]; var b = _points[end]; var vector = b - a;
                AddCurve(edge, CurveGeometry.FromLine(new Line3Curve(a, Direction3D.Create(vector))), 0, vector.Length);
            }
            var modelEdge = _topology.Model.GetEdge(edge);
            return new(edge, modelEdge.StartVertexId != start);
        }

        private VertexId AddPoint(double x, double y, double z) { var v = _topology.AddVertex(); _points[v] = new Point3D(x, y, z); return v; }
        private EdgeId AddCircle(VertexId vertex, double radius, double z) { var e = _topology.AddEdge(vertex, vertex); AddCurve(e, CurveGeometry.FromCircle(new Circle3Curve(new Point3D(0, 0, z), PlusZ, radius, PlusX)), 0, 2d * double.Pi); return e; }
        private ConeSurface Cone(double intercept, double k) => new(new Point3D(0, 0, -intercept / k), k > 0 ? PlusZ : MinusZ, double.Atan(double.Abs(k)), PlusX);
        private void AddCurve(EdgeId edge, CurveGeometry curve, double start, double end) { var id = new CurveGeometryId(_geometry.Curves.Count() + 1); _geometry.AddCurve(id, curve); _bindings.AddEdgeBinding(new(edge, id, new ParameterInterval(start, end))); }
        private void BindSurface(FaceId face, SurfaceGeometry surface, bool sameSense = true) { var id = new SurfaceGeometryId(_geometry.Surfaces.Count() + 1); _geometry.AddSurface(id, surface); _bindings.AddFaceBinding(new(face, id, sameSense)); }
        private void BindPlane(FaceId face, Point3D origin, Direction3D normal) => BindSurface(face, SurfaceGeometry.FromPlane(new PlaneSurface(origin, normal, Reference(normal))));
        private static Direction3D Reference(Direction3D normal) => double.Abs(normal.X) < .9 ? Direction3D.Create(new Vector3D(1, 0, 0)) : Direction3D.Create(new Vector3D(0, 1, 0));
        private FaceId AddFace(IReadOnlyList<IReadOnlyList<Use>> loops) { var ids = new List<LoopId>(); foreach (var uses in loops) { var loop = _topology.AllocateLoopId(); var coedges = uses.Select(_ => _topology.AllocateCoedgeId()).ToArray(); for (var i = 0; i < coedges.Length; i++) _topology.AddCoedge(new Coedge(coedges[i], uses[i].Edge, loop, coedges[(i + 1) % coedges.Length], coedges[(i + coedges.Length - 1) % coedges.Length], uses[i].Reverse)); _topology.AddLoop(new Loop(loop, coedges)); ids.Add(loop); } return _topology.AddFace(ids); }
        private static Use[] Forward(IReadOnlyList<EdgeId> edges) => edges.Select(e => new Use(e, false)).ToArray();
        private static Use[] Reverse(IReadOnlyList<EdgeId> edges) => edges.Reverse().Select(e => new Use(e, true)).ToArray();
        private static (int, int) Key(VertexId a, VertexId b) => a.Value < b.Value ? (a.Value, b.Value) : (b.Value, a.Value);
        private readonly record struct Use(EdgeId Edge, bool Reverse);
    }
}
