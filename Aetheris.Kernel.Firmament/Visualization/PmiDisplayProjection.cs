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
    /// <summary>Filled glyphs, arrowheads and datum triangles (dark), in model millimetres.
    /// Geometry only: GLB metadata keeps the semantic fields, not these triangles.</summary>
    [System.Text.Json.Serialization.JsonIgnore] public PmiDisplayMesh? TextFill { get; init; }
    /// <summary>Opaque backing panels behind text so callouts stay legible over a translucent part.</summary>
    [System.Text.Json.Serialization.JsonIgnore] public PmiDisplayMesh? Panel { get; init; }
}

/// <summary>Triangles in Z-up millimetres; three coordinates per position.</summary>
public sealed record PmiDisplayMesh(double[] Positions, int[] Indices);

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
        var height = Math.Clamp(Math.Max(Math.Max(maxX - minX, maxY - minY), maxZ - minZ) / 34, .5, 8);
        var center = new Point3D((minX + maxX) / 2, (minY + maxY) / 2, (minZ + maxZ) / 2);

        // 1. Resolve every semantic record to its exact supports.
        var specs = new List<Spec>();
        var notes = new List<Step242SemanticPmiInspectionItem>();
        foreach (var item in inspection.Items)
        {
            // Part notes read back as AP242 annotations (material, general tolerances, finish).
            if (item.Kind == "Annotation") { notes.Add(item); continue; }
            if (!item.PresentationSupported)
                throw new InvalidOperationException($"pmi-display-parameters-unsupported: '{item.Name}' retains advanced semantic parameters that this 3D label path cannot yet display completely.");
            if (item.Kind is not ("Datum" or "Diameter" or "Dimension") && Step242GeometricToleranceKinds.Find(item.Kind) is null)
                throw new InvalidOperationException($"pmi-display-kind-unsupported: {item.Kind} '{item.Name}' has no qualified 3D callout yet.");
            if (item.Kind != "Datum" && item.Value is null) throw new InvalidOperationException($"pmi-display-value-unresolved: '{item.Name}' has no semantic magnitude.");
            var targets = item.GeometricFaceEntityIds.Where(id => sourceFaces.TryGetValue(id, out var face)
                    && patches.TryGetValue(face, out var patch) && patch.Positions.Count > 0 && patch.TriangleIndices.Count >= 3)
                .Select(id => patches[sourceFaces[id]]).ToArray();
            if (targets.Length == 0 || targets.Length != item.GeometricFaceEntityIds.Count)
                throw new InvalidOperationException($"pmi-display-target-unresolved: '{item.Name}' has no complete geometric face association.");
            var surfaces = targets.Select(t => body.GetFaceSurface(t.FaceId)).ToArray();
            Feature feature;
            if (surfaces.All(s => s.Plane is not null))
            {
                // Construction may split one physical face; it must remain one plane.
                var plane = surfaces[0].Plane!.Value; var n = plane.Normal.ToVector();
                if (surfaces.Any(s => s.Plane!.Value.Normal.ToVector().Cross(n).Length > 1e-7 || Math.Abs((s.Plane!.Value.Origin - plane.Origin).Dot(n)) > 1e-7))
                    throw new InvalidOperationException($"pmi-display-target-multiple-unsupported: '{item.Name}' spans more than one plane.");
                feature = new PlanarFeature(n, targets);
            }
            else if (surfaces.All(s => s.Cylinder is not null))
            {
                // One or more holes (a quantity callout); each hole is one connected wall on one cylinder.
                var walls = targets.GroupBy(t => CylinderKey(body.GetFaceSurface(t.FaceId).Cylinder!.Value)).Select(g => g.ToArray()).ToArray();
                if (walls.Any(w => !OneCylindricalWall(body, w)))
                    throw new InvalidOperationException($"pmi-display-target-multiple-unsupported: '{item.Name}' requires connected cylindrical walls.");
                // Several holes are one callout only when the record states that quantity and the holes are alike.
                if (walls.Length > 1 && (item.Quantity != walls.Length
                    || walls.Select(w => Math.Round(body.GetFaceSurface(w[0].FaceId).Cylinder!.Value.Radius, 9)).Distinct().Count() != 1))
                    throw new InvalidOperationException($"pmi-display-target-multiple-unsupported: '{item.Name}' spans {walls.Length} cylinders without a matching quantity of identical holes.");
                feature = new HoleFeature(walls.Select(w => body.GetFaceSurface(w[0].FaceId).Cylinder!.Value).ToArray(), walls);
            }
            else throw new InvalidOperationException($"pmi-display-target-kind-unsupported: '{item.Name}' requires plane or cylinder supports.");
            specs.Add(new(item, targets, feature));
        }

        // 2. Group records that describe the same feature into one callout (size above its frames).
        var layout = new Layout(height, minX, maxX, minY, maxY, minZ, maxZ);
        var output = new Dictionary<int, PmiDisplayAnnotation>();
        var groups = specs.GroupBy(s => string.Join(",", s.Item.GeometricFaceEntityIds.Order())).Select(g => g.ToArray()).ToArray();
        var datumFaces = specs.Where(s => s.Item.Kind == "Datum" && s.Feature is PlanarFeature)
            .ToDictionary(s => s.Item.Name, s => s, StringComparer.Ordinal);

        // Basic dimensions locate position-toleranced holes from their planar datums (theoretically exact, from the
        // model). They share one horizontal plane at the top of the vertical datum faces.
        layout.DimensionLevel = datumFaces.Values.Where(d => d.Feature is PlanarFeature { } f && Math.Abs(f.Normal.Z) < 1e-7)
            .SelectMany(d => d.Targets.SelectMany(t => t.Positions)).Select(p => p.Z).DefaultIfEmpty(maxZ).Max();
        var basics = BasicDimensions(specs, datumFaces, layout);
        var dimensionPlane = new PmiPlane(new Point3D(0, 0, layout.DimensionLevel), new Vector3D(1, 0, 0), new Vector3D(0, 1, 0));

        // Notes occupy the lower right of the front plane; reserve it before placing datum callouts there.
        var notesBlock = notes.Count > 0 ? PmiDrafting.Notes(notes.Select(n => n.Text ?? "").ToArray(), .8 * height) : null;
        if (notesBlock is not null)
            layout.Reserve(new PmiPlane(new Point3D(0, minY, 0), new Vector3D(1, 0, 0), new Vector3D(0, 0, 1)),
                (maxX - notesBlock.Width, minZ - 3 * height - layout.DimensionDepth - notesBlock.Height, maxX, minZ - 3 * height - layout.DimensionDepth));

        // Holes first, so planar datum symbols find free space outside dimensions and hole callouts.
        foreach (var group in groups.OrderBy(g => g[0].Feature is HoleFeature ? 0 : 1).ThenByDescending(g => g[0].Feature is HoleFeature hole ? hole.Cylinders.Length : 0))
        {
            var ordered = group.OrderBy(s => s.Item.Kind switch { "Diameter" or "Dimension" => 0, "Datum" => 3, "Position" => 2, _ => 1 }).ToArray();
            var blocks = ordered.Select(s => (Spec: s, Block: s.Item.Kind switch
            {
                "Diameter" or "Dimension" => PmiDrafting.TextBlock(PmiDrafting.Size(s.Item), height),
                "Datum" => PmiDrafting.DatumBox(s.Item.Name, height),
                _ => PmiDrafting.ControlFrame(s.Item.Kind, PmiDrafting.ZonePrefix(s.Item.ZoneForms), s.Item.Value!.Value,
                    s.Item.Modifiers, s.Item.DatumReferenceFrames.Count > 0
                        ? s.Item.DatumReferenceFrames.Select(f => string.Join("-", f.Labels)).ToArray() : s.Item.DatumReferences, height)
            })).ToArray();
            var gap = .35 * height;
            var stackHeight = blocks.Sum(b => b.Block.Height) + gap * (blocks.Length - 1);
            var stackWidth = blocks.Max(b => b.Block.Width);
            var placed = group[0].Feature switch
            {
                HoleFeature hole => layout.PlaceHoleCallout(hole, center, stackWidth, stackHeight, points),
                PlanarFeature planar => layout.PlacePlanarCallout(planar, ordered[0], stackWidth, stackHeight, ordered.Any(s => s.Item.Kind == "Datum")),
                _ => throw new InvalidOperationException()
            };
            var y = placed.Y + stackHeight;
            for (var i = 0; i < blocks.Length; i++)
            {
                var (spec, block) = blocks[i];
                y -= block.Height;
                var canvas = new PmiCanvas();
                canvas.Append(block.Canvas, placed.X, y);
                if (i == 0) canvas.Append(placed.Leader, 0, 0);
                y -= gap;
                var emitted = Emit(spec, canvas, placed.Plane, placed.Anchor, height, layout.Lift,
                    basics.GetValueOrDefault(spec.Item.EntityId), dimensionPlane);
                if (i == 0 && placed.Drop > 1e-6)
                    emitted = emitted with { Lines = [[P(placed.Anchor), P(placed.Anchor + new Vector3D(0, 0, placed.Drop))], .. emitted.Lines] };
                output[spec.Item.EntityId] = i > 0 ? emitted with { Target = null } : emitted;
            }
        }

        // 3. General notes: one block in the front plane below the part, clear of the datum callouts.
        if (notes.Count > 0)
        {
            var plane = new PmiPlane(new Point3D(0, minY, 0), new Vector3D(1, 0, 0), new Vector3D(0, 0, 1));
            var block = notesBlock!;
            var x = maxX - block.Width; var top = minZ - 3 * height - layout.DimensionDepth;
            var canvas = new PmiCanvas();
            canvas.Append(block.Canvas, x, top - block.Height);
            var at = P(plane.At(x, top));
            foreach (var (note, index) in notes.Select((n, k) => (n, k)))
                output[note.EntityId] = new PmiDisplayAnnotation("pmi:" + note.EntityId, "Note", note.Name, note.Text ?? "",
                    null, null, null, [], at, at, height, index == 0 ? Lines3D(canvas, plane, 0) : [[at, at]])
                {
                    TextFill = index == 0 ? Fill3D(canvas.Fill, canvas.FillIndices, plane, layout.Lift * 2) : null,
                    Panel = index == 0 ? Fill3D(canvas.Panel, canvas.PanelIndices, plane, -layout.Lift) : null
                };
        }
        return inspection.Items.Where(i => output.ContainsKey(i.EntityId)).Select(i => output[i.EntityId]).ToArray();
    }

    /// <summary>Model edges for presentation, excluding parameter seams and section splits inside one smooth face.</summary>
    public static IReadOnlyList<double[][]> EdgeLines(string step, DisplayTessellationOptions? options = null)
    {
        var imported = Step242Importer.ImportBody(step);
        if (!imported.IsSuccess) throw new InvalidOperationException("pmi-display-body-unavailable");
        var body = imported.Value;
        var tessellation = BrepDisplayTessellator.Tessellate(body, options ?? DisplayTessellationOptions.Default);
        if (!tessellation.IsSuccess) throw new InvalidOperationException("pmi-display-target-unavailable");
        var faces = body.Topology.Faces.ToDictionary(f => f.Id, f => f.LoopIds.SelectMany(id => body.Topology.GetLoop(id).CoedgeIds)
            .Select(id => body.Topology.GetCoedge(id).EdgeId).ToArray());
        var owners = faces.SelectMany(f => f.Value.Select(e => (Edge: e, Face: f.Key))).GroupBy(x => x.Edge).ToDictionary(g => g.Key, g => g.Select(x => x.Face).ToArray());
        bool Smooth(Aetheris.Kernel.Core.Topology.EdgeId edge)
        {
            if (!owners.TryGetValue(edge, out var adjacent)) return false;
            if (adjacent.Length == 2 && adjacent[0] == adjacent[1]) return true; // periodic seam
            if (adjacent.Length != 2) return false;
            var a = body.GetFaceSurface(adjacent[0]); var b = body.GetFaceSurface(adjacent[1]);
            if (a.Plane is { } pa && b.Plane is { } pb)
                return pa.Normal.ToVector().Cross(pb.Normal.ToVector()).Length < 1e-7 && Math.Abs((pb.Origin - pa.Origin).Dot(pa.Normal.ToVector())) < 1e-7;
            if (a.Cylinder is { } ca && b.Cylinder is { } cb)
                return CylinderKey(ca) == CylinderKey(cb);
            return false;
        }
        return tessellation.Value.EdgePolylines.Where(e => !Smooth(e.EdgeId))
            .Select(e => (e.IsClosed ? e.Points.Append(e.Points[0]) : e.Points).Select(P).ToArray()).ToArray();
    }

    private abstract record Feature;
    private sealed record PlanarFeature(Vector3D Normal, DisplayFaceMeshPatch[] Patches) : Feature;
    private sealed record HoleFeature(CylinderSurface[] Cylinders, DisplayFaceMeshPatch[][] Walls) : Feature;
    private sealed record Spec(Step242SemanticPmiInspectionItem Item, DisplayFaceMeshPatch[] Targets, Feature Feature);
    private sealed record Placement(PmiPlane Plane, double X, double Y, PmiCanvas Leader, Point3D Anchor)
    {
        /// <summary>Vertical distance from the annotation plane down to the anchor on the support.</summary>
        public double Drop { get; init; }
    }

    private static string CylinderKey(CylinderSurface c)
    {
        var axis = c.Axis.ToVector(); if (axis.Z < 0 || axis.Z == 0 && (axis.Y < 0 || axis.Y == 0 && axis.X < 0)) axis = -axis;
        var o = c.Origin - Point3D.Origin; var foot = o - axis * o.Dot(axis);
        return string.Create(CultureInfo.InvariantCulture, $"{Math.Round(c.Radius, 6)}|{Math.Round(axis.X, 6)},{Math.Round(axis.Y, 6)},{Math.Round(axis.Z, 6)}|{Math.Round(foot.X, 5)},{Math.Round(foot.Y, 5)},{Math.Round(foot.Z, 5)}");
    }

    private static PmiDisplayAnnotation Emit(Spec spec, PmiCanvas canvas, PmiPlane plane, Point3D anchor, double height, double lift,
        PmiCanvas? dimensions, PmiPlane dimensionPlane)
    {
        var item = spec.Item;
        var text = item.Kind switch
        {
            "Datum" => $"Datum {item.Name}",
            "Diameter" or "Dimension" => PmiDrafting.Size(item),
            _ => $"{item.Kind} {(item.ZoneForms.Contains("between two equidistant surfaces") ? "BetweenSurfaces " : PmiDrafting.ZonePrefix(item.ZoneForms))}{PmiDrafting.Number(item.Value!.Value)}"
                + string.Concat(item.Modifiers.Select(m => m switch { "MAXIMUM_MATERIAL_REQUIREMENT" => " (M)", "LEAST_MATERIAL_REQUIREMENT" => " (L)", _ => " " + m }))
                + (item.DatumReferences.Count > 0 ? " | " + string.Join(" | ", item.DatumReferences) : "")
        };
        var lines = Lines3D(canvas, plane, 0).ToList();
        (double X, double Y) label = canvas.Lines.Count > 0 ? canvas.Lines[0][0] : (0d, 0d);
        var fill = Fill3D(canvas.Fill, canvas.FillIndices, plane, lift * 2);
        var panel = Fill3D(canvas.Panel, canvas.PanelIndices, plane, -lift);
        if (dimensions is not null)
        {
            lines.AddRange(Lines3D(dimensions, dimensionPlane, 0));
            fill = Merge(fill, Fill3D(dimensions.Fill, dimensions.FillIndices, dimensionPlane, lift * 2));
            panel = Merge(panel, Fill3D(dimensions.Panel, dimensions.PanelIndices, dimensionPlane, -lift));
        }
        return new("pmi:" + item.EntityId, item.Kind, item.AuthoredName ?? item.Name, text, item.Value, item.TolerancePlus, item.ToleranceMinus,
            item.GeometricFaceEntityIds, P(anchor), P(plane.At(label.X, label.Y)), height, lines)
        { Target = Highlight(spec, height), TextFill = fill, Panel = panel };
    }

    private static PmiDisplayMesh? Merge(PmiDisplayMesh? a, PmiDisplayMesh? b) => a is null ? b : b is null ? a
        : new(a.Positions.Concat(b.Positions).ToArray(), a.Indices.Concat(b.Indices.Select(i => i + a.Positions.Length / 3)).ToArray());

    private static IReadOnlyList<double[][]> Lines3D(PmiCanvas canvas, PmiPlane plane, double lift) =>
        canvas.Lines.Select(line => line.Select(p => P(plane.At(p.X, p.Y, lift))).ToArray()).ToArray();

    private static PmiDisplayMesh? Fill3D(List<(double X, double Y)> points, List<int> indices, PmiPlane plane, double lift) =>
        indices.Count == 0 ? null : new(points.SelectMany(p => P(plane.At(p.X, p.Y, lift))).ToArray(), indices.ToArray());

    /// <summary>The highlighted support: every associated patch, lifted off the surface.</summary>
    private static PmiDisplayTarget Highlight(Spec spec, double height)
    {
        var offset0 = height * .002;
        var positions = new List<double>(); var normals = new List<double>(); var indices = new List<int>();
        foreach (var patch in spec.Targets)
        {
            var offset = positions.Count / 3;
            positions.AddRange(patch.Positions.Zip(patch.Normals, (p, n) => new[] { p.X + n.X * offset0, p.Y + n.Y * offset0, p.Z + n.Z * offset0 }).SelectMany(v => v));
            normals.AddRange(patch.Normals.SelectMany(n => new[] { n.X, n.Y, n.Z }));
            indices.AddRange(patch.TriangleIndices.Select(i => i + offset));
        }
        var normal = spec.Feature is PlanarFeature planar ? V(planar.Normal) : [0d, 0d, 1d];
        return new(spec.Feature is PlanarFeature ? "PlanePatch" : "CylinderWall", normal, 0, positions.ToArray(), normals.ToArray(), indices.ToArray(), [])
            { SurfaceOffset = offset0 };
    }

    /// <summary>
    /// Boxed basic dimensions from each planar datum referenced by a Position control to every
    /// controlled hole axis, drawn in the datum face's top plane as baseline dimensions.
    /// </summary>
    private static Dictionary<int, PmiCanvas> BasicDimensions(IReadOnlyList<Spec> specs, IReadOnlyDictionary<string, Spec> datums, Layout layout)
    {
        var result = new Dictionary<int, PmiCanvas>();
        var requests = new List<(Spec Owner, Vector3D Normal, double DatumOffset, Point3D Axis, string Datum)>();
        foreach (var position in specs.Where(s => s.Item.Kind == "Position" && s.Feature is HoleFeature))
            foreach (var label in position.Item.DatumReferences)
                if (datums.TryGetValue(label, out var datum) && datum.Feature is PlanarFeature { } face && Math.Abs(face.Normal.Z) < 1e-7)
                {
                    var origin = datum.Targets[0].Positions[0];
                    foreach (var cylinder in ((HoleFeature)position.Feature).Cylinders)
                        if (Math.Abs(cylinder.Axis.ToVector().Z) > 1 - 1e-7)
                            requests.Add((position, face.Normal, (origin - Point3D.Origin).Dot(face.Normal), cylinder.Origin, label));
                }
        int tiersX = 0, tiersY = 0;
        foreach (var direction in requests.GroupBy(r => (Math.Round(r.Normal.X), Math.Round(r.Normal.Y))))
        {
            var values = direction.Select(r => (r, Value: Math.Abs((r.Axis - Point3D.Origin).Dot(r.Normal) - r.DatumOffset)))
                .GroupBy(v => Math.Round(v.Value, 6)).Select(g => g.First()).OrderBy(v => v.Value).ToArray();
            for (var tier = 0; tier < values.Length; tier++)
            {
                var (request, value) = values[tier];
                var canvas = result.TryGetValue(request.Owner.Item.EntityId, out var existing) ? existing : result[request.Owner.Item.EntityId] = new PmiCanvas();
                layout.BasicDimension(canvas, request.Normal, request.DatumOffset, request.Axis, value, tier);
            }
            if (Math.Abs(direction.Key.Item1) > .5) tiersX = Math.Max(tiersX, values.Length); else tiersY = Math.Max(tiersY, values.Length);
        }
        layout.ReserveDimensionBands(tiersX, tiersY);
        return result;
    }

    /// <summary>Annotation-plane placement with simple collision avoidance in each plane.</summary>
    private sealed class Layout(double h, double minX, double maxX, double minY, double maxY, double minZ, double maxZ)
    {
        private readonly Dictionary<string, List<(double X0, double Y0, double X1, double Y1)>> occupied = [];
        private readonly List<(double X0, double Y0, double X1, double Y1)> dimensionLabels = [];
        public double DimensionDepth { get; private set; }
        public double Lift => .02;
        /// <summary>Basic dimensions all live in one horizontal plane, at the top of the datum faces.</summary>
        public double DimensionLevel { get; set; } = double.NaN;

        // Parallel planes overlap in the view that reads them, so they share one occupancy map.
        private static string Key(PmiPlane plane) => string.Create(CultureInfo.InvariantCulture,
            $"{plane.N.X:0.###},{plane.N.Y:0.###},{plane.N.Z:0.###}");

        private bool Free(PmiPlane plane, (double X0, double Y0, double X1, double Y1) r, bool outsideBody)
        {
            if (outsideBody)
            {
                var (bx0, by0) = plane.Local(new Point3D(minX, minY, minZ)); var (bx1, by1) = plane.Local(new Point3D(maxX, maxY, maxZ));
                var pad = .6 * h;
                if (r.X1 > Math.Min(bx0, bx1) - pad && r.X0 < Math.Max(bx0, bx1) + pad && r.Y1 > Math.Min(by0, by1) - pad && r.Y0 < Math.Max(by0, by1) + pad) return false;
            }
            var list = occupied.GetValueOrDefault(Key(plane)) ?? [];
            var m = .5 * h;
            return list.All(o => r.X1 + m <= o.X0 || r.X0 - m >= o.X1 || r.Y1 + m <= o.Y0 || r.Y0 - m >= o.Y1);
        }

        private void Occupy(PmiPlane plane, (double X0, double Y0, double X1, double Y1) r)
        {
            if (!occupied.TryGetValue(Key(plane), out var list)) occupied[Key(plane)] = list = [];
            list.Add(r);
        }

        /// <summary>Leader from the hole's visible rim, out and away from the part, with a horizontal shoulder.</summary>
        public Placement PlaceHoleCallout(HoleFeature hole, Point3D center, double width, double height, Point3D[] bodyPoints)
        {
            // The callout points at the hole nearest the default front-left view, and leaves the part
            // along whichever diagonal reaches free space soonest (outward from the part first).
            var cylinder = hole.Cylinders.OrderBy(c => c.Origin.X + c.Origin.Y).First();
            var r = cylinder.Radius;
            var level = bodyPoints.Where(p => Math.Pow(p.X - cylinder.Origin.X, 2) + Math.Pow(p.Y - cylinder.Origin.Y, 2) <= Math.Pow(2.2 * r + h, 2)).Max(p => p.Z);
            var plane = new PmiPlane(new Point3D(0, 0, level), new Vector3D(1, 0, 0), new Vector3D(0, 1, 0));
            var axis = (X: cylinder.Origin.X, Y: cylinder.Origin.Y);
            var sx = Math.Sign(axis.X - center.X); var sy = Math.Sign(axis.Y - center.Y);
            var preferred = (X: sx == 0 ? -1 : sx, Y: sy == 0 ? -1 : sy);
            var directions = new[] { preferred, (X: -1, Y: -1), (X: 1, Y: -1), (X: -1, Y: 1), (X: 1, Y: 1) }.Distinct().ToArray();
            // The front faces the default view: try the front diagonals first within a modest reach.
            var passes = new[] { (Directions: directions.Where(d => d.Y < 0).ToArray(), Limit: 32 * h), (Directions: directions, Limit: 90 * h) };
            foreach (var (candidates, limit) in passes)
            for (var reach = 3.5 * h; reach < limit; reach += .5 * h)
                foreach (var d in candidates)
                {
                    var outward = (X: d.X * Math.Sqrt(.5), Y: d.Y * Math.Sqrt(.5));
                    var tip = (X: axis.X + outward.X * r, Y: axis.Y + outward.Y * r);
                    var elbow = (X: tip.X + outward.X * reach, Y: tip.Y + outward.Y * reach);
                    var shoulder = 1.2 * h;
                    var x = outward.X >= 0 ? elbow.X + shoulder : elbow.X - shoulder - width;
                    var rect = (x, elbow.Y - height / 2, x + width, elbow.Y + height / 2);
                    if (!Free(plane, rect, true)) continue;
                    Occupy(plane, rect);
                    var leader = new PmiCanvas();
                    leader.Line(tip, elbow, (outward.X >= 0 ? x : x + width, elbow.Y));
                    Arrow(leader, tip, (-outward.X, -outward.Y));
                    // The callout belongs to the controlled wall: when it starts below the entry
                    // (inside a counterbore), the leader drops to the wall's rim.
                    var wallTop = hole.Walls.SelectMany(w => w).SelectMany(p => p.Positions).Max(p => p.Z);
                    var anchor = new Point3D(tip.X, tip.Y, Math.Min(level, wallTop));
                    return new(plane, x, elbow.Y - height / 2, leader, anchor) { Drop = level - anchor.Z };
                }
            throw new InvalidOperationException("pmi-display-layout-no-space: a hole callout has no free position.");
        }

        /// <summary>Reserve the band a set of baseline dimensions occupies, extension lines included.</summary>
        public void ReserveDimensionBands(int tiersAlongX, int tiersAlongY)
        {
            var plane = new PmiPlane(new Point3D(0, 0, DimensionLevel), new Vector3D(1, 0, 0), new Vector3D(0, 1, 0));
            var spacing = 2.6 * h; var first = 3.2 * h;
            DimensionDepth = first + (Math.Max(tiersAlongX, 1) - 1) * spacing + 1.2 * h;
            if (tiersAlongX > 0) Occupy(plane, (minX - h, minY - first - (tiersAlongX - 1) * spacing - 1.2 * h, maxX + h, minY));
            if (tiersAlongY > 0) Occupy(plane, (minX - first - (tiersAlongY - 1) * spacing - 1.6 * h, minY - h, minX, maxY + h));
        }

        /// <summary>Reserve a rectangle in a plane (for blocks placed by the caller).</summary>
        public void Reserve(PmiPlane plane, (double X0, double Y0, double X1, double Y1) rect) => Occupy(plane, rect);

        /// <summary>A planar feature is shown edge-on: datum triangle or arrow on the face line, stem along the normal.</summary>
        public Placement PlacePlanarCallout(PlanarFeature face, Spec first, double width, double height, bool datum)
        {
            var n = face.Normal;
            var facePoints = face.Patches.SelectMany(p => p.Positions).ToArray();
            PmiPlane plane; (double X, double Y) anchor; (double X, double Y) dir; bool constrain;
            if (Math.Abs(n.Z) > .9)
            {
                // Horizontal face: drawn in the front plane through its nearest edge.
                var y0 = facePoints.Min(p => p.Y);
                plane = new PmiPlane(new Point3D(0, y0, 0), new Vector3D(1, 0, 0), new Vector3D(0, 0, 1));
                var z = facePoints[0].Z;
                var xs = facePoints.Where(p => Math.Abs(p.Y - y0) < 1e-6).Select(p => p.X).DefaultIfEmpty(facePoints.Average(p => p.X)).ToArray();
                var x0 = xs.Min(); var x1 = xs.Max();
                anchor = (x0 + (x1 - x0) * (datum ? .3 : .7), z);
                dir = (0, n.Z > 0 ? 1 : -1);
                constrain = y0 > minY + 1e-6;
            }
            else
            {
                // Vertical face: drawn in the horizontal plane through its top edge.
                var z = facePoints.Max(p => p.Z);
                plane = new PmiPlane(new Point3D(0, 0, z), new Vector3D(1, 0, 0), new Vector3D(0, 1, 0));
                var top = facePoints.Where(p => Math.Abs(p.Z - z) < 1e-6).ToArray();
                var tangent = new Vector3D(-n.Y, n.X, 0);
                var ts = top.Select(p => (p - Point3D.Origin).Dot(tangent)).ToArray();
                dir = (n.X, n.Y);
                // Slide along the face until the stem clears every dimension value.
                (double X, double Y) Along(double fraction)
                {
                    var t = ts.Min() + (ts.Max() - ts.Min()) * fraction;
                    var basePoint = top[0] + tangent * (t - (top[0] - Point3D.Origin).Dot(tangent));
                    return (basePoint.X, basePoint.Y);
                }
                var direction = dir;
                bool Clear((double X, double Y) a) => dimensionLabels.All(r =>
                {
                    var far = (X: a.X + direction.X * 1000, Y: a.Y + direction.Y * 1000);
                    var x0 = Math.Min(a.X, far.X) - .6 * h; var x1 = Math.Max(a.X, far.X) + .6 * h;
                    var y0 = Math.Min(a.Y, far.Y) - .6 * h; var y1 = Math.Max(a.Y, far.Y) + .6 * h;
                    return r.X1 < x0 || r.X0 > x1 || r.Y1 < y0 || r.Y0 > y1;
                });
                anchor = new[] { .72, .5, .85, .3, .62, .4, .15, .92 }.Select(Along).FirstOrDefault(Clear, Along(.72));
                constrain = z < maxZ - 1e-6;
            }
            for (var reach = 1.6 * h; reach < 80 * h; reach += .5 * h)
            {
                var end = (X: anchor.X + dir.X * reach, Y: anchor.Y + dir.Y * reach);
                // The block attaches by the middle of its near edge.
                var x = dir.X > .5 ? end.X : dir.X < -.5 ? end.X - width : end.X - (datum ? 0 : width / 2) - (datum ? width / 2 : 0);
                var y = dir.Y > .5 ? end.Y : dir.Y < -.5 ? end.Y - height : end.Y - height / 2;
                if (Math.Abs(dir.Y) > .5) x = end.X - width / 2;
                var rect = (x, y, x + width, y + height);
                if (!Free(plane, rect, constrain)) continue;
                Occupy(plane, rect);
                var leader = new PmiCanvas();
                if (datum)
                {
                    // Filled datum triangle with its base on the feature.
                    var side = (X: -dir.Y, Y: dir.X);
                    var b = .55 * h; var a = .95 * h;
                    leader.Line(anchor, end);
                    leader.Triangle((anchor.X + side.X * b, anchor.Y + side.Y * b), (anchor.X - side.X * b, anchor.Y - side.Y * b),
                        (anchor.X + dir.X * a, anchor.Y + dir.Y * a));
                    leader.Line((anchor.X + side.X * b, anchor.Y + side.Y * b), (anchor.X - side.X * b, anchor.Y - side.Y * b));
                }
                else
                {
                    leader.Line(anchor, end);
                    Arrow(leader, anchor, (-dir.X, -dir.Y));
                }
                return new(plane, x, y, leader, plane.At(anchor.X, anchor.Y));
            }
            throw new InvalidOperationException($"pmi-display-layout-no-space: '{first.Item.Name}' has no free position.");
        }

        /// <summary>One baseline basic dimension: extension lines, a dimension line with arrows and a boxed value.</summary>
        public void BasicDimension(PmiCanvas canvas, Vector3D datumNormal, double datumOffset, Point3D axis, double value, int tier)
        {
            // Dimensions along X sit in front of the part; dimensions along Y sit to its left.
            var alongX = Math.Abs(datumNormal.X) > .5;
            var plane = new PmiPlane(new Point3D(0, 0, DimensionLevel), new Vector3D(1, 0, 0), new Vector3D(0, 1, 0));
            var spacing = 2.6 * h; var first = 3.2 * h;
            var datumCoordinate = datumOffset * (alongX ? datumNormal.X : datumNormal.Y);
            var featureCoordinate = alongX ? axis.X : axis.Y;
            var label = PmiDrafting.TextBlock(PmiDrafting.Number(value), h, boxed: true);
            if (alongX)
            {
                var y = minY - first - tier * spacing;
                canvas.Line((datumCoordinate, minY - .4 * h), (datumCoordinate, y - .6 * h));
                canvas.Line((featureCoordinate, axis.Y), (featureCoordinate, y - .6 * h));
                canvas.Line((datumCoordinate, y), (featureCoordinate, y));
                var s = Math.Sign(featureCoordinate - datumCoordinate);
                Arrow(canvas, (datumCoordinate, y), (-s, 0)); Arrow(canvas, (featureCoordinate, y), (s, 0));
                var span = Math.Abs(featureCoordinate - datumCoordinate);
                // A short dimension keeps its value beyond the feature end rather than over the arrows.
                var cx = span < label.Width + 2.4 * h ? featureCoordinate + s * (1.2 * h) - (s < 0 ? label.Width : 0)
                    : (datumCoordinate + featureCoordinate) / 2 - label.Width / 2;
                canvas.Append(label.Canvas, cx, y - label.Height / 2);
                dimensionLabels.Add((cx, y - label.Height / 2, cx + label.Width, y + label.Height / 2));
                Occupy(plane, (Math.Min(datumCoordinate, featureCoordinate), y - label.Height / 2, Math.Max(datumCoordinate, featureCoordinate), y + label.Height / 2));
            }
            else
            {
                var x = minX - first - tier * spacing;
                canvas.Line((minX - .4 * h, datumCoordinate), (x - .6 * h, datumCoordinate));
                canvas.Line((axis.X, featureCoordinate), (x - .6 * h, featureCoordinate));
                canvas.Line((x, datumCoordinate), (x, featureCoordinate));
                var s = Math.Sign(featureCoordinate - datumCoordinate);
                Arrow(canvas, (x, datumCoordinate), (0, -s)); Arrow(canvas, (x, featureCoordinate), (0, s));
                var span = Math.Abs(featureCoordinate - datumCoordinate);
                var cy = span < label.Height + 2.4 * h ? featureCoordinate + s * (1.2 * h) - (s < 0 ? label.Height : 0)
                    : (datumCoordinate + featureCoordinate) / 2 - label.Height / 2;
                canvas.Append(label.Canvas, x - label.Width / 2, cy);
                dimensionLabels.Add((x - label.Width / 2, cy, x + label.Width / 2, cy + label.Height));
                Occupy(plane, (x - label.Width / 2, Math.Min(datumCoordinate, featureCoordinate), x + label.Width / 2, Math.Max(datumCoordinate, featureCoordinate)));
            }
            // Mark the controlled axis.
            canvas.Line((axis.X - .8 * h, axis.Y), (axis.X + .8 * h, axis.Y));
            canvas.Line((axis.X, axis.Y - .8 * h), (axis.X, axis.Y + .8 * h));
        }

        private void Arrow(PmiCanvas canvas, (double X, double Y) tip, (double X, double Y) pointing)
        {
            var length = Math.Sqrt(pointing.X * pointing.X + pointing.Y * pointing.Y);
            var d = (X: pointing.X / length, Y: pointing.Y / length); var side = (X: -d.Y, Y: d.X);
            var l = .9 * h; var w = .22 * h;
            canvas.Triangle(tip, (tip.X - d.X * l + side.X * w, tip.Y - d.Y * l + side.Y * w), (tip.X - d.X * l - side.X * w, tip.Y - d.Y * l - side.Y * w));
        }
    }

    private static double[] P(Point3D p) => [p.X, p.Y, p.Z];
    private static double[] V(Vector3D v) => [v.X, v.Y, v.Z];

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
