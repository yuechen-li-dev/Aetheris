using System.Globalization;
using Aetheris.Kernel.Core.Brep;
using Aetheris.Kernel.Core.Brep.Tessellation;
using Aetheris.Kernel.Core.Geometry.Surfaces;
using Aetheris.Kernel.Core.Math;
using Aetheris.Kernel.Core.Step242;
using Aetheris.Kernel.Firmament.Materializer;

namespace Aetheris.Kernel.Firmament.Visualization;

/// <summary>Presentation only. Values and topology associations come from AP242 readback,
/// never from rounded display coordinates. Points are in Z-up millimetres.</summary>
public sealed record PmiDisplayAnnotation(string Id, string Kind, string Name, string Text,
    double? Value, double? TolerancePlus, double? ToleranceMinus, IReadOnlyList<int> StepFaceEntities,
    double[] Anchor, double[] LabelPosition, double TextHeight, IReadOnlyList<double[][]> Lines)
{
    public PmiDisplayTarget? Target { get; init; }
}

/// <summary>A presentation witness on the associated support, not new engineering geometry.
/// Plane patches stay inside a trimmed display triangle. Cylinders highlight their associated
/// trimmed wall and physical rim wires; the leader evaluates the analytic support.</summary>
public sealed record PmiDisplayTarget(string Kind, double[] Normal, double StemLength, double[] Positions,
    double[] Normals, int[] Indices, IReadOnlyList<double[][]> Lines)
{
    /// <summary>Small outward presentation offset prevents coincident-surface striping in
    /// ordinary glTF viewers. Anchor remains on the exact support; body buffers never move.</summary>
    public double SurfaceOffset { get; init; }
}

public static class PmiDisplayProjection
{
    public static IReadOnlyList<PmiDisplayAnnotation> FromStep(string step, DisplayTessellationOptions? options = null)
    {
        var inspection = Step242SemanticPmiInspector.Inspect(step);
        if (!inspection.Success) throw new InvalidOperationException("pmi-display-inspection-failed: " + string.Join("; ", inspection.Diagnostics));
        if (inspection.Items.Count == 0) return [];
        var imported = Step242Importer.ImportBody(step);
        if (!imported.IsSuccess) throw new InvalidOperationException("pmi-display-body-unavailable");
        var body = imported.Value;
        var tessellation = BrepDisplayTessellator.Tessellate(body, options ?? DisplayTessellationOptions.Default);
        if (!tessellation.IsSuccess) throw new InvalidOperationException("pmi-display-target-unavailable");
        var patches = tessellation.Value.FacePatches.ToDictionary(p => p.FaceId.Value);
        var sourceFaces = body.Bindings.FaceBindings.Where(b => b.SourceStepEntityId.HasValue)
            .ToDictionary(b => b.SourceStepEntityId!.Value, b => b.FaceId.Value);
        var points = patches.Values.SelectMany(p => p.Positions).ToArray();
        if (points.Length == 0) throw new InvalidOperationException("pmi-display-target-unavailable: No displayable faces are available for callouts.");
        var minX = points.Min(p => p.X); var maxX = points.Max(p => p.X);
        var minY = points.Min(p => p.Y); var maxY = points.Max(p => p.Y);
        var minZ = points.Min(p => p.Z); var maxZ = points.Max(p => p.Z);
        var height = Math.Clamp(Math.Max(Math.Max(maxX - minX, maxY - minY), maxZ - minZ) / 25, .5, 8);
        var result = new List<PmiDisplayAnnotation>();
        foreach (var item in inspection.Items)
        {
            if (!item.PresentationSupported)
                throw new InvalidOperationException($"pmi-display-parameters-unsupported: '{item.Name}' retains advanced semantic parameters that this 3D label path cannot yet display completely.");
            if (item.Kind is not ("Datum" or "Diameter" or "Dimension") && Step242GeometricToleranceKinds.Find(item.Kind) is null)
                throw new InvalidOperationException($"pmi-display-kind-unsupported: {item.Kind} '{item.Name}' has no qualified 3D callout yet.");
            var targets = item.GeometricFaceEntityIds.Where(id => sourceFaces.TryGetValue(id, out var face)
                    && patches.TryGetValue(face, out var patch) && patch.Positions.Count > 0 && patch.TriangleIndices.Count >= 3)
                .Select(id => patches[sourceFaces[id]]).ToArray();
            if (targets.Length == 0 || targets.Length != item.GeometricFaceEntityIds.Count)
                throw new InvalidOperationException($"pmi-display-target-unresolved: '{item.Name}' has no complete geometric face association.");
            double[] label = [maxX + height * 3, maxY - result.Count * height * 2.5, maxZ + height];
            // A section planner can partition one physical cylindrical wall.
            // Admit only a connected set on the same analytic cylinder, and
            // retain every associated patch in the highlight and leaders.
            if (targets.Length != 1 && (item.Kind is "Datum" or "Diameter") && (item.Kind != "Diameter" || !OneCylindricalWall(body, targets)))
                throw new InvalidOperationException($"pmi-display-target-multiple-unsupported: '{item.Name}' requires a multi-face presentation.");
            var internalEdges = targets.SelectMany(p => body.Topology.GetFace(p.FaceId).LoopIds
                    .SelectMany(id => body.Topology.GetLoop(id).CoedgeIds).Select(id => body.Topology.GetCoedge(id).EdgeId).Distinct())
                .GroupBy(id => id).Where(g => g.Count() > 1).Select(g => g.Key).ToHashSet();
            var witnesses = targets.Select(p => Target(body, tessellation.Value, p, item.Kind, height, label, internalEdges)).ToArray();
            var (anchorPoint, normal, target) = witnesses[0];
            if (witnesses.Length > 1)
            {
                var positions = new List<double>(); var normals = new List<double>(); var indices = new List<int>();
                foreach (var witness in witnesses)
                {
                    var offset = positions.Count / 3;
                    positions.AddRange(witness.Marker.Positions); normals.AddRange(witness.Marker.Normals);
                    indices.AddRange(witness.Marker.Indices.Select(i => i + offset));
                }
                target = target with { Positions = positions.ToArray(), Normals = normals.ToArray(), Indices = indices.ToArray(), Lines = witnesses.SelectMany(w => w.Marker.Lines).ToArray() };
            }
            double[] anchor = P(anchorPoint);
            var stem = anchorPoint + normal * target.StemLength;
            string N(double value) => value.ToString("G9", CultureInfo.InvariantCulture);
            string Modifier(string value) => value switch { "MAXIMUM_MATERIAL_REQUIREMENT" => "(M)", "LEAST_MATERIAL_REQUIREMENT" => "(L)", "FREE_STATE" => "(F)", "COMMON_ZONE" => "CZ", "STATISTICAL_TOLERANCE" => "ST", _ => value };
            var datumLabels = item.DatumReferenceFrames.Count > 0 ? item.DatumReferenceFrames.Select(frame => string.Join("-", frame.Labels) + string.Concat(frame.Modifiers.Select(Modifier))) : item.DatumReferences;
            var zoneLabels = string.Join(" ", item.ZoneForms.Select(form => form switch
            {
                "cylindrical or circular" => "Ø", "spherical" => "SØ",
                "between two equidistant surfaces" => "BetweenSurfaces ",
                _ => throw new InvalidOperationException($"pmi-display-zone-unsupported: '{item.Name}' has unsupported zone form '{form}'.")
            }));
            if (item.Kind != "Datum" && item.Value is null) throw new InvalidOperationException($"pmi-display-value-unresolved: '{item.Name}' has no semantic magnitude.");
            var text = item.Kind == "Datum" ? $"[{item.Name}]" : Step242GeometricToleranceKinds.Find(item.Kind) is not null
                ? $"{item.Kind} | " + zoneLabels + $"{N(item.Value!.Value)} mm" + string.Concat(item.Modifiers.Select(Modifier))
                    + (item.DatumReferences.Count > 0 ? " | " + string.Join(" | ", datumLabels) : "")
                : (item.Kind == "Diameter" || item.DimensionKind?.Equals("diameter", StringComparison.OrdinalIgnoreCase) == true ? "Ø" : (item.DimensionKind ?? "Length") + " ") + N(item.Value!.Value)
                    + (item.TolerancePlus.HasValue && item.ToleranceMinus.HasValue ? $" +{N(item.TolerancePlus.Value)}/-{N(item.ToleranceMinus.Value)}" : "") + " " + item.ValueUnit;
            var outlines = PlanarTextProfiles.PresentationContours(text, height);
            var lines = new List<double[][]> { new[] { anchor, P(stem), new[] { label[0] - height, label[1], label[2] }, label } };
            foreach (var witness in witnesses.Skip(1))
                lines.Add([P(witness.Anchor), P(witness.Anchor + witness.Normal * witness.Marker.StemLength), [label[0] - height, label[1], label[2]], label]);
            // A small open arrow terminates at the actual support, along its face normal.
            var side = Direction3D.Create(normal.Cross(Math.Abs(normal.Z) < .9 ? new(0,0,1) : new Vector3D(0,1,0))).ToVector();
            lines.Add([P(anchorPoint + normal * target.StemLength * .35 + side * target.StemLength * .15), anchor,
                P(anchorPoint + normal * target.StemLength * .35 - side * target.StemLength * .15)]);
            foreach (var curve in outlines)
            {
                double[] P((double X, double Y) p) => [label[0] + p.X, label[1] + p.Y, label[2]];
                lines.Add(curve switch
                {
                    LineArcLineSegment2D line => [P(line.Start), P(line.End)],
                    LineArcCubicBezier2D cubic => Enumerable.Range(0, 13).Select(i =>
                    {
                        var t = i / 12d; var s = 1 - t;
                        return P((s*s*s*cubic.Start.X + 3*s*s*t*cubic.Control1.X + 3*s*t*t*cubic.Control2.X + t*t*t*cubic.End.X,
                            s*s*s*cubic.Start.Y + 3*s*s*t*cubic.Control1.Y + 3*s*t*t*cubic.Control2.Y + t*t*t*cubic.End.Y));
                    }).ToArray(),
                    _ => throw new InvalidOperationException("pmi-display-font-curve-unsupported")
                });
            }
            result.Add(new("pmi:" + item.EntityId, item.Kind, item.AuthoredName ?? item.Name, text,
                item.Value, item.TolerancePlus, item.ToleranceMinus, item.GeometricFaceEntityIds, anchor, label, height, lines) { Target = target });
        }
        return result;
    }

    private static double[] P(Point3D p) => [p.X, p.Y, p.Z];
    private static double[] V(Vector3D v) => [v.X, v.Y, v.Z];

    private static (Point3D Anchor, Vector3D Normal, PmiDisplayTarget Marker) Target(
        BrepBody body, DisplayTessellationResult tessellation, DisplayFaceMeshPatch patch, string kind, double height, double[] label, IReadOnlySet<Aetheris.Kernel.Core.Topology.EdgeId> internalEdges)
    {
        body.TryGetFaceSurfaceGeometry(patch.FaceId, out var surface);
        if (surface?.Plane is PlaneSurface plane)
        {
            // The largest triangle incircle is a bounded, deterministic interior witness.
            // A vertex average of the whole face can land in a hole or outside a concavity.
            var triangle = Triangles(patch).Select(t => Incenter(t.A, t.B, t.C)).MaxBy(t => t.Radius);
            if (triangle.Radius <= 1e-9) throw new InvalidOperationException("pmi-display-target-degenerate");
            var center = triangle.Center;
            var normal = plane.Normal.ToVector();
            if (normal.Dot(patch.Normals[0]) < 0) normal = -normal;
            var u = plane.UAxis.ToVector(); var v = plane.VAxis.ToVector();
            var size = Math.Min(height * .8, triangle.Radius * .6); // corners inside incircle
            var offset = Math.Min(height * .002, size * .01);
            var lifted = center + normal * offset;
            Point3D[] corners = [lifted - u * size - v * size, lifted + u * size - v * size,
                lifted + u * size + v * size, lifted - u * size + v * size];
            return (center, normal, new("PlanePatch", V(normal), height, corners.SelectMany(P).ToArray(),
                corners.SelectMany(_ => V(normal)).ToArray(), [0,1,2,0,2,3], [corners.Append(corners[0]).Select(P).ToArray(),
                    [P(lifted - u * size), P(lifted + u * size)], [P(lifted - v * size), P(lifted + v * size)]]) { SurfaceOffset = offset });
        }
        if (surface?.Cylinder is CylinderSurface cylinder)
        {
            // Unwrap each admitted display triangle locally, including the periodic seam.
            // Select a substantial triangle away from the end rims; use the label-facing
            // side only as a deterministic tie breaker, never as topology authority.
            Point3D Uv(Point3D point)
            {
                var d = point - cylinder.Origin;
                return new(Math.Atan2(d.Dot(cylinder.YAxis.ToVector()), d.Dot(cylinder.XAxis.ToVector())) * cylinder.Radius,
                    d.Dot(cylinder.Axis.ToVector()), 0);
            }
            Point3D Unwrap(Point3D p, double reference) => p with { X = reference + Math.IEEERemainder(p.X - reference, 2 * Math.PI * cylinder.Radius) };
            Point3D Evaluate(Point3D uv) => cylinder.Evaluate(uv.X / cylinder.Radius, uv.Y);
            var midV = (patch.Positions.Min(p => Uv(p).Y) + patch.Positions.Max(p => Uv(p).Y)) / 2;
            var candidates = Triangles(patch).Select(t =>
            {
                var a = Uv(t.A); var b = Unwrap(Uv(t.B), a.X); var c = Unwrap(Uv(t.C), a.X);
                var center = new Point3D((a.X+b.X+c.X)/3, (a.Y+b.Y+c.Y)/3, 0);
                var area = (b-a).Cross(c-a).Length;
                var radial = cylinder.Normal(center.X/cylinder.Radius).ToVector();
                var facing = radial.Dot(new Point3D(label[0],label[1],label[2])-Evaluate(center));
                return (center,area,facing);
            }).Where(t => t.area > 1e-12).ToArray();
            if (candidates.Length == 0) throw new InvalidOperationException("pmi-display-target-degenerate");
            var largest = candidates.Max(t => t.area);
            var chosen = candidates.Where(t => t.area >= largest * .5)
                .OrderBy(t => Math.Round(Math.Abs(t.center.Y - midV)/height, 8)).ThenByDescending(t => t.facing).First();
            var anchor = Evaluate(chosen.center);
            var normal = cylinder.Normal(chosen.center.X/cylinder.Radius).ToVector();
            // Compare at the same angular location to retain the imported face orientation.
            var first = patch.Positions[patch.TriangleIndices[0]];
            var sense = cylinder.Normal(Uv(first).X/cylinder.Radius).ToVector().Dot(patch.Normals[patch.TriangleIndices[0]]) < 0 ? -1 : 1;
            normal *= sense;
            // Highlight the associated trimmed wall, not a nearly invisible tessellation strip.
            // Kernel edge wires identify its rims. An edge used twice by this face is a
            // parameter seam, not a manufactured edge, so it receives no rim highlight.
            var rims = body.Topology.GetFace(patch.FaceId).LoopIds.SelectMany(id => body.Topology.GetLoop(id).CoedgeIds)
                .Select(id => body.Topology.GetCoedge(id).EdgeId).GroupBy(id => id).Where(g => g.Count() == 1 && !internalEdges.Contains(g.Key)).Select(g => g.Key).ToHashSet();
            var stemLength = Math.Min(height, cylinder.Radius*.45);
            var offset = Math.Min(height * .002, stemLength * .01);
            double[] Lift(Point3D point) => P(point + cylinder.Normal(Uv(point).X/cylinder.Radius).ToVector() * sense * offset);
            var outlines = tessellation.EdgePolylines.Where(edge => rims.Contains(edge.EdgeId)).Select(edge =>
                (edge.IsClosed ? edge.Points.Append(edge.Points[0]) : edge.Points).Select(Lift).ToArray()).ToArray();
            return (anchor, normal, new("CylinderWall", V(normal), stemLength, patch.Positions.SelectMany(Lift).ToArray(),
                patch.Normals.SelectMany(V).ToArray(), patch.TriangleIndices.ToArray(), outlines) { SurfaceOffset = offset });
        }
        throw new InvalidOperationException($"pmi-display-target-kind-unsupported: {kind} requires its qualified plane or cylinder support.");
    }

    private static bool OneCylindricalWall(BrepBody body, IReadOnlyList<DisplayFaceMeshPatch> patches)
    {
        var surfaces = patches.Select(p => body.GetFaceSurface(p.FaceId)).ToArray();
        if (surfaces.Any(s => s.Cylinder is null)) return false;
        var first = surfaces[0].Cylinder!.Value; var axis = first.Axis.ToVector();
        if (surfaces.Skip(1).Any(s =>
        {
            var c = s.Cylinder!.Value; var delta = c.Origin - first.Origin;
            return Math.Abs(c.Radius - first.Radius) > 1e-7 || axis.Cross(c.Axis.ToVector()).Length > 1e-7
                || (delta - axis * delta.Dot(axis)).Length > 1e-7;
        })) return false;
        var edges = patches.Select(p => body.Topology.GetFace(p.FaceId).LoopIds.SelectMany(id => body.Topology.GetLoop(id).CoedgeIds)
            .Select(id => body.Topology.GetCoedge(id).EdgeId).ToHashSet()).ToArray();
        var seen = new HashSet<int> { 0 }; var pending = new Queue<int>(); pending.Enqueue(0);
        while (pending.TryDequeue(out var current))
            for (var i = 0; i < edges.Length; i++)
                if (!seen.Contains(i) && edges[current].Overlaps(edges[i])) { seen.Add(i); pending.Enqueue(i); }
        return seen.Count == patches.Count;
    }

    private static IEnumerable<(Point3D A, Point3D B, Point3D C)> Triangles(DisplayFaceMeshPatch patch)
    {
        for (var i = 0; i + 2 < patch.TriangleIndices.Count; i += 3)
            yield return (patch.Positions[patch.TriangleIndices[i]], patch.Positions[patch.TriangleIndices[i+1]], patch.Positions[patch.TriangleIndices[i+2]]);
    }

    private static (Point3D Center, double Radius) Incenter(Point3D a, Point3D b, Point3D c)
    {
        var wa = (b-c).Length; var wb = (a-c).Length; var wc = (a-b).Length; var sum = wa+wb+wc;
        if (sum <= 1e-12) return (a, 0);
        return (new((a.X*wa+b.X*wb+c.X*wc)/sum, (a.Y*wa+b.Y*wb+c.Y*wc)/sum,
            (a.Z*wa+b.Z*wb+c.Z*wc)/sum), (b-a).Cross(c-a).Length/sum);
    }
}
