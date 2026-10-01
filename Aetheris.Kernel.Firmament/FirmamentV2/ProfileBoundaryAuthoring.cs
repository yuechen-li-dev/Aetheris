using System.Globalization;
using System.Text.RegularExpressions;
using Aetheris.Kernel.Firmament.Materializer;

namespace Aetheris.Kernel.Firmament.FirmamentV2;

public sealed record ProfileBoundaryEditEvidence(string Name, string SourceMember, double From, double To,
    string Construction, IReadOnlyList<string> Descendants, string SourceSpan);

// Construction is a typed compile-time value, not an executable delegate. Future
// equation curves must pass their own purity/admissibility gate before producing
// this same bounded exact curve representation.
internal abstract record ProfileCurveConstruction;
internal sealed record ProfileThroughConstruction(IReadOnlyList<(double X, double Y)> Points,
    IReadOnlyList<(double X, double Y)>? Derivatives, (double X, double Y)? StartTangent,
    (double X, double Y)? EndTangent, bool MatchTangent) : ProfileCurveConstruction;

/// <summary>Immutable named edits into the ordinary resolved Profile authority.</summary>
internal static class ProfileBoundaryAuthoring
{
    private const double Tol = 1e-7;
    private sealed record Block(string Name, string Body, int Start, int Length);
    private sealed record Edit(string Name, int Segment, double From, double To,
        IReadOnlyList<(string Name, LineArcProfileCurve2D Curve)> Curves, string Construction, string SourceSpan,
        bool RequireTangentJoin = false);

    private static string WithoutComments(string text) => Regex.Replace(text, @"/\*.*?\*/|//[^\r\n]*", "", RegexOptions.Singleline);
    internal static bool IsDerived(string? body) => body is not null && Regex.IsMatch(WithoutComments(body), @"^\s*From\s*:");

    internal static bool TryResolve(string source, string name, HashSet<string> visiting,
        List<string> diagnostics, out ResolvedProfile2D? result)
    {
        result = null;
        var declaration = Regex.Match(source, $@"\bProfile\s+{Regex.Escape(name)}(?:\s+Using\s+(?<owner>\w+))?\s*\{{");
        if (!declaration.Success) return false;
        var body = ReadBody(source, declaration.Index + declaration.Length - 1, out _);
        if (body is null || !IsDerived(body)) return false;
        body = WithoutComments(body);
        var owner = declaration.Groups["owner"].Value;
        var replacements = Blocks(body, "Replace").ToArray();
        var remainder = body;
        foreach (var block in replacements.OrderByDescending(b => b.Start)) remainder = remainder.Remove(block.Start, block.Length);
        var applies = Regex.Matches(remainder, @"\bApply\s+(?<name>\w+)\s*;?").Select(m => m.Groups["name"].Value).ToArray();
        remainder = Regex.Replace(remainder, @"\bApply\s+\w+\s*;?", "");
        var header = Fields(remainder, ["From"], diagnostics, name);
        if (!header.TryGetValue("From", out var from) || !Regex.IsMatch(from, @"^\w+(?:\.\w+)?$"))
        { diagnostics.Add($"profile-edit-source-invalid:{name}"); return true; }
        var deltas = SemanticProfileDeltaParser.Parse(source);
        if (applies.Length > 0) diagnostics.AddRange(deltas.Diagnostics);
        // Existing edge authoring attaches ProfileDelta implicitly. A derivation
        // applies only explicitly selected programs, so resolve an unedited seed.
        var seedSource = source;
        ResolvedProfile2D? seed;
        IReadOnlyList<string> seedErrors;
        var localFrom = from.Contains('.') && from.StartsWith(owner + ".", StringComparison.Ordinal) ? from[(owner.Length + 1)..] : from;
        if (Regex.IsMatch(seedSource, $@"\bProfile\s+{Regex.Escape(localFrom)}\b"))
            seed = ProfileAuthoringParser.ResolveNamedProfileCore(seedSource, localFrom, visiting, out seedErrors);
        else
        {
            foreach (var delta in Blocks(source, "ProfileDelta").OrderByDescending(b => b.Start))
                seedSource = seedSource.Remove(delta.Start, delta.Length);
            var synthetic = "__BoundarySeed_" + name;
            while (Regex.IsMatch(seedSource, $@"\b{Regex.Escape(synthetic)}\b")) synthetic += "_";
            seed = ProfileAuthoringParser.ResolveNamedProfileCore(seedSource + $"\nProfile {synthetic} {{ Loop Outer {{ {localFrom} |> TraceLoop }} }}",
                synthetic, visiting, out seedErrors);
        }
        diagnostics.AddRange(seedErrors);
        if (seed is null) return true;
        if (seed.Loops.Count != 1) { diagnostics.Add($"profile-edit-loop-count-invalid:{name}"); return true; }
        var segments = seed.Loops[0].Segments;
        var edits = new List<Edit>();
        var ids = new HashSet<string>(StringComparer.Ordinal);
        foreach (var block in replacements)
        {
            if (!ids.Add(block.Name)) { diagnostics.Add($"profile-edit-duplicate-name:{name}.{block.Name}"); continue; }
            var fields = Fields(block.Body, ["On", "Range", "Through", "Derivatives", "Join", "StartTangent", "EndTangent"], diagnostics, name + "." + block.Name);
            if (!fields.TryGetValue("On", out var on)) { diagnostics.Add($"profile-edit-target-missing:{block.Name}"); continue; }
            var index = FindMember(segments, on, owner, localFrom);
            if (index < 0) { diagnostics.Add($"profile-edit-target-missing:{block.Name}:{on}"); continue; }
            var carrier = segments[index].Geometry;
            if (carrier is not LineArcLineSegment2D line) { diagnostics.Add($"profile-edit-curve-not-qualified:{block.Name}:straight-carrier-required"); continue; }
            var length = Distance(line.Start, line.End);
            var range = fields.TryGetValue("Range", out var rangeText) ? Vector(rangeText, "mm", diagnostics, block.Name) : (0d, length);
            if (!ValidRange(range, length)) { diagnostics.Add($"profile-edit-range-invalid:{block.Name}"); continue; }
            if (!fields.TryGetValue("Through", out var through)) { diagnostics.Add($"profile-edit-construction-required:{block.Name}:Through"); continue; }
            var points = Vectors(through, "mm", diagnostics, block.Name);
            var derivatives = fields.TryGetValue("Derivatives", out var derivativeText) ? Vectors(derivativeText, "mm", diagnostics, block.Name) : null;
            var join = fields.GetValueOrDefault("Join", "Position");
            if (join is not ("Position" or "Tangent")) diagnostics.Add($"profile-edit-join-invalid:{block.Name}:{join}");
            (double X, double Y)? startTangent = fields.TryGetValue("StartTangent", out var startText) ? Vector(startText, "", diagnostics, block.Name) : null;
            (double X, double Y)? endTangent = fields.TryGetValue("EndTangent", out var endText) ? Vector(endText, "", diagnostics, block.Name) : null;
            if (derivatives is not null && (startTangent is not null || endTangent is not null || join == "Tangent"))
                diagnostics.Add($"profile-edit-construction-conflict:{block.Name}:Derivatives-and-tangents");
            var construction = new ProfileThroughConstruction(points, derivatives, startTangent, endTangent, join == "Tangent");
            var curves = Construct(line, range, construction, diagnostics, block.Name);
            edits.Add(new(block.Name, index, range.Item1 / length, range.Item2 / length,
                curves.Select((c, i) => ($"{block.Name}_Span{i}", (LineArcProfileCurve2D)c)).ToArray(),
                derivatives is null ? "ChordHermite" : "ExplicitHermite", $"profile:{name}/Replace:{block.Name}", construction.MatchTangent));
        }
        foreach (var apply in applies)
        {
            if (!ids.Add(apply)) { diagnostics.Add($"profile-edit-duplicate-name:{name}.{apply}"); continue; }
            var matches = deltas.Deltas.Where(d => d.Delta.Name == apply).ToArray();
            if (matches.Length != 1) { diagnostics.Add($"profile-edit-apply-unresolved:{apply}"); continue; }
            var delta = matches[0];
            var index = FindMember(segments, delta.OwnerPath, owner, localFrom);
            if (index < 0 || segments[index].Geometry is not LineArcLineSegment2D line)
            { diagnostics.Add($"profile-edit-apply-target-invalid:{apply}:{delta.OwnerPath}"); continue; }
            var resolved = SemanticEdgeProfileResolver.Resolve(new(delta.OwnerPath, delta.OwnerPath,
                new(line.Start.X, line.Start.Y), new(line.End.X, line.End.Y), [delta.Delta with { Side = -delta.Delta.Side }], seed.PlaneFrame, "Profile Apply"));
            diagnostics.AddRange(resolved.Diagnostics);
            var member = resolved.Profile?.OrderedMembers.SingleOrDefault(m => !m.IsGeneratedCarrier);
            if (member is null) continue;
            var length = Distance(line.Start, line.End);
            edits.Add(new(apply, index, member.StartU / length, member.EndU / length,
                member.CurveDescendants.Select((c, i) => ($"{apply}.{delta.Delta.Members[i].Name}", c.Geometry)).ToArray(),
                "ProfileDelta", $"profile:{name}/Apply:{apply}"));
        }
        foreach (var group in edits.GroupBy(e => e.Segment))
        {
            var ordered = group.OrderBy(e => e.From).ToArray();
            for (var i = 1; i < ordered.Length; i++)
                if (ordered[i].From < ordered[i - 1].To - Tol) diagnostics.Add($"profile-edit-overlap:{ordered[i - 1].Name}:{ordered[i].Name}");
        }
        if (diagnostics.Count != 0) return true;
        var output = new List<ResolvedProfileSegment2D>();
        for (var i = 0; i < segments.Count; i++)
        {
            var original = segments[i];
            var ordered = edits.Where(e => e.Segment == i).OrderBy(e => e.From).ToArray();
            if (ordered.Length == 0) { output.Add(original); continue; }
            var cursor = 0d; var previous = "Start";
            foreach (var edit in ordered)
            {
                Retain(cursor, edit.From, previous, edit.Name);
                foreach (var curve in edit.Curves)
                    output.Add(new(curve.Name, curve.Curve, new($"profile:{name}:segment:{curve.Name}",
                        $"profile-edit:{name}.{edit.Name}", edit.SourceSpan, edit.Construction, seed.PlaneFrame, original.Provenance.StableId)));
                cursor = edit.To; previous = edit.Name;
            }
            Retain(cursor, 1, previous, "End");
            void Retain(double a, double b, string left, string right)
            {
                if (b - a <= Tol) return;
                var id = $"{original.Name}_Between_{left}_{right}";
                output.Add(new(id, ProfileArrangementBuilder.TrimBounded(original.Geometry, a, b), original.Provenance with {
                    StableId = $"profile:{name}:segment:{id}", TracedFrom = original.Provenance.StableId,
                    Derivation = FormattableString.Invariant($"RetainedBoundary:{a:R}..{b:R}") }));
            }
        }
        result = new(name, seed.PlaneFrame, [new("Outer", true, output)], seed.ConstructionPlane,
            BoundaryEdits: edits.Select(e => new ProfileBoundaryEditEvidence(e.Name, segments[e.Segment].Name, e.From, e.To,
                e.Construction, e.Curves.Select(c => c.Name).ToArray(), e.SourceSpan)).ToArray());
        diagnostics.AddRange(ResolvedProfile2DValidator.Validate(result).Diagnostics);
        if (edits.Count > 0) ValidateSimple(output, diagnostics, name);
        foreach (var edit in edits.Where(e => e.RequireTangentJoin))
        {
            var first = output.FindIndex(s => s.Name == edit.Curves[0].Name);
            var last = output.FindIndex(s => s.Name == edit.Curves[^1].Name);
            if (!SameDirection(Tangent(output[(first + output.Count - 1) % output.Count].Geometry, true), Tangent(output[first].Geometry, false))
                || !SameDirection(Tangent(output[last].Geometry, true), Tangent(output[(last + 1) % output.Count].Geometry, false)))
                diagnostics.Add($"profile-edit-join-mismatch:{name}:{edit.Name}:retained-boundary-tangent");
        }
        if (diagnostics.Count != 0) result = null;
        return true;
    }

    private static int FindMember(IReadOnlyList<ResolvedProfileSegment2D> segments, string reference, string owner, string seed)
    {
        if (seed.StartsWith("__Boundary2Value_", StringComparison.Ordinal)) seed = seed[17..];
        if (owner.Length > 0 && reference.StartsWith(owner + ".", StringComparison.Ordinal)) reference = reference[(owner.Length + 1)..];
        var leaf = reference.StartsWith(seed + ".", StringComparison.Ordinal) ? reference[(seed.Length + 1)..] : reference;
        var candidates = segments.Select((s, i) => (s, i)).Where(x => x.s.Name == reference || x.s.Name == leaf
            || x.s.Name == reference.Replace('.', '_') || x.s.Provenance.TracedFrom == reference
            || x.s.Provenance.ConceptStableId == reference).ToArray();
        return candidates.Length == 1 ? candidates[0].i : -1;
    }

    private static IReadOnlyList<LineArcCubicBezier2D> Construct(LineArcLineSegment2D line, (double, double) range,
        ProfileThroughConstruction construction, List<string> diagnostics, string name)
    {
        var length = Distance(line.Start, line.End);
        var points = new[] { Lerp(line.Start, line.End, range.Item1 / length) }.Concat(construction.Points)
            .Append(Lerp(line.Start, line.End, range.Item2 / length)).ToArray();
        if (points.Length > 1024 || points.Zip(points.Skip(1)).Any(p => Distance(p.First, p.Second) <= Tol))
        { diagnostics.Add($"profile-edit-knots-invalid:{name}"); return []; }
        var derivatives = construction.Derivatives?.ToArray();
        if (derivatives is not null && (derivatives.Length != points.Length || derivatives.Any(d => Distance(d, (0, 0)) <= Tol)))
        { diagnostics.Add($"profile-edit-derivatives-invalid:{name}"); return []; }
        var speeds = points.Zip(points.Skip(1)).Select(p => Distance(p.First, p.Second)).ToArray();
        if (derivatives is null)
        {
            derivatives = new (double X, double Y)[points.Length];
            derivatives[0] = Subtract(points[1], points[0]);
            derivatives[^1] = Subtract(points[^1], points[^2]);
            for (var i = 1; i < points.Length - 1; i++)
            {
                var before = Scale(Subtract(points[i], points[i - 1]), 1 / speeds[i - 1]);
                var after = Scale(Subtract(points[i + 1], points[i]), 1 / speeds[i]);
                derivatives[i] = Scale(Add(Scale(before, speeds[i]), Scale(after, speeds[i - 1])), 1 / (speeds[i - 1] + speeds[i]));
            }
            var direction = Subtract(line.End, line.Start);
            Endpoint(0, construction.StartTangent ?? (construction.MatchTangent ? direction : null));
            Endpoint(points.Length - 1, construction.EndTangent ?? (construction.MatchTangent ? direction : null));
            void Endpoint(int i, (double X, double Y)? tangent)
            {
                if (tangent is null) { derivatives[i] = Scale(derivatives[i], 1 / speeds[i == 0 ? 0 : ^1]); return; }
                var magnitude = Distance(tangent.Value, (0, 0));
                if (magnitude <= Tol) { diagnostics.Add($"profile-edit-tangent-invalid:{name}"); return; }
                derivatives[i] = Scale(tangent.Value, 1 / magnitude);
            }
            if (derivatives.Any(d => Distance(d, (0, 0)) <= Tol)) diagnostics.Add($"profile-edit-cusp:{name}");
        }
        return Enumerable.Range(0, points.Length - 1).Select(i => {
            var factor = construction.Derivatives is null ? speeds[i] / 3 : 1d / 3;
            return new LineArcCubicBezier2D(points[i], Add(points[i], Scale(derivatives[i], factor)),
                Subtract(points[i + 1], Scale(derivatives[i + 1], factor)), points[i + 1]);
        }).ToArray();
    }

    private static void ValidateSimple(IReadOnlyList<ResolvedProfileSegment2D> segments, List<string> diagnostics, string name)
    {
        if (segments.Count > 1024) { diagnostics.Add($"profile-edit-boundary-limit:{name}:maximum=1024"); return; }
        if (segments.Any(s => s.Geometry is not (LineArcLineSegment2D or LineArcCircularArc2D or LineArcCubicBezier2D)))
        { diagnostics.Add($"profile-edit-validation-not-qualified:{name}"); return; }
        foreach (var s in segments)
            if (s.Geometry is LineArcCubicBezier2D c)
            {
                if (CubicCrossesItself(c)) diagnostics.Add($"profile-edit-self-intersection:{name}:{s.Name}");
                if (!CubicIsRegular(c)) diagnostics.Add($"profile-edit-cusp:{name}:{s.Name}");
            }
        for (var i = 0; i < segments.Count; i++) for (var j = i + 1; j < segments.Count; j++)
        {
            var hit = ProfileArrangementBuilder.IntersectBounded(segments[i].Geometry, segments[j].Geometry);
            var adjacent = j == i + 1 || i == 0 && j == segments.Count - 1;
            if (hit.HasBoundedOverlap || hit.Intersections.Any(p => !adjacent ||
                !(j == i + 1 && p.FirstParameter >= 1 - Tol && p.SecondParameter <= Tol ||
                  i == 0 && j == segments.Count - 1 && p.FirstParameter <= Tol && p.SecondParameter >= 1 - Tol)))
                diagnostics.Add($"profile-edit-self-intersection:{name}:{segments[i].Name}:{segments[j].Name}");
        }
    }

    private static bool CubicCrossesItself(LineArcCubicBezier2D c)
    {
        var a = Add(Subtract(c.End, c.Start), Scale(Subtract(c.Control1, c.Control2), 3));
        var b = Scale(Add(Subtract(c.Start, Scale(c.Control1, 2)), c.Control2), 3);
        var d = Scale(Subtract(c.Control1, c.Start), 3);
        var denominator = Cross(a, b);
        if (Math.Abs(denominator) < 1e-12) return false;
        var sum = -Cross(a, d) / denominator;
        var product = sum * sum + (Math.Abs(a.X) >= Math.Abs(a.Y) ? (b.X * sum + d.X) / a.X : (b.Y * sum + d.Y) / a.Y);
        var discriminant = sum * sum - 4 * product;
        if (discriminant <= Tol * Tol) return false;
        var first = (sum - Math.Sqrt(discriminant)) / 2; var second = (sum + Math.Sqrt(discriminant)) / 2;
        return first >= -Tol && second <= 1 + Tol && second - first > Tol;
    }

    private static bool CubicIsRegular(LineArcCubicBezier2D c)
    {
        var a = Scale(Add(Subtract(c.End, c.Start), Scale(Subtract(c.Control1, c.Control2), 3)), 3);
        var b = Scale(Add(Subtract(c.Start, Scale(c.Control1, 2)), c.Control2), 6);
        var d = Scale(Subtract(c.Control1, c.Start), 3);
        var useX = Math.Abs(a.X) + Math.Abs(b.X) + Math.Abs(d.X) >= Math.Abs(a.Y) + Math.Abs(b.Y) + Math.Abs(d.Y);
        var aa = useX ? a.X : a.Y; var bb = useX ? b.X : b.Y; var dd = useX ? d.X : d.Y;
        var roots = new List<double> { 0, 1 };
        if (Math.Abs(aa) < 1e-12) { if (Math.Abs(bb) >= 1e-12) roots.Add(-dd / bb); }
        else
        {
            var discriminant = bb * bb - 4 * aa * dd;
            if (discriminant >= 0) { roots.Add((-bb + Math.Sqrt(discriminant)) / (2 * aa)); roots.Add((-bb - Math.Sqrt(discriminant)) / (2 * aa)); }
        }
        return !roots.Any(t => t >= 0 && t <= 1 && Distance(Add(Add(Scale(a, t * t), Scale(b, t)), d), (0, 0)) <= Tol);
    }

    private static Dictionary<string, string> Fields(string text, string[] allowed, List<string> diagnostics, string name)
    {
        var result = new Dictionary<string, string>(StringComparer.Ordinal); var cursor = 0;
        while (cursor < text.Length)
        {
            if (char.IsWhiteSpace(text[cursor]) || text[cursor] == ';') { cursor++; continue; }
            var match = Regex.Match(text[cursor..], @"^(?<key>\w+)\s*:\s*");
            if (!match.Success) { diagnostics.Add($"profile-edit-fields-invalid:{name}:{text[cursor..].Trim()}"); break; }
            var key = match.Groups["key"].Value; cursor += match.Length; var start = cursor;
            if (cursor < text.Length && text[cursor] == '[')
            {
                var depth = 0;
                do { if (text[cursor] == '[') depth++; else if (text[cursor] == ']') depth--; cursor++; }
                while (cursor < text.Length && depth > 0);
                if (depth != 0) diagnostics.Add($"profile-edit-array-invalid:{name}:{key}");
            }
            else while (cursor < text.Length && text[cursor] != ';' && text[cursor] != '\r' && text[cursor] != '\n'
                && !Regex.IsMatch(text[cursor..], @"^\s+\w+\s*:")) cursor++;
            if (!allowed.Contains(key) || !result.TryAdd(key, text[start..cursor].Trim())) diagnostics.Add($"profile-edit-field-invalid:{name}:{key}");
        }
        return result;
    }

    private static (double X, double Y) Vector(string text, string unit, List<string> diagnostics, string name)
    {
        var pieces = text.Trim().Trim('[', ']').Split(',', StringSplitOptions.TrimEntries);
        if (pieces.Length == 2 && Number(pieces[0], unit, out var x) && Number(pieces[1], unit, out var y)) return (x, y);
        diagnostics.Add($"profile-edit-vector-invalid:{name}:{text}"); return (0, 0);
    }
    private static IReadOnlyList<(double X, double Y)> Vectors(string text, string unit, List<string> diagnostics, string name)
    {
        var matches = Regex.Matches(text, @"\[[^\[\]]*\]").ToArray();
        var residue = text.Trim();
        if (!residue.StartsWith('[') || !residue.EndsWith(']')) { diagnostics.Add($"profile-edit-array-invalid:{name}"); return []; }
        residue = Regex.Replace(residue[1..^1], @"\[[^\[\]]*\]", "");
        if (residue.Any(c => !char.IsWhiteSpace(c) && c != ',')) diagnostics.Add($"profile-edit-array-invalid:{name}");
        return text.Trim() == "[]" ? [] : matches.Select(m => Vector(m.Value, unit, diagnostics, name)).ToArray();
    }
    private static bool Number(string text, string unit, out double value)
    {
        value = 0;
        if (unit.Length > 0 && !text.EndsWith(unit, StringComparison.Ordinal)) return false;
        return double.TryParse(unit.Length > 0 ? text[..^unit.Length] : text, NumberStyles.Float, CultureInfo.InvariantCulture, out value) && double.IsFinite(value);
    }
    private static IEnumerable<Block> Blocks(string source, string kind)
    {
        foreach (Match match in Regex.Matches(source, $@"\b{kind}\s+(?<name>\w+)\s*\{{"))
        {
            var body = ReadBody(source, match.Index + match.Length - 1, out var end);
            if (body is not null) yield return new(match.Groups["name"].Value, body, match.Index, end - match.Index + 1);
        }
    }
    private static string? ReadBody(string source, int open, out int end)
    {
        var depth = 0; end = open;
        for (var i = open; i < source.Length; i++)
        { if (source[i] == '{') depth++; else if (source[i] == '}' && --depth == 0) { end = i; return source[(open + 1)..i]; } }
        return null;
    }
    private static bool ValidRange((double A, double B) range, double length) => range.A >= 0 && range.B <= length && range.B - range.A > Tol;
    private static (double X, double Y) Tangent(LineArcProfileCurve2D curve, bool end) => curve switch {
        LineArcLineSegment2D line => Subtract(line.End, line.Start),
        LineArcCubicBezier2D cubic => end ? Subtract(cubic.End, cubic.Control2) : Subtract(cubic.Control1, cubic.Start),
        LineArcCircularArc2D arc => (Math.Sign(arc.SweepAngleRadians) * -Math.Sin(arc.StartAngleRadians + (end ? arc.SweepAngleRadians : 0)),
            Math.Sign(arc.SweepAngleRadians) * Math.Cos(arc.StartAngleRadians + (end ? arc.SweepAngleRadians : 0))),
        _ => (0, 0)
    };
    private static bool SameDirection((double X, double Y) a, (double X, double Y) b) =>
        Distance(a, (0, 0)) > Tol && Distance(b, (0, 0)) > Tol
        && Distance(Scale(a, 1 / Distance(a, (0, 0))), Scale(b, 1 / Distance(b, (0, 0)))) <= Tol;
    private static double Distance((double X, double Y) a, (double X, double Y) b) => Math.Sqrt(Math.Pow(a.X - b.X, 2) + Math.Pow(a.Y - b.Y, 2));
    private static (double X, double Y) Lerp((double X, double Y) a, (double X, double Y) b, double t) => Add(a, Scale(Subtract(b, a), t));
    private static (double X, double Y) Add((double X, double Y) a, (double X, double Y) b) => (a.X + b.X, a.Y + b.Y);
    private static (double X, double Y) Subtract((double X, double Y) a, (double X, double Y) b) => (a.X - b.X, a.Y - b.Y);
    private static (double X, double Y) Scale((double X, double Y) a, double s) => (a.X * s, a.Y * s);
    private static double Cross((double X, double Y) a, (double X, double Y) b) => a.X * b.Y - a.Y * b.X;
}
