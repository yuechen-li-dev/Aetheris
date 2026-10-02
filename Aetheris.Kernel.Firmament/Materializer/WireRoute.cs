using System.Text.RegularExpressions;
using Aetheris.Kernel.Core.Math;
using Aetheris.Kernel.Core.Numerics;
using Aetheris.Kernel.Core.Results;
using Aetheris.Kernel.Firmament.Assembly;
using Aetheris.Kernel.StandardLibrary.Materials;

namespace Aetheris.Kernel.Firmament.Materializer;

/// <summary>Finite three-point route. Corner is a virtual intersection, not an exact-through point.</summary>
public sealed record WirePointRouteAir(string Name, double DiameterMm, string MaterialReference,
    Point3D Start, Point3D Corner, Point3D End, double MinimumBendRadiusMm, double RadiusMm, string CornerName = "Corner");

/// <summary>Deterministic fillet lowering to the existing exact WireForm sweep authority.</summary>
public static class WireRouteAuthoring
{
    private static readonly Regex Declaration = new(@"\bWireRoute\s+(?<name>[A-Za-z_]\w*)\s*\{", RegexOptions.CultureInvariant);
    public static bool IsSource(string source) => Declaration.IsMatch(source);

    public static KernelResult<WireFormFeatureAir> Parse(string source)
    {
        source = FirmamentV2.FirmamentSourceSpelling.Normalize(source);
        // Comments cannot contribute operations or fields.
        source = Regex.Replace(source, @"//[^\r\n]*", "");
        var declarations = Declaration.Matches(source);
        if (declarations.Count != 1) return Fail("declaration-invalid", "Exactly one concrete WireRoute is required.");
        if (Regex.IsMatch(source, @"\bWireForm\s+[A-Za-z_]\w*\s*\{"))
            return Fail("declaration-invalid", "WireRoute and WireForm cannot share a concrete materialization unit.");
        var declaration = declarations[0];
        var open = declaration.Index + declaration.Length - 1;
        var close = WireFormAuthoring.MatchingBrace(source, open);
        if (close < 0) return Fail("declaration-invalid", "Unclosed WireRoute.");
        var body = source[(open + 1)..close];
        var path = Block(ref body, @"Path");
        if (path is null) return Fail("path-invalid", "Path is required.");
        var fields = Fields(body, ["Diameter", "Material", "MinimumBendRadius"]);
        if (fields is null || !WireFormAuthoring.TryLength(fields.GetValueOrDefault("Diameter"), out var diameter) || diameter <= 0
            || !WireFormAuthoring.TryLength(fields.GetValueOrDefault("MinimumBendRadius"), out var minimum) || minimum <= 0)
            return Fail("dimensions-invalid", "Diameter and MinimumBendRadius require finite positive lengths; unknown/duplicate fields are rejected.");
        var steps = new List<(string Kind, string Name, string Body)>();
        while (!string.IsNullOrWhiteSpace(path))
        {
            var match = Regex.Match(path, @"^\s*(?<kind>Start|Corner|Follow|End)(?:\s+(?<name>[A-Za-z_]\w*))?\s*\{");
            if (!match.Success) return Fail("path-unsupported", "Expected Start/Corner/End or Start/Follow/End; Via and multi-corner routes are deferred.");
            var stepClose = WireFormAuthoring.MatchingBrace(path, match.Length - 1);
            if (stepClose < 0) return Fail("path-invalid", "Unclosed route step.");
            steps.Add((match.Groups["kind"].Value, match.Groups["name"].Value, path[match.Length..stepClose]));
            path = path[(stepClose + 1)..];
        }
        if (steps.Count == 3 && steps[0].Kind == "Start" && steps[1].Kind == "Follow" && steps[2].Kind == "End")
            return FollowLine(source, declaration.Groups["name"].Value, diameter, minimum,
                fields.GetValueOrDefault("Material") ?? "Standard.Materials.StainlessSteel.304_Annealed", steps);
        if (steps.Count != 3 || steps[0].Kind != "Start" || steps[1].Kind != "Corner" || steps[2].Kind != "End")
            return Fail("path-unsupported", "Exactly Start, Corner, End are supported.");
        var points = new Point3D[3]; double radius = 0;
        for (var i = 0; i < steps.Count; i++)
        {
            var step = steps[i].Body;
            var on = Block(ref step, "On");
            var values = Fields(step, i == 1 ? ["At", "Radius"] : ["At"]);
            if (values is null || !TryPoint(values.GetValueOrDefault("At"), on is not null, out var point))
                return Fail("point-invalid", "At requires Point3 of finite lengths, or Point2 with an explicit On frame.");
            if (on is not null)
            {
                // Share the checked placement parser and composition math; no second frame engine.
                var diagnostics = new List<AssemblyDiagnostic>();
                var frame = AssemblyFrameAuthoring.Parse("WireRoute.On", on, "From", "<wire-route>", diagnostics);
                if (frame is null || frame.From != "World") return Fail("frame-invalid", "On requires From: World; placed port references need a later assembly binding phase.");
                var transform = AssemblyFrameAuthoring.Compose(Transform3D.Identity, frame, diagnostics);
                if (diagnostics.Count != 0) return Fail("frame-invalid", string.Join("; ", diagnostics.Select(d => d.Message)));
                point = transform.Apply(point);
            }
            points[i] = point;
            if (i == 1 && (!WireFormAuthoring.TryLength(values!.GetValueOrDefault("Radius"), out radius) || radius < minimum))
                return Fail("radius-invalid", "Corner Radius must be finite and at least MinimumBendRadius.");
        }
        return Lower(new(declaration.Groups["name"].Value, diameter,
            fields.GetValueOrDefault("Material") ?? "Standard.Materials.StainlessSteel.304_Annealed", points[0], points[1], points[2], minimum, radius,
            string.IsNullOrEmpty(steps[1].Name) ? "Corner" : steps[1].Name));
    }

    private static KernelResult<WireFormFeatureAir> FollowLine(string source, string name, double diameter, double radius,
        string material, IReadOnlyList<(string Kind, string Name, string Body)> steps)
    {
        var startFields = Fields(steps[0].Body, ["At"]); var endFields = Fields(steps[2].Body, ["At"]);
        var follow = Fields(steps[1].Body, ["Curve", "From", "Distance", "Direction"]);
        if (startFields is null || endFields is null || follow is null
            || !TryPoint(startFields.GetValueOrDefault("At"), false, out var start)
            || !TryPoint(endFields.GetValueOrDefault("At"), false, out var end)
            || !WireFormAuthoring.TryLength(follow.GetValueOrDefault("From"), out var from)
            || !WireFormAuthoring.TryLength(follow.GetValueOrDefault("Distance"), out var distance) || distance <= 0)
            return Fail("guide-fields-invalid", "Follow requires Point3 endpoints and finite From/positive Distance lengths.");
        var reference = follow.GetValueOrDefault("Curve")?.Split('.');
        if (reference is not { Length: 2 } || reference.Any(p => !Regex.IsMatch(p, @"^[A-Za-z_]\w*$")))
            return Fail("guide-unresolved", "Curve requires a named Concept Struct Line3.");
        var owner = Regex.Matches(source, $@"\bConcept\s+Struct\s+{Regex.Escape(reference[0])}\s*\{{");
        if (owner.Count != 1) return Fail("guide-unresolved", "Concept guide owner is missing or ambiguous.");
        var close = WireFormAuthoring.MatchingBrace(source, owner[0].Index + owner[0].Length - 1);
        if (close < 0) return Fail("guide-unresolved", "Concept guide owner is unclosed.");
        var ownerBody = source[(owner[0].Index + owner[0].Length)..close];
        var guideBody = Block(ref ownerBody, "Line3\\s+" + Regex.Escape(reference[1]));
        var guide = guideBody is null ? null : Fields(guideBody, ["From", "To"]);
        if (guide is null || !TryPoint(guide.GetValueOrDefault("From"), false, out var a) || !TryPoint(guide.GetValueOrDefault("To"), false, out var b))
            return Fail("guide-unresolved", "Line3 requires finite Point3 From and To.");
        var axis = b - a;
        if (!double.IsFinite(axis.Length) || !axis.TryNormalize(out var direction)) return Fail("guide-degenerate", "Guide endpoints must be distinct.");
        var sense = follow.GetValueOrDefault("Direction", "Forward");
        if (sense is not ("Forward" or "Reverse")) return Fail("guide-direction-invalid", sense);
        var last = from + (sense == "Reverse" ? -distance : distance);
        if (from < 0 || from > axis.Length || last < 0 || last > axis.Length) return Fail("guide-interval-invalid", "The locked interval must lie entirely on the declared guide; no wrapping/clamping.");
        var entry = a + direction * from; var exit = a + direction * last;
        if (sense == "Reverse") direction = -direction;
        var tolerance = ToleranceContext.Default.Linear;
        var endOffset = end - exit;
        if (endOffset.Cross(direction).Length > tolerance || endOffset.Dot(direction) <= tolerance)
            return Fail("guide-exit-not-qualified", "The first guide lane requires an End beyond the interval along its outgoing tangent.");
        // A unique orthogonal projection determines the approach; no strategy search.
        var corner = entry + direction * (start - entry).Dot(direction);
        var approach = corner - start;
        if (approach.Length <= tolerance)
            return Fail("guide-approach-not-qualified", "The first lane requires a non-collinear approach; a wholly straight wire can use WireForm Straight.");
        if ((entry - corner).Dot(direction) < radius + tolerance)
            return Fail("guide-connector-space-insufficient", "The entry fillet must finish before the locked interval starts.");
        var route = Lower(new(name, diameter, material, start, corner, end, radius, radius, "GuideEntry"));
        if (!route.IsSuccess || route.Value is null) return route;
        var tail = (WireStraightAir)route.Value.Operations[^1];
        var firstLength = (entry - tail.Input.Position).Length;
        var enter = tail.Input with { Position = entry, AccumulatedLengthMm = tail.Input.AccumulatedLengthMm + firstLength };
        var leave = enter with { Position = exit, AccumulatedLengthMm = enter.AccumulatedLengthMm + distance };
        var finish = leave with { Position = end, AccumulatedLengthMm = leave.AccumulatedLengthMm + endOffset.Length };
        return KernelResult<WireFormFeatureAir>.Success(route.Value with { Operations =
            [route.Value.Operations[0], route.Value.Operations[1],
             new WireStraightAir("GuideApproach", 3, firstLength, tail.Input, enter),
             new WireStraightAir(string.IsNullOrEmpty(steps[1].Name) ? "Follow" : steps[1].Name, 4, distance, enter, leave),
             new WireStraightAir("GuideExit", 5, endOffset.Length, leave, finish)] });
    }

    public static KernelResult<WireFormFeatureAir> Lower(WirePointRouteAir route)
    {
        var tolerance = ToleranceContext.Default;
        if (!double.IsFinite(route.DiameterMm) || route.DiameterMm <= 0 || !double.IsFinite(route.MinimumBendRadiusMm)
            || route.MinimumBendRadiusMm <= 0 || !double.IsFinite(route.RadiusMm) || route.RadiusMm < route.MinimumBendRadiusMm
            || route.RadiusMm <= route.DiameterMm / 2 + tolerance.Linear)
            return Fail("radius-invalid", "Positive dimensions and centerline radius greater than wire radius are required.");
        if (new[] { route.Start, route.Corner, route.End }.Any(p => !double.IsFinite(p.X) || !double.IsFinite(p.Y) || !double.IsFinite(p.Z)))
            return Fail("point-invalid", "Route coordinates must be finite.");
        var lead = route.Corner - route.Start; var tail = route.End - route.Corner;
        if (!double.IsFinite(lead.Length) || !double.IsFinite(tail.Length)
            || !lead.TryNormalize(out var incoming) || !tail.TryNormalize(out var outgoing))
            return Fail("point-invalid", "Route legs must have distinct finite endpoints.");
        var cross = incoming.Cross(outgoing);
        if (!cross.TryNormalize(out var normal)) return Fail("corner-degenerate", "Collinear/reversing corners are unsupported; use WireForm Straight for a straight wire.");
        var angle = Math.Atan2(cross.Length, Math.Clamp(incoming.Dot(outgoing), -1, 1));
        var setback = route.RadiusMm * Math.Tan(angle / 2);
        if (!double.IsFinite(setback) || angle <= tolerance.Angular || Math.PI - angle <= tolerance.Angular
            || lead.Length - setback <= tolerance.Linear || tail.Length - setback <= tolerance.Linear)
            return Fail("corner-does-not-fit", "Bend tangent setbacks must fit both route legs with positive remaining straight lengths.");
        var material = new MaterialResolver().Resolve(route.MaterialReference.Trim('"'));
        if (!material.IsSuccess || material.Material is null) return Fail("material-unresolved", material.Message ?? "Unresolved material.");
        var tangent = Direction3D.Create(incoming); var up = Direction3D.Create(normal);
        var start = new WireState(route.Start, tangent, up, 0);
        var bendStart = start with { Position = route.Corner - incoming * setback, AccumulatedLengthMm = lead.Length - setback };
        var radial = Direction3D.Create(incoming.Cross(normal));
        var center = bendStart.Position - radial.ToVector() * route.RadiusMm;
        var bendEnd = new WireState(route.Corner + outgoing * setback, Direction3D.Create(outgoing), up,
            bendStart.AccumulatedLengthMm + route.RadiusMm * angle);
        var end = bendEnd with { Position = route.End, AccumulatedLengthMm = bendEnd.AccumulatedLengthMm + tail.Length - setback };
        return KernelResult<WireFormFeatureAir>.Success(new(route.Name, route.DiameterMm, route.MaterialReference, material.Material, start,
            [new WireStraightAir("Lead", 1, lead.Length - setback, start, bendStart),
             new WireBendAir(route.CornerName, 2, route.RadiusMm, angle, "Up", up, center, radial, bendStart, bendEnd),
             new WireStraightAir("Tail", 3, tail.Length - setback, bendEnd, end)], WireFormAuthoring.FrameTransportPolicy));
    }

    private static bool TryPoint(string? text, bool on, out Point3D point)
    {
        point = default;
        if (text is null) return false;
        var match = Regex.Match(text, @"^Point(?<dimension>2|3)\((?<values>[^()]*)\)$");
        if (!match.Success) return false;
        var dimension = int.Parse(match.Groups["dimension"].Value);
        if (dimension == 2 && !on) return false;
        var parts = match.Groups["values"].Value.Split(',');
        if (parts.Length != dimension) return false;
        var values = new double[3];
        for (var i = 0; i < dimension; i++) if (!WireFormAuthoring.TryLength(parts[i].Trim(), out values[i])) return false;
        point = new(values[0], values[1], values[2]); return true;
    }
    private static string? Block(ref string source, string header)
    {
        var matches = Regex.Matches(source, $@"\b{header}\s*\{{");
        if (matches.Count != 1) return null;
        var match = matches[0]; var close = WireFormAuthoring.MatchingBrace(source, match.Index + match.Length - 1);
        if (close < 0) return null;
        var body = source[(match.Index + match.Length)..close];
        source = source.Remove(match.Index, close - match.Index + 1); return body;
    }
    private static Dictionary<string, string>? Fields(string source, string[] allowed)
    {
        var result = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var field in source.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var colon = field.IndexOf(':');
            if (colon < 0 || !allowed.Contains(field[..colon].Trim()) || !result.TryAdd(field[..colon].Trim(), field[(colon + 1)..].Trim())) return null;
        }
        return result;
    }
    private static KernelResult<WireFormFeatureAir> Fail(string code, string message) => KernelResult<WireFormFeatureAir>.Failure([new(
        Core.Diagnostics.KernelDiagnosticCode.ValidationFailed, Core.Diagnostics.KernelDiagnosticSeverity.Error,
        $"wire-route-{code}: {message}", "FirmamentV2.WireRoute")]);
}
