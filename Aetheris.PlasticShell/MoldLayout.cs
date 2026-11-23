using Aetheris.Kernel.Core.Math;

namespace Aetheris.PlasticShell;

/// <summary>
/// Gates and ejector pins against the reviewed part. A gate lands on the part where its kind can reach it: an edge gate
/// on the parting line, a submarine gate on the −pull half, a pin or direct gate on the +pull half; never on an action's
/// face. An ejector pin seats square to the pull on the −pull half, its footprint and the policy's clearance around it
/// on faces flat to the pull at the pin's height.
/// </summary>
internal static class MoldLayout
{
    /// <summary>How far a declared point may lie from the part's mesh.</summary>
    public const double PointTolerance = 0.1;

    public sealed record Finding(string Code, bool Hard, bool Declaration, string Message, string Entity);

    public static (IReadOnlyList<MoldGateEvidence> Gates, IReadOnlyList<MoldEjectorEvidence> Ejectors, IReadOnlyList<Finding> Findings) Check(
        MoldIntent intent, MoldMeshRays mesh, double orientation, Vector3D pull, Func<int, MoldFaceSide> halfOf,
        IReadOnlySet<int> cosmetic, IReadOnlyDictionary<int, string> actionOf, IReadOnlyList<Point3D[]> partingPolylines,
        Func<int, Point3D, double?> toSilhouette, double chord)
    {
        var findings = new List<Finding>();
        var tolerance = PointTolerance + chord;
        static string Half(MoldFaceSide side) => side switch { MoldFaceSide.Positive => "+pull", MoldFaceSide.Negative => "−pull", _ => "neither" };

        var gates = new List<MoldGateEvidence>();
        foreach (var gate in intent.Gates ?? [])
        {
            var hit = mesh.Locate(gate.At);
            string? problem = null;
            // The parting line is the edges between the halves, and the silhouettes of faces it splits.
            double? toParting = partingPolylines.Count == 0 ? null : partingPolylines.Min(p => DistanceToPolyline(gate.At, p));
            if (hit is { } onFace && toSilhouette(onFace.Face, onFace.Point) is { } silhouette) toParting = toParting is null ? silhouette : double.Min(toParting.Value, silhouette);
            if (hit is null || hit.Value.Distance > tolerance) problem = $"is {hit?.Distance ?? double.PositiveInfinity:0.###} mm off the part";
            else if (actionOf.TryGetValue(hit.Value.Face, out var action)) problem = $"lands on face {hit.Value.Face}, formed by action {action}, which moves";
            else
            {
                var half = halfOf(hit.Value.Face);
                problem = gate.Kind switch
                {
                    MoldGateKind.Edge when toParting is null => "is an edge gate, and the part has no parting line",
                    MoldGateKind.Edge when toParting > 0.5 + chord => $"is an edge gate {toParting:0.###} mm from the parting line; an edge gate sits on it",
                    MoldGateKind.Submarine when half != MoldFaceSide.Negative => $"is a submarine gate on face {hit.Value.Face}, formed by the {Half(half)} half; it tunnels through the −pull half",
                    MoldGateKind.Pin or MoldGateKind.Direct when half != MoldFaceSide.Positive => $"is a {gate.Kind.ToString().ToLowerInvariant()} gate on face {hit.Value.Face}, formed by the {Half(half)} half; it feeds the +pull half",
                    _ => null,
                };
            }
            var onCosmetic = hit is { } h && h.Distance <= tolerance && cosmetic.Contains(h.Face);
            gates.Add(new(gate.Name, gate.Kind, hit?.Point ?? gate.At, hit is { } g && g.Distance <= tolerance ? g.Face : null,
                hit is { } s && s.Distance <= tolerance ? halfOf(s.Face) : null, hit?.Distance ?? double.PositiveInfinity, toParting, onCosmetic, problem));
            if (problem is not null) findings.Add(new(MoldDiagnosticCodes.GateMisplaced, true, true, $"Gate {gate.Name} {problem}.", $"gate:{gate.Name}"));
            else if (onCosmetic) findings.Add(new(MoldDiagnosticCodes.GateOnCosmetic, false, false,
                $"Gate {gate.Name} is on cosmetic face {hit!.Value.Face}; it leaves a vestige there.", $"gate:{gate.Name}"));
        }

        var ejectors = new List<MoldEjectorEvidence>();
        var (u, v) = Across(pull);
        foreach (var pin in intent.Ejectors ?? [])
        {
            var hit = mesh.Locate(pin.At);
            string? problem = null; var hard = true;
            var seats = new SortedSet<int>();
            if (hit is null || hit.Value.Distance > tolerance) problem = $"is {hit?.Distance ?? double.PositiveInfinity:0.###} mm off the part";
            else
            {
                var centre = hit.Value.Point;
                var normal = hit.Value.Normal * orientation;
                var half = halfOf(hit.Value.Face);
                var tilt = double.Acos(double.Clamp(-normal.Dot(pull), -1, 1)) * 180 / double.Pi;
                if (half != MoldFaceSide.Negative) problem = $"is on face {hit.Value.Face}, formed by the {Half(half)} half; pins push from the −pull half";
                else if (tilt > 1) { problem = $"is on face {hit.Value.Face}, {tilt:0.##}° off square to the pull; the pin would skid"; hard = false; }
                else
                {
                    // The pin's footprint, then the clearance ring around it, must lie on faces square to the pull at its height.
                    var radius = pin.Diameter / 2;
                    foreach (var (ring, what) in new[] { (radius, "its footprint"), (radius + intent.Policy.EjectorClearance, $"the {intent.Policy.EjectorClearance:0.###} mm clearance around it") })
                    {
                        for (var k = 0; k < 32 && problem is null; k++)
                        {
                            var a = 2 * double.Pi * k / 32;
                            var sample = centre + (u * double.Cos(a) + v * double.Sin(a)) * ring;
                            var at = mesh.Locate(sample);
                            if (at is null || at.Value.Distance > tolerance) { problem = $"has {what} running off the part"; hard = what == "its footprint"; break; }
                            var n = at.Value.Normal * orientation;
                            if (halfOf(at.Value.Face) != MoldFaceSide.Negative || -n.Dot(pull) < double.Cos(double.Pi / 180))
                            { problem = $"has {what} reaching face {at.Value.Face}, which is not square to the pull"; hard = what == "its footprint"; break; }
                            seats.Add(at.Value.Face);
                        }
                        if (problem is not null) break;
                    }
                }
            }
            var seatCosmetic = seats.Any(cosmetic.Contains) || hit is { } c && cosmetic.Contains(c.Face);
            ejectors.Add(new(pin.Name, hit?.Point ?? pin.At, pin.Diameter, hit is { } f && f.Distance <= tolerance ? f.Face : null, seats.ToArray(), seatCosmetic, problem));
            if (problem is not null) findings.Add(new(MoldDiagnosticCodes.EjectorMisplaced, hard, hard, $"Ejector {pin.Name} {problem}.", $"ejector:{pin.Name}"));
            else if (seatCosmetic) findings.Add(new(MoldDiagnosticCodes.EjectorOnCosmetic, false, false,
                $"Ejector {pin.Name} pushes on cosmetic face(s) {string.Join(", ", seats.Where(cosmetic.Contains))}; it leaves a witness mark there.", $"ejector:{pin.Name}"));
        }
        return (gates, ejectors, findings);
    }

    public static (Vector3D U, Vector3D V) Across(Vector3D pull)
    {
        var helper = double.Abs(pull.X) < 0.9 ? new Vector3D(1, 0, 0) : new Vector3D(0, 1, 0);
        var u = pull.Cross(helper); u /= u.Length;
        var v = pull.Cross(u); v /= v.Length;
        return (u, v);
    }

    private static double DistanceToPolyline(Point3D p, Point3D[] line)
    {
        var best = double.PositiveInfinity;
        for (var i = 0; i + 1 < line.Length; i++)
        {
            var d = line[i + 1] - line[i];
            var t = d.Dot(d) == 0 ? 0 : double.Clamp((p - line[i]).Dot(d) / d.Dot(d), 0, 1);
            best = double.Min(best, (line[i] + d * t - p).Length);
        }
        if (line.Length == 1) best = (line[0] - p).Length;
        return best;
    }
}
