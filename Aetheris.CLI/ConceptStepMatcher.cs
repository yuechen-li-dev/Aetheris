using System.Globalization;
using System.Text.RegularExpressions;
using Aetheris.Kernel.Core.Brep;
using Aetheris.Kernel.Core.Geometry;
using Aetheris.Kernel.Core.Geometry.Surfaces;
using Aetheris.Kernel.Core.Math;
using Aetheris.Kernel.Core.Step242;
using Aetheris.Kernel.Firmament.FirmamentV2;

namespace Aetheris.CLI;

/// <summary>Read-only, bounded comparison of resolved Concept IR against analytic STEP evidence.</summary>
public enum ConceptStepMatchStatus { Matched, WithinTolerance, Candidate, Ambiguous, Conflicted, Missing, Unverifiable, Unsupported }
public enum ConceptStepOverallStatus { Matched, Partial, Conflicted, InvalidConcept, InvalidStep, Unsupported }
public enum ConceptStepEvidenceQuality { ExactAnalytic, DerivedAnalytic, TopologySupported, TessellatedApproximation, HeuristicCandidate, Unavailable }

public sealed record ConceptStepTolerance(double LinearMm = 0.01, double AngularDegrees = 0.1, double DimensionMm = 0.01);
public sealed record ConceptStepHoleRole(double? DiameterMm, ConceptIrVector3? Axis, string? Kind);
public sealed record ConceptStepMatchRole(string Member, string Kind, ConceptStepHoleRole? Hole = null);
public sealed record ConceptStepPlaneEvidence(int FaceId, Point3D Origin, Vector3D Normal);
public sealed record ConceptStepHoleEvidence(int FaceId, Point3D Center, Vector3D Axis, double Radius, double MinExtent, double MaxExtent, bool Through);
public sealed record ConceptStepEvidenceIndex(BoundingBox3D Bounds, IReadOnlyList<ConceptStepPlaneEvidence> Planes, IReadOnlyList<ConceptStepHoleEvidence> Cylinders);
public sealed record ConceptStepMemberResult(string Name, string Kind, ConceptStepMatchStatus Status, object Expected, object? Observed, double? Deviation, double? AllowedTolerance, int CandidateCount, ConceptStepEvidenceQuality EvidenceQuality, string Provenance, string? Note = null);
public sealed record ConceptStepMatchReport(ConceptStepOverallStatus Status, string ConceptStruct, string StepBody, ConceptStepTolerance Tolerances, object Summary, IReadOnlyList<ConceptStepMemberResult> Members, IReadOnlyList<string> Diagnostics);

public static class ConceptStepMatcher
{
    private static readonly Regex HoleRole = new(@"(?<member>[A-Za-z_][A-Za-z0-9_]*)\s+As\s+HoleCenters\s*\{(?<body>.*?)\}", RegexOptions.Singleline | RegexOptions.CultureInvariant);
    private static readonly Regex LengthField = new(@"\b(?<name>Diameter)\s*:\s*(?<value>[-+0-9.]+)mm", RegexOptions.CultureInvariant);
    private static readonly Regex AxisField = new(@"\bAxis\s*:\s*(?<value>[+-][XYZ])", RegexOptions.CultureInvariant);
    private static readonly Regex KindField = new(@"\bKind\s*:\s*(?<value>[A-Za-z_][A-Za-z0-9_]*)", RegexOptions.CultureInvariant);

    public static ConceptStepMatchReport Match(string stepPath, string conceptPath, ConceptStepTolerance? tolerances = null)
    {
        tolerances ??= new ConceptStepTolerance();
        var source = File.ReadAllText(conceptPath);
        var roles = ParseRoles(source);
        var parse = FirmamentV2Parser.Parse(StripMatchBlocks(source));
        if (!parse.IsSuccess || parse.Document?.ConceptIr is null)
            return new(ConceptStepOverallStatus.InvalidConcept, "<unresolved>", "body-0", tolerances, new { }, [], parse.Diagnostics);

        BrepBody body;
        try
        {
            var imported = Step242Importer.ImportBody(File.ReadAllText(stepPath));
            if (!imported.IsSuccess) return new(ConceptStepOverallStatus.InvalidStep, "<unresolved>", "body-0", tolerances, new { }, [], imported.Diagnostics.Select(d => d.Message).ToArray());
            body = imported.Value;
        }
        catch (Exception ex) { return new(ConceptStepOverallStatus.InvalidStep, "<unresolved>", "body-0", tolerances, new { }, [], [ex.Message]); }

        var evidence = BuildEvidence(body);
        var instance = parse.Document.ConceptIr.Structs.SingleOrDefault();
        if (instance is null) return new(ConceptStepOverallStatus.InvalidConcept, "<unresolved>", "body-0", tolerances, new { }, [], ["No Concept Struct was resolved."]);
        var members = new List<ConceptStepMemberResult>();
        foreach (var pair in instance.Members.OrderBy(p => p.Key, StringComparer.Ordinal))
        {
            var name = $"{instance.Name}.{pair.Key}";
            roles.TryGetValue(pair.Key, out var role);
            members.Add(MatchMember(name, pair.Value, role, evidence, tolerances));
        }
        var conflicted = members.Any(m => m.Status is ConceptStepMatchStatus.Conflicted or ConceptStepMatchStatus.Missing);
        var full = members.All(m => m.Status is ConceptStepMatchStatus.Matched or ConceptStepMatchStatus.WithinTolerance);
        var overall = conflicted ? ConceptStepOverallStatus.Conflicted : full ? ConceptStepOverallStatus.Matched : ConceptStepOverallStatus.Partial;
        var summary = new { matched = members.Count(m => m.Status == ConceptStepMatchStatus.Matched), withinTolerance = members.Count(m => m.Status == ConceptStepMatchStatus.WithinTolerance), candidate = members.Count(m => m.Status == ConceptStepMatchStatus.Candidate), ambiguous = members.Count(m => m.Status == ConceptStepMatchStatus.Ambiguous), conflicted = members.Count(m => m.Status == ConceptStepMatchStatus.Conflicted), missing = members.Count(m => m.Status == ConceptStepMatchStatus.Missing), unverifiable = members.Count(m => m.Status == ConceptStepMatchStatus.Unverifiable), unsupported = members.Count(m => m.Status == ConceptStepMatchStatus.Unsupported) };
        return new(overall, instance.Name, "body-0", tolerances, summary, members, ["STEP identifiers are diagnostic evidence only; they are not Firmament source contracts."]);
    }

    private static ConceptStepMemberResult MatchMember(string name, ConceptIrValue value, ConceptStepMatchRole? role, ConceptStepEvidenceIndex evidence, ConceptStepTolerance tol) => value switch
    {
        ConceptIrBox3Value box => MatchBox(name, box, evidence.Bounds, tol),
        ConceptIrPlaneValue plane => MatchPlane(name, plane, evidence.Planes, tol),
        ConceptIrAxisValue axis => MatchAxis(name, axis, evidence.Cylinders, tol),
        ConceptIrPointSetValue points when role?.Kind == "HoleCenters" => MatchHolePoints(name, points, role.Hole!, evidence.Cylinders, tol),
        ConceptIrPoint3Value => new(name, "Point3", ConceptStepMatchStatus.Unverifiable, value, null, null, null, 0, ConceptStepEvidenceQuality.Unavailable, value.Provenance, "Point3 requires an explicit semantic match role."),
        ConceptIrPointSetValue => new(name, "Point3[]", ConceptStepMatchStatus.Unverifiable, value, null, null, null, 0, ConceptStepEvidenceQuality.Unavailable, value.Provenance, "Point3[] requires `Match { Member As HoleCenters { ... } }`."),
        _ => new(name, value.Kind.ToString(), ConceptStepMatchStatus.Unsupported, value, null, null, null, 0, ConceptStepEvidenceQuality.Unavailable, value.Provenance, "This Concept value kind has no M5 STEP matcher.")
    };

    private static ConceptStepMemberResult MatchBox(string name, ConceptIrBox3Value expected, BoundingBox3D actual, ConceptStepTolerance tol)
    {
        var diffs = new[] { expected.Min.X - actual.Min.X, expected.Min.Y - actual.Min.Y, expected.Min.Z - actual.Min.Z, expected.Max.X - actual.Max.X, expected.Max.Y - actual.Max.Y, expected.Max.Z - actual.Max.Z };
        var deviation = diffs.Max(Math.Abs);
        var status = deviation <= tol.LinearMm ? (deviation == 0 ? ConceptStepMatchStatus.Matched : ConceptStepMatchStatus.WithinTolerance) : ConceptStepMatchStatus.Conflicted;
        return new(name, "Box3", status, expected, new { min = actual.Min, max = actual.Max, size = new[] { actual.Max.X - actual.Min.X, actual.Max.Y - actual.Min.Y, actual.Max.Z - actual.Min.Z } }, deviation, tol.LinearMm, 1, ConceptStepEvidenceQuality.TopologySupported, expected.Provenance);
    }

    private static ConceptStepMemberResult MatchPlane(string name, ConceptIrPlaneValue expected, IReadOnlyList<ConceptStepPlaneEvidence> candidates, ConceptStepTolerance tol)
    {
        var matching = candidates.Select(p => new { Plane = p, angle = Angle(expected.Normal, p.Normal), offset = Math.Abs((p.Origin - ToPoint(expected.Origin)).Dot(Normalize(expected.Normal))) })
            .Where(x => x.angle <= tol.AngularDegrees && x.offset <= tol.LinearMm).OrderBy(x => x.Plane.FaceId).ToArray();
        if (matching.Length == 0) return new(name, "Plane", ConceptStepMatchStatus.Conflicted, expected, candidates.Select(p => new { p.FaceId, p.Origin, p.Normal }).ToArray(), null, tol.LinearMm, 0, ConceptStepEvidenceQuality.ExactAnalytic, expected.Provenance, "No coplanar analytic face.");
        var status = matching.Length == 1 ? (matching[0].angle == 0 && matching[0].offset == 0 ? ConceptStepMatchStatus.Matched : ConceptStepMatchStatus.WithinTolerance) : ConceptStepMatchStatus.Ambiguous;
        return new(name, "Plane", status, expected, new { faceIds = matching.Select(x => x.Plane.FaceId).ToArray(), normalDeviationDeg = matching[0].angle, offsetMm = matching[0].offset }, Math.Max(matching[0].angle, matching[0].offset), tol.LinearMm, matching.Length, ConceptStepEvidenceQuality.ExactAnalytic, expected.Provenance);
    }

    private static ConceptStepMemberResult MatchAxis(string name, ConceptIrAxisValue expected, IReadOnlyList<ConceptStepHoleEvidence> candidates, ConceptStepTolerance tol)
    {
        var matches = candidates.Select(c => new { C = c, angle = Angle(expected.Direction, c.Axis), distance = DistanceToAxis(ToPoint(expected.Origin), c.Center, c.Axis) }).Where(x => x.angle <= tol.AngularDegrees && x.distance <= tol.LinearMm).OrderBy(x => x.C.FaceId).ToArray();
        if (matches.Length == 0) return new(name, "Axis", ConceptStepMatchStatus.Unverifiable, expected, null, null, tol.LinearMm, 0, ConceptStepEvidenceQuality.Unavailable, expected.Provenance, "No analytic cylindrical or revolution axis supports this reference axis.");
        var status = matches.Length == 1 ? ConceptStepMatchStatus.Matched : ConceptStepMatchStatus.Ambiguous;
        return new(name, "Axis", status, expected, new { faceIds = matches.Select(x => x.C.FaceId).ToArray(), reversedOrientationAllowed = true }, matches[0].distance, tol.LinearMm, matches.Length, ConceptStepEvidenceQuality.ExactAnalytic, expected.Provenance);
    }

    private static ConceptStepMemberResult MatchHolePoints(string name, ConceptIrPointSetValue expected, ConceptStepHoleRole role, IReadOnlyList<ConceptStepHoleEvidence> candidates, ConceptStepTolerance tol)
    {
        var usable = candidates.Where(c => (role.DiameterMm is null || Math.Abs(2 * c.Radius - role.DiameterMm.Value) <= tol.DimensionMm) && (role.Axis is null || Angle(role.Axis, c.Axis) <= tol.AngularDegrees) && (!string.Equals(role.Kind, "Through", StringComparison.OrdinalIgnoreCase) || c.Through)).OrderBy(c => c.FaceId).ToArray();
        var assignments = new List<object>(); var max = 0d; var used = new HashSet<int>();
        foreach (var point in expected.Points.OrderBy(p => p.Ordinal ?? int.MaxValue))
        {
            // A hole center declared on its entry plane is geometrically represented by the same cylinder axis,
            // not necessarily by the surface placement origin (which may be at the opposite entry plane).
            var ranked = usable.Where(c => !used.Contains(c.FaceId)).Select(c => new { C = c, d = DistanceToAxis(ToPoint(point.Point), c.Center, c.Axis) }).OrderBy(x => x.d).ThenBy(x => x.C.FaceId).ToArray();
            if (ranked.Length == 0) return new(name, "Point3[]", ConceptStepMatchStatus.Conflicted, expected, assignments, null, tol.LinearMm, usable.Length, ConceptStepEvidenceQuality.DerivedAnalytic, expected.Provenance, "Insufficient distinct hole candidates.");
            var best = ranked[0]; used.Add(best.C.FaceId); max = Math.Max(max, best.d); assignments.Add(new { ordinal = point.Ordinal, expected = point.Point, candidate = new { faceId = best.C.FaceId, center = best.C.Center, diameter = 2 * best.C.Radius, axis = best.C.Axis }, deviation = best.d });
        }
        var status = max <= tol.LinearMm && usable.Length == expected.Points.Count ? (max == 0 ? ConceptStepMatchStatus.Matched : ConceptStepMatchStatus.WithinTolerance) : max <= tol.LinearMm ? ConceptStepMatchStatus.Candidate : ConceptStepMatchStatus.Conflicted;
        return new(name, "Point3[]", status, new { points = expected.Points.Select(p => p.Point).ToArray(), role }, assignments, max, tol.LinearMm, usable.Length, ConceptStepEvidenceQuality.DerivedAnalytic, expected.Provenance);
    }

    private static ConceptStepEvidenceIndex BuildEvidence(BrepBody body)
    {
        var vertices = body.Topology.Vertices.Select(v => body.TryGetVertexPoint(v.Id, out var p) ? (Point3D?)p : null).Where(p => p.HasValue).Select(p => p!.Value).ToArray();
        if (vertices.Length == 0) throw new InvalidOperationException("STEP body has no vertex evidence for body bounds.");
        var bounds = new BoundingBox3D(new(vertices.Min(p => p.X), vertices.Min(p => p.Y), vertices.Min(p => p.Z)), new(vertices.Max(p => p.X), vertices.Max(p => p.Y), vertices.Max(p => p.Z)));
        var planes = new List<ConceptStepPlaneEvidence>(); var cylinders = new List<ConceptStepHoleEvidence>();
        foreach (var face in body.Topology.Faces.OrderBy(f => f.Id.Value)) if (body.TryGetFaceSurfaceGeometry(face.Id, out var surface) && surface is not null)
        {
            if (surface.Kind == SurfaceGeometryKind.Plane && surface.Plane is PlaneSurface p) planes.Add(new(face.Id.Value, p.Origin, p.Normal.ToVector()));
            if (surface.Kind == SurfaceGeometryKind.Cylinder && surface.Cylinder is CylinderSurface c)
            {
                var axis = c.Axis.ToVector(); var ts = vertices.Select(v => (v - c.Origin).Dot(axis)).ToArray(); var min = ts.Min(); var max = ts.Max();
                var through = Math.Abs(min - MinOnAxis(bounds, c.Origin, axis)) <= 0.01 && Math.Abs(max - MaxOnAxis(bounds, c.Origin, axis)) <= 0.01;
                cylinders.Add(new(face.Id.Value, c.Origin, axis, c.Radius, min, max, through));
            }
        }
        return new(bounds, planes, cylinders);
    }

    private static Dictionary<string, ConceptStepMatchRole> ParseRoles(string source)
    {
        var roles = new Dictionary<string, ConceptStepMatchRole>(StringComparer.Ordinal);
        foreach (Match match in HoleRole.Matches(source))
        {
            var body = match.Groups["body"].Value; var diameter = LengthField.Match(body); var axis = AxisField.Match(body); var kind = KindField.Match(body);
            roles[match.Groups["member"].Value] = new(match.Groups["member"].Value, "HoleCenters", new(diameter.Success ? double.Parse(diameter.Groups["value"].Value, CultureInfo.InvariantCulture) : null, axis.Success ? Axis(axis.Groups["value"].Value) : null, kind.Success ? kind.Groups["value"].Value : null));
        }
        return roles;
    }
    private static string StripMatchBlocks(string source) { var result = source; foreach (Match m in Regex.Matches(source, @"\bMatch\s*\{")) { var open = source.IndexOf('{', m.Index); var close = BraceClose(source, open); if (close >= 0) result = result.Remove(open, close - open + 1).Insert(open, new string(' ', close - open + 1)); } return result; }
    private static int BraceClose(string text, int open) { var d = 0; for (var i = open; i < text.Length; i++) { if (text[i] == '{') d++; else if (text[i] == '}' && --d == 0) return i; } return -1; }
    private static ConceptIrVector3 Axis(string value) => value switch { "+X" => new(1, 0, 0), "-X" => new(-1, 0, 0), "+Y" => new(0, 1, 0), "-Y" => new(0, -1, 0), "+Z" => new(0, 0, 1), _ => new(0, 0, -1) };
    private static Point3D ToPoint(ConceptIrPoint3 p) => new(p.X, p.Y, p.Z);
    private static Vector3D Normalize(ConceptIrVector3 v) => Normalize(new Vector3D(v.X, v.Y, v.Z));
    private static Vector3D Normalize(Vector3D v) { var l = Math.Sqrt(v.Dot(v)); return v * (1 / l); }
    private static double Angle(ConceptIrVector3 a, Vector3D b) => Angle(new Vector3D(a.X, a.Y, a.Z), b);
    private static double Angle(Vector3D a, Vector3D b) => Math.Acos(Math.Clamp(Math.Abs(Normalize(a).Dot(Normalize(b))), -1, 1)) * 180 / Math.PI;
    private static double Distance(Point3D a, Point3D b) => Math.Sqrt((a - b).Dot(a - b));
    private static double DistanceToAxis(Point3D p, Point3D origin, Vector3D axis) { var d = p - origin; var n = Normalize(axis); return Math.Sqrt((d - n * d.Dot(n)).Dot(d - n * d.Dot(n))); }
    private static double MinOnAxis(BoundingBox3D b, Point3D origin, Vector3D axis) => Corners(b).Min(p => (p - origin).Dot(axis));
    private static double MaxOnAxis(BoundingBox3D b, Point3D origin, Vector3D axis) => Corners(b).Max(p => (p - origin).Dot(axis));
    private static IEnumerable<Point3D> Corners(BoundingBox3D b) { foreach (var x in new[] { b.Min.X, b.Max.X }) foreach (var y in new[] { b.Min.Y, b.Max.Y }) foreach (var z in new[] { b.Min.Z, b.Max.Z }) yield return new(x, y, z); }
}
