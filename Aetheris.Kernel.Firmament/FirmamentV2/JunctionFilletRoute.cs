using Aetheris.Kernel.Core.Brep;
using Aetheris.Kernel.Core.Brep.Blends;
using Aetheris.Kernel.Core.Diagnostics;
using Aetheris.Kernel.Core.Geometry;
using Aetheris.Kernel.Core.Math;
using Aetheris.Kernel.Core.Results;
using Aetheris.Kernel.Core.Step242;
using Aetheris.Kernel.Firmament.FirmamentV2.Syntax;
using Aetheris.Kernel.Firmament.Materializer;

namespace Aetheris.Kernel.Firmament.FirmamentV2;

/// <summary>
/// <c>EdgeFinish&lt;Fillet&gt; { where: A meets B; radius: r }</c> on a Compose body: the edges where the faces of
/// operation A meet the faces of operation B, rounded by a ball of radius r. Operands are Compose operations by name
/// (Base, Add, Boss, Pocket) or every operation with a role (<c>role Pad</c>). The body is built without these
/// finishes through the ordinary pipeline; each face of the result is then attributed to the operations whose prism
/// it lies on (a cap inside the profile, or a side on its outline within its heights), and an edge is selected when
/// one face belongs to A and not B and the other to B and not A. Faces both operations share, such as a pad's wall
/// flush with the plate's, select nothing. The selected edges are rounded with <see cref="UniformRounding.RoundSelectedEdges"/>.
/// </summary>
internal static class JunctionFilletRoute
{
    internal const string Code = "firmament-junction-fillet";

    internal sealed record Operand(string Name, bool Role);

    internal sealed record Finish(string Name, Operand Left, Operand Right, double Radius, FirmamentV2SourceSpan Span);

    internal sealed record Request(string Stripped, IReadOnlyList<Finish> Finishes, IReadOnlyList<(string Message, FirmamentV2SourceSpan Span)> Errors, string? Host);

    /// <summary>Every edge finish with a <c>where</c> field, and the source without them; null when there is none.</summary>
    internal static Request? Find(string source)
    {
        var document = FirmamentSyntaxParser.Parse(source).Document;
        var declarations = FirmamentSyntaxTraversal.Declarations(document).ToArray();
        var finishes = declarations.Where(d => d.Type.Name.Text == "EdgeFinish" && Field(d, "Where") is not null).ToArray();
        if (finishes.Length == 0) return null;
        var found = new List<Finish>();
        var errors = new List<(string, FirmamentV2SourceSpan)>();
        foreach (var finish in finishes)
        {
            var name = finish.Name?.Text ?? "EdgeFinish";
            var generic = finish.Type.Arguments.Count == 1 ? FirmamentSyntaxValues.Reference(finish.Type.Arguments[0].Value) : null;
            var kind = generic ?? FirmamentSyntaxValues.Reference(Field(finish, "Kind"));
            var where = Field(finish, "Where") as JunctionSyntax;
            var fields = finish.Body?.Statements.OfType<FieldSyntax>().ToArray() ?? [];
            var extra = fields.Select(f => f.Name.Text).FirstOrDefault(f => !new[] { "Where", "Radius", "Kind" }.Any(n => FirmamentSyntaxFields.SameField(f, n)));
            if (kind != "Fillet")
                errors.Add(($"{Code}-kind:{name}: 'where' selects edges for EdgeFinish<Fillet> only; '{name}' is {(kind is null ? "missing its kind" : $"a {kind}")}.", finish.Span));
            else if (where is null)
                errors.Add(($"{Code}-where-invalid:{name}: 'where' takes a junction: write where: A meets B, where A and B are Compose operations by name or 'role Name' for every operation with that role.", finish.Span));
            else if (extra is not null)
                errors.Add(($"{Code}-field-unknown:{name}: '{extra}' does not combine with where; a junction fillet takes where and radius.", finish.Span));
            else if (!FirmamentSyntaxValues.TryMeasure(Field(finish, "Radius"), "mm", out var radius) || !(radius > 0))
                errors.Add(($"{Code}-radius-invalid:{name}: a junction fillet needs a positive radius.", finish.Span));
            else
                found.Add(new(name, new(where!.Left.Name.Text, where.Left.Role), new(where.Right.Name.Text, where.Right.Role), radius, finish.Span));
        }
        // Remove the finishes, last first; a Modify left empty goes with them.
        var removals = new List<FirmamentV2SourceSpan>();
        foreach (var owner in declarations.Where(d => d.Type.Name.Text == "Modify" && d.Body is not null))
        {
            var mine = owner.Body!.Statements.Where(s => finishes.Contains(s)).ToArray();
            if (mine.Length == 0) continue;
            if (mine.Length == owner.Body.Statements.Count) removals.Add(owner.Span); else removals.AddRange(mine.Select(m => m.Span));
        }
        var stripped = removals.OrderByDescending(r => r.Start).Aggregate(source, (text, span) => text.Remove(span.Start, span.Length));
        var host = declarations.FirstOrDefault(d => d.Type.Name.Text == "Modify" && d.Body?.Statements.Any(s => finishes.Contains(s)) == true)?.Name?.Text;
        return new(stripped, found, errors, host);
    }

    internal static KernelResult<FirmamentStepExportResult> Export(Request request, Func<string, KernelResult<FirmamentStepExportResult>> build, string? sourceDirectory)
    {
        if (request.Errors.Count > 0)
            return KernelResult<FirmamentStepExportResult>.Failure(request.Errors.Select(e => Error(e.Message, e.Span)).ToArray());
        var unfinished = build(request.Stripped);
        if (!unfinished.IsSuccess) return unfinished;
        // The Compose operations as the build bound them (the regex reader is the fallback for sources the V2 parser
        // does not bind).
        var parsed = FirmamentV2Parser.Parse(request.Stripped, sourceDirectory);
        var composition = parsed.BoundComposition ?? PrismaticProfileCompositionParser.Parse(request.Stripped);
        if (composition.Feature is not { } feature)
            return Refuse(request.Finishes[0], "a junction fillet selects edges between the operations of a Compose body, and this source has none"
                + (composition.Diagnostics.Count > 0 ? $" ({string.Join("; ", composition.Diagnostics.Take(3))})" : ""));
        // Fillet the body exactly as it was exported: some routes' in-memory body is an intermediate (a drilled
        // body's runtime handle can predate the drills' final topology).
        var imported = Step242Importer.ImportBody(unfinished.Value.StepText);
        var body = imported.IsSuccess ? imported.Value : unfinished.Value.RuntimeBody;
        if (body is null) return Refuse(request.Finishes[0], "the body before filleting could not be read back: " + string.Join("; ", imported.Diagnostics.Select(d => d.Message)));
        // Every finish's edges are rounded in one construction, each by its own radius: a later finish never has
        // to round a body an earlier one already rounded.
        var sides = new List<(Finish Finish, IReadOnlyList<Prism> Left, IReadOnlyList<Prism> Right)>();
        foreach (var finish in request.Finishes)
        {
            var left = Prisms(feature, composition.Profiles, finish.Left, out var leftError);
            var right = Prisms(feature, composition.Profiles, finish.Right, out var rightError);
            if ((leftError ?? rightError) is { } error) return Refuse(finish, error);
            sides.Add((finish, left!, right!));
        }
        var claimed = sides.ToDictionary(s => s.Finish, _ => 0);
        var touching = sides.ToDictionary(s => s.Finish, _ => (Left: 0, Right: 0));
        var offered = 0;
        Finish? conflict = null;
        var rounded = UniformRounding.RoundSelectedEdges(body, edge =>
        {
            static bool In(IReadOnlyList<Prism> side, Point3D p, SurfaceGeometry s) => side.Any(prism => prism.Holds(p, s));
            double? radius = null;
            offered++;
            foreach (var (finish, left, right) in sides)
            {
                var (aL, aR, bL, bR) = (In(left, edge.InsideA, edge.SurfaceA), In(right, edge.InsideA, edge.SurfaceA), In(left, edge.InsideB, edge.SurfaceB), In(right, edge.InsideB, edge.SurfaceB));
                var t = touching[finish]; touching[finish] = (t.Left + (aL || bL ? 1 : 0), t.Right + (aR || bR ? 1 : 0));
                if (!(aL && !aR && bR && !bL || aR && !aL && bL && !bR)) continue;
                if (radius is not null && radius != finish.Radius) conflict = finish;
                radius ??= finish.Radius; claimed[finish]++;
            }
            return radius;
        });
        if (conflict is not null) return Refuse(conflict, "an edge it selects is also selected by another finish with a different radius");
        // Edges are offered before anything is built, so a finish that claimed none is reported as such even when
        // the construction refuses later; a refusal before any edge was offered is reported as itself.
        if (offered == 0 && !rounded.IsSuccess) return Refuse(request.Finishes[0], string.Join("; ", rounded.Diagnostics.Select(d => d.Message)), all: request.Finishes);
        if (sides.FirstOrDefault(s => claimed[s.Finish] == 0).Finish is { } empty)
            return Refuse(empty, $"no edge of the body lies where these operations meet ({touching[empty].Left} edges touch {Describe(empty.Left)}'s faces, {touching[empty].Right} touch {Describe(empty.Right)}'s)");
        if (!rounded.IsSuccess) return Refuse(request.Finishes[0], string.Join("; ", rounded.Diagnostics.Select(d => d.Message)), all: request.Finishes);
        // Faces the construction left split on one surface go back together before export.
        body = BrepSplitFaceMerge.Merge(rounded.Value);
        var receipts = request.Finishes.Select(f => new FirmamentFeatureReceipt(request.Host ?? unfinished.Value.ExportedFeatureId, "EdgeFinish", f.Name)).ToList();
        // Faces the fillet kept are the faces they were; a rounding face belongs to the finish whose radius it has.
        var correspondence = unfinished.Value.RuntimeCorrespondence is { } before && unfinished.Value.RuntimeBody is { } built
            ? FaceCorrespondenceTransfer.Transfer(built, before, body, request.Finishes.Max(f => f.Radius), surface =>
                FaceCorrespondenceTransfer.RoundingRadius(surface) is { } r && request.Finishes.Where(f => Math.Abs(f.Radius - r) < 1e-7).ToArray() is [var only]
                    ? ($"face({only.Name}.Fillet)", $"edgefinish:{only.Name}") : null)
            : null;
        // PMI is written against the filleted body, through the names its faces kept.
        var pmi = Array.Empty<Step242SemanticPmi>();
        if (parsed.Document is { Pmi.Count: > 0 } document && correspondence is not null)
        {
            var holes = (feature.ShaftHoles ?? []).Select(h => (h.Name, h.StableId)).Concat((feature.CounterboreHoles ?? []).Select(h => (h.Name, h.StableId)))
                .Concat((feature.CountersinkHoles ?? []).Select(h => (h.Name, h.StableId))).ToDictionary(h => h.Name, h => h.StableId, StringComparer.Ordinal);
            pmi = FirmamentBuildAndExport.BuildV2SemanticPmi(document, [], feature.Name, holes, correspondence, body,
                FirmamentBuildAndExport.ComposeOperationCapFaces(feature, body, correspondence), feature).ToArray();
        }
        var step = Step242Exporter.ExportBody(body, pmi, new Step242ExportOptions { ProductName = unfinished.Value.ExportedFeatureId });
        if (!step.IsSuccess) return KernelResult<FirmamentStepExportResult>.Failure(step.Diagnostics);
        return KernelResult<FirmamentStepExportResult>.Success(unfinished.Value with
        {
            StepText = step.Value,
            RuntimeBody = body,
            RuntimeCorrespondence = correspondence,
            // Retained CIR described the body before filleting; it no longer holds.
            Cir = null,
            MaterializedFeatures = [.. unfinished.Value.MaterializedFeatures ?? [], .. receipts],
        }, unfinished.Diagnostics);
    }

    /// <summary>One Compose operation's prism: its profile's region swept between two heights along +Z.</summary>
    private sealed class Prism(ResolvedProfile2D profile, double from, double to)
    {
        private const double Tol = 1e-6;
        private readonly List<List<(double X, double Y)>> loops = profile.Loops.Select(l => l.Segments.SelectMany(s => Sample(s.Geometry)).ToList()).ToList();

        public bool Holds(Point3D p, SurfaceGeometry surface)
        {
            var scale = double.Max(1d, double.Max(double.Abs(from), double.Abs(to)));
            if (surface.Plane is { } plane && double.Abs(double.Abs(plane.Normal.ToVector().Z) - 1d) < 1e-9)
                return (double.Abs(p.Z - from) < Tol * scale || double.Abs(p.Z - to) < Tol * scale) && Inside(p.X, p.Y) && Boundary(p.X, p.Y) > Tol * scale;
            var vertical = surface.Plane is { } wall ? double.Abs(wall.Normal.ToVector().Z) < 1e-9
                : surface.Cylinder is { } cylinder && double.Abs(double.Abs(cylinder.Axis.ToVector().Z) - 1d) < 1e-9;
            return vertical && p.Z > from - Tol * scale && p.Z < to + Tol * scale && Boundary(p.X, p.Y) < 1e-5 * scale;
        }

        private bool Inside(double x, double y)
        {
            // Even-odd over every loop: inner loops are holes.
            var inside = false;
            foreach (var loop in loops)
                for (int i = 0, j = loop.Count - 1; i < loop.Count; j = i++)
                    if (loop[i].Y > y != loop[j].Y > y && x < (loop[j].X - loop[i].X) * (y - loop[i].Y) / (loop[j].Y - loop[i].Y) + loop[i].X) inside = !inside;
            return inside;
        }

        private double Boundary(double x, double y) => profile.Loops.SelectMany(l => l.Segments).Min(s => Distance(s.Geometry, x, y));

        private static double Distance(LineArcProfileCurve2D curve, double x, double y)
        {
            switch (curve)
            {
                case LineArcLineSegment2D line:
                {
                    var (dx, dy) = (line.End.X - line.Start.X, line.End.Y - line.Start.Y);
                    var t = double.Clamp(((x - line.Start.X) * dx + (y - line.Start.Y) * dy) / (dx * dx + dy * dy), 0, 1);
                    return double.Hypot(x - line.Start.X - t * dx, y - line.Start.Y - t * dy);
                }
                case LineArcCircularArc2D arc:
                {
                    var angle = double.Atan2(y - arc.Center.Y, x - arc.Center.X);
                    var offset = Wrap((angle - arc.StartAngleRadians) * double.Sign(arc.SweepAngleRadians));
                    if (offset <= double.Abs(arc.SweepAngleRadians)) return double.Abs(double.Hypot(x - arc.Center.X, y - arc.Center.Y) - arc.Radius);
                    var ends = new[] { arc.StartAngleRadians, arc.StartAngleRadians + arc.SweepAngleRadians };
                    return ends.Min(t => double.Hypot(x - arc.Center.X - arc.Radius * double.Cos(t), y - arc.Center.Y - arc.Radius * double.Sin(t)));
                }
                case LineArcFullCircle2D circle:
                    return double.Abs(double.Hypot(x - circle.Center.X, y - circle.Center.Y) - circle.Radius);
                default:
                    return Sample(curve).Zip(Sample(curve).Skip(1)).Min(s => Distance(new LineArcLineSegment2D(s.First, s.Second), x, y));
            }
        }

        private static double Wrap(double a) { var t = a % (2 * double.Pi); return t < 0 ? t + 2 * double.Pi : t; }

        private static IEnumerable<(double X, double Y)> Sample(LineArcProfileCurve2D curve) => curve switch
        {
            LineArcLineSegment2D line => [line.Start],
            LineArcCircularArc2D arc => Enumerable.Range(0, 64).Select(i => arc.StartAngleRadians + arc.SweepAngleRadians * i / 64)
                .Select(t => (arc.Center.X + arc.Radius * double.Cos(t), arc.Center.Y + arc.Radius * double.Sin(t))),
            LineArcFullCircle2D circle => Enumerable.Range(0, 128).Select(i => 2 * double.Pi * i / 128)
                .Select(t => (circle.Center.X + circle.Radius * double.Cos(t), circle.Center.Y + circle.Radius * double.Sin(t))),
            _ => throw new NotSupportedException($"profile curve {curve.GetType().Name} is not supported by a junction fillet"),
        };
    }

    private static IReadOnlyList<Prism>? Prisms(PrismaticProfileCompositionFeature feature, IReadOnlyDictionary<string, ResolvedProfile2D> profiles, Operand operand, out string? error)
    {
        error = null;
        var operations = feature.Operations.Where(o => operand.Role
            ? string.Equals(o.SemanticRole, operand.Name, StringComparison.OrdinalIgnoreCase)
            : string.Equals(o.Name, operand.Name, StringComparison.Ordinal)).ToArray();
        if (operations.Length == 0)
        {
            var known = operand.Role ? feature.Operations.Select(o => o.SemanticRole).Distinct() : feature.Operations.Select(o => o.Name);
            error = $"'{(operand.Role ? "role " : "")}{operand.Name}' is not {(operand.Role ? "a role" : "an operation")} of Compose {feature.Name}; it has {string.Join(", ", known)}";
            return null;
        }
        if (feature.Axis != "+Z" || operations.Any(o => !profiles.TryGetValue(o.ProfileReference, out var p) || p.ConstructionPlane is { } plane && !WorldXy(plane)))
        {
            var o = operations.First(); profiles.TryGetValue(o.ProfileReference, out var p0);
            error = $"a junction fillet needs operations whose profiles lie on XY and extrude along +Z; '{operand.Name}' does not (axis {feature.Axis}, profile '{o.ProfileReference}' on {p0?.PlaneFrame ?? "unknown"})";
            return null;
        }
        return operations.Select(o => new Prism(profiles[o.ProfileReference], double.Min(o.From, o.To), double.Max(o.From, o.To))).ToArray();
    }

    private static bool WorldXy(ConstructionPlane plane) =>
        (plane.Origin - Point3D.Origin).Length < 1e-12 && plane.AxisX.ToVector().X > 1 - 1e-12 && plane.AxisZ.ToVector().Z > 1 - 1e-12;

    private static KernelResult<FirmamentStepExportResult> Refuse(Finish finish, string reason, IReadOnlyList<Finish>? all = null) =>
        KernelResult<FirmamentStepExportResult>.Failure([Error(all is { Count: > 1 }
            ? $"{Code}:{string.Join(",", all.Select(f => f.Name))}: {reason}"
            : $"{Code}:{finish.Name}: where {Describe(finish.Left)} meets {Describe(finish.Right)}: {reason}", finish.Span)]);

    private static string Describe(Operand operand) => (operand.Role ? "role " : "") + operand.Name;

    private static KernelDiagnostic Error(string message, FirmamentV2SourceSpan span) =>
        new(KernelDiagnosticCode.ValidationFailed, KernelDiagnosticSeverity.Error, message, "Firmament.JunctionFillet") { SourceSpan = new(span.Start, span.Length) };

    private static ExpressionSyntax? Field(DeclarationSyntax d, string name) =>
        d.Body?.Statements.OfType<FieldSyntax>().FirstOrDefault(f => FirmamentSyntaxFields.SameField(f.Name.Text, name))?.Value;
}
