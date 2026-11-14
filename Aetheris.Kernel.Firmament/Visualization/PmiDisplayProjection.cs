using System.Globalization;
using Aetheris.Kernel.Core.Brep;
using Aetheris.Kernel.Core.Brep.Tessellation;
using Aetheris.Kernel.Core.Geometry;
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
    /// <summary>Drafting lines without glyph outlines (leaders, frames, dimension lines) for renderers that fill text.</summary>
    [System.Text.Json.Serialization.JsonIgnore] public IReadOnlyList<double[][]>? DraftingLines { get; init; }
    /// <summary>Leader colour (linear RGB) tying a callout to its feature; null draws the default ink.</summary>
    [System.Text.Json.Serialization.JsonIgnore] public double[]? LeaderColor { get; init; }
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
        // The viewport's own tessellation, so target patches lie on the displayed faces.
        var tessellation = BrepDisplayTessellator.Tessellate(body, options ?? DisplayTessellationOptions.ForViewport(body));
        if (!tessellation.IsSuccess) throw new InvalidOperationException("pmi-display-target-unavailable");
        var patches = tessellation.Value.FacePatches.ToDictionary(p => p.FaceId.Value);
        var sourceFaces = body.Bindings.FaceBindings.Where(b => b.SourceStepEntityId.HasValue)
            .ToDictionary(b => b.SourceStepEntityId!.Value, b => b.FaceId.Value);
        var points = patches.Values.SelectMany(p => p.Positions).ToArray();
        if (points.Length == 0) throw new InvalidOperationException("pmi-display-target-unavailable: No displayable faces are available for callouts.");
        var minX = points.Min(p => p.X); var maxX = points.Max(p => p.X);
        var minY = points.Min(p => p.Y); var maxY = points.Max(p => p.Y);
        var minZ = points.Min(p => p.Z); var maxZ = points.Max(p => p.Z);
        // Text scales with the part: large parts (hundreds of mm) still read at a whole-part view.
        var height = Math.Clamp(Math.Max(Math.Max(maxX - minX, maxY - minY), maxZ - minZ) / 34, .5, 24);
        var center = new Point3D((minX + maxX) / 2, (minY + maxY) / 2, (minZ + maxZ) / 2);

        // 1. Resolve every semantic record to its exact supports.
        var specs = new List<Spec>();
        var notes = new List<Step242SemanticPmiInspectionItem>();
        var projected = new List<Step242SemanticPmiInspectionItem>();
        var between = new List<(Step242SemanticPmiInspectionItem Item, Vector3D Normal, IReadOnlyList<Point3D> A, IReadOnlyList<Point3D> B)>();
        var fromModel = new HashSet<int>();
        var unlocated = new List<Step242SemanticPmiInspectionItem>();
        PlanarFeature Lead(DisplayFaceMeshPatch[] faces)
        {
            var lead = faces.MaxBy(Area)!;
            var sum = new Vector3D(0, 0, 0);
            for (var i = 0; i + 2 < lead.TriangleIndices.Count; i += 3)
            {
                var a = lead.Positions[lead.TriangleIndices[i]]; var b = lead.Positions[lead.TriangleIndices[i + 1]]; var c = lead.Positions[lead.TriangleIndices[i + 2]];
                sum += (b - a).Cross(c - a);
            }
            var normal = sum.Length > 1e-12 ? sum / sum.Length : new Vector3D(0, 0, 1);
            return new PlanarFeature(normal, [lead]);
        }
        static double Area(DisplayFaceMeshPatch patch)
        {
            var area = 0d;
            for (var i = 0; i + 2 < patch.TriangleIndices.Count; i += 3)
            {
                var a = patch.Positions[patch.TriangleIndices[i]]; var b = patch.Positions[patch.TriangleIndices[i + 1]]; var c = patch.Positions[patch.TriangleIndices[i + 2]];
                area += (b - a).Cross(c - a).Length / 2;
            }
            return area;
        }

        foreach (var inspected in inspection.Items)
        {
            var item = inspected;
            // Part notes read back as AP242 annotations (material, general tolerances, finish).
            if (item.Kind == "Annotation") { notes.Add(item); continue; }
            // Sizes and locations projected from construction are drawn on their features below.
            if (item.Kind == "Dimension" && item.Target.StartsWith("compose:", StringComparison.Ordinal)) { projected.Add(item); continue; }
            if (!item.PresentationSupported)
                throw new InvalidOperationException($"pmi-display-parameters-unsupported: '{item.Name}' retains advanced semantic parameters that this 3D label path cannot yet display completely: {string.Join(", ", item.PresentationGaps)}.");
            if (item.Kind is not ("Datum" or "Diameter" or "Dimension" or "Symmetry" or "Concentricity") && Step242GeometricToleranceKinds.Find(item.Kind) is null)
                throw new InvalidOperationException($"pmi-display-kind-unsupported: {item.Kind} '{item.Name}' has no qualified 3D callout yet.");
            var targets = item.GeometricFaceEntityIds.Where(id => sourceFaces.TryGetValue(id, out var face)
                    && patches.TryGetValue(face, out var patch) && patch.Positions.Count > 0 && patch.TriangleIndices.Count >= 3)
                .Select(id => patches[sourceFaces[id]]).ToArray();
            // A record the file attaches to no face of the part (auxiliary geometry, or nothing) is listed with the
            // part's notes, by its callout text, rather than given a leader to a guessed face.
            if (targets.Length == 0)
            {
                unlocated.Add(item);
                continue;
            }
            var surfaces = targets.Select(t => body.GetFaceSurface(t.FaceId)).ToArray();
            // A size or location the file states without a value (no characteristic representation) is drawn with the
            // model's nominal, in parentheses: a reference value, not a requirement the file made.
            if (item.Kind == "Dimension" && item.Value is null && ModelNominal(item, surfaces) is { } nominal)
            { item = item with { Value = nominal }; fromModel.Add(item.EntityId); }
            if (item.Kind != "Datum" && item.Value is null) throw new InvalidOperationException($"pmi-display-value-unresolved: '{item.Name}' has no semantic magnitude.");
            // A distance between two parallel faces is drawn between them, whatever their orientation.
            if (item.Kind == "Dimension" && item.DimensionKind is "distance" or "linear distance" && surfaces.All(s => s.Plane is not null))
            {
                var n0 = surfaces[0].Plane!.Value.Normal.ToVector();
                var sides = targets.Zip(surfaces).GroupBy(t => Math.Round((t.Second.Plane!.Value.Origin - Point3D.Origin).Dot(n0), 6)).ToArray();
                // Exactly two parallel sides are drawn as a dimension between them; any other set points at a face below.
                if (sides.Length == 2 && surfaces.All(s => s.Plane!.Value.Normal.ToVector().Cross(n0).Length <= 1e-7))
                {
                    between.Add((item, n0, sides[0].SelectMany(t => t.First.Positions).ToArray(), sides[1].SelectMany(t => t.First.Positions).ToArray()));
                    continue;
                }
            }
            Feature feature;
            static bool OnePlane(SurfaceGeometry[] faces)
            {
                var plane = faces[0].Plane!.Value; var n = plane.Normal.ToVector();
                return faces.All(s => s.Plane!.Value.Normal.ToVector().Cross(n).Length <= 1e-7 && Math.Abs((s.Plane!.Value.Origin - plane.Origin).Dot(n)) <= 1e-7);
            }
            if (surfaces.All(s => s.Plane is not null) && OnePlane(surfaces))
            {
                // Construction may split one physical face; it remains one plane.
                feature = new PlanarFeature(surfaces[0].Plane!.Value.Normal.ToVector(), targets);
            }
            else if (surfaces.Any(s => s.Plane is not null) && !(surfaces.All(s => s.Cylinder is not null)))
            {
                // A requirement on several faces (a profile of a surface set, a feature of size between two planes, an
                // angle): the callout points at its largest planar face; every face stays highlighted as the target.
                var lead = targets.Zip(surfaces).Where(t => t.Second.Plane is not null).MaxBy(t => Area(t.First));
                feature = new PlanarFeature(lead.Second.Plane!.Value.Normal.ToVector(), [lead.First]);
            }
            else if (surfaces.All(s => s.Cylinder is not null))
            {
                // One or more holes (a quantity callout); each hole is one connected wall on one cylinder.
                var walls = targets.GroupBy(t => CylinderKey(body.GetFaceSurface(t.FaceId).Cylinder!.Value)).Select(g => g.ToArray()).ToArray();
                // A quantity callout covers several alike holes; any other set of cylinders (a coaxial stack, holes of
                // different sizes, a wall split into pieces that do not connect) points at its largest wall.
                var sameSize = walls.Select(w => Math.Round(body.GetFaceSurface(w[0].FaceId).Cylinder!.Value.Radius, 9)).Distinct().Count() == 1;
                // A size on several alike holes is one callout on all of them; when the record states no quantity, the
                // callout states the number of holes it targets (2X).
                if (walls.Length > 1 && sameSize && item.Kind is "Diameter" or "Dimension" && item.Quantity is null) item = item with { Quantity = walls.Length };
                var alike = walls.Length > 1 && item.Quantity == walls.Length && sameSize;
                // One size cannot hold for cylinders of different sizes: that set is rejected, not drawn on one of them.
                if (walls.Length > 1 && !alike && item.Kind is "Diameter" or "Dimension"
                    && walls.Select(w => Math.Round(body.GetFaceSurface(w[0].FaceId).Cylinder!.Value.Radius, 9)).Distinct().Count() > 1)
                    throw new InvalidOperationException($"pmi-display-target-multiple-unsupported: '{item.Name}' sizes {walls.Length} cylinders of different diameters.");
                if (walls.Length > 1 && !alike) walls = [walls.MaxBy(w => body.GetFaceSurface(w[0].FaceId).Cylinder!.Value.Radius)!];
                // Hole callouts are drawn from the rim of a hole drilled along Z; a hole on another axis points at its wall.
                feature = walls.All(w => OneCylindricalWall(body, w))
                    && walls.All(w => Math.Abs(Math.Abs(body.GetFaceSurface(w[0].FaceId).Cylinder!.Value.Axis.ToVector().Z) - 1) < 1e-6)
                    ? new HoleFeature(walls.Select(w => body.GetFaceSurface(w[0].FaceId).Cylinder!.Value).ToArray(), walls)
                    : Lead(targets);
            }
            // Cones, tori, spheres and freeform faces: the callout points at the largest face along its mean normal.
            else feature = Lead(targets);
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
        DisplayFaceMeshPatch[] Patches(Step242SemanticPmiInspectionItem item)
        {
            var found = item.GeometricFaceEntityIds.Where(id => sourceFaces.TryGetValue(id, out var face) && patches.ContainsKey(face))
                .Select(id => patches[sourceFaces[id]]).ToArray();
            if (found.Length == 0 || found.Length != item.GeometricFaceEntityIds.Count)
                throw new InvalidOperationException($"pmi-display-target-unresolved: '{item.Name}' has no complete geometric face association.");
            return found;
        }
        static string FeatureKey(Step242SemanticPmiInspectionItem item) { var t = item.Target["compose:".Length..]; var hash = t.IndexOf('#'); return hash < 0 ? t : t[..hash]; }
        var locations = new List<(int Owner, Vector3D Normal, double DatumOffset, Point3D Point, bool Boxed)>();
        foreach (var item in projected.Where(i => i.DimensionKind?.StartsWith("location ", StringComparison.Ordinal) == true))
        {
            var words = item.DimensionKind!.Split(' ');
            var n = words[1] switch { "-X" => new Vector3D(-1, 0, 0), "+X" => new Vector3D(1, 0, 0), "-Y" => new Vector3D(0, -1, 0), "+Y" => new Vector3D(0, 1, 0),
                _ => throw new InvalidOperationException($"pmi-display-location-unsupported: '{item.Name}' runs from {words[1]}.") };
            var faces = Patches(item);
            var offsets = faces.Select(f => (Face: f, Plane: body.GetFaceSurface(f.FaceId).Plane)).ToArray();
            // The origin is the extreme plane in that direction; the rest is the located feature.
            var datumOffset = offsets.Where(f => f.Plane is { } p && p.Normal.ToVector().Dot(n) > 1 - 1e-8).Max(f => (f.Plane!.Value.Origin - Point3D.Origin).Dot(n));
            var feature = offsets.Where(f => !(f.Plane is { } p && p.Normal.ToVector().Dot(n) > 1 - 1e-8 && Math.Abs((p.Origin - Point3D.Origin).Dot(n) - datumOffset) < 1e-7))
                .SelectMany(f => f.Face.Positions).ToArray();
            var point = new Point3D((feature.Min(p => p.X) + feature.Max(p => p.X)) / 2, (feature.Min(p => p.Y) + feature.Max(p => p.Y)) / 2, feature.Max(p => p.Z));
            locations.Add((item.EntityId, n, datumOffset, point, words.Contains("basic")));
        }
        var basics = BasicDimensions(specs, datumFaces, locations, layout);
        var dimensionPlane = new PmiPlane(new Point3D(0, 0, layout.DimensionLevel), new Vector3D(1, 0, 0), new Vector3D(0, 1, 0));

        var noteLines = notes.Select(n => n.Text ?? "").Concat(unlocated.Select(u => "NOT LOCATED ON PART: " + CalloutText(u))).ToArray();
        var notesBlock = noteLines.Length > 0 ? PmiDrafting.Notes(noteLines, .8 * height) : null;

        var attachedTo = new Dictionary<int, int>(); // records drawn as part of another record's line
        // Each feature's leaders and dimensions share a colour, so a callout reads with its feature in 3D.
        double[][] palette = [[.1, .36, .78], [.86, .5, .04], [.12, .56, .3], [.55, .24, .7], [.05, .55, .6], [.75, .2, .3]];
        var nextColor = 0;
        var featureColors = new Dictionary<string, double[]>(StringComparer.Ordinal);

        // Holes first, so planar datum symbols find free space outside dimensions and hole callouts.
        foreach (var group in groups.OrderBy(g => g[0].Feature is HoleFeature ? 0 : 1).ThenByDescending(g => g[0].Feature is HoleFeature hole ? hole.Cylinders.Length : 0))
        {
            var ordered = group.OrderBy(s => s.Item.Kind switch { "Diameter" or "Dimension" => 0, "Datum" => 3, "Position" => 2, _ => 1 }).ToArray();
            var blockList = ordered.Select(s => (Spec: s, Block: s.Item.Kind switch
            {
                "Diameter" or "Dimension" => PmiDrafting.TextBlock(fromModel.Contains(s.Item.EntityId) ? $"({PmiDrafting.Size(s.Item)})" : PmiDrafting.Size(s.Item), height),
                "Datum" => PmiDrafting.DatumBox(s.Item.Name, height),
                _ => PmiDrafting.ControlFrame(s.Item.Kind, PmiDrafting.ZonePrefix(s.Item.ZoneForms), s.Item.Value!.Value,
                    s.Item.Modifiers, s.Item.DatumReferenceFrames.Count > 0
                        ? s.Item.DatumReferenceFrames.Select(f => string.Join("-", f.Labels)).ToArray() : s.Item.DatumReferences, height, s.Item.ToleranceParameters)
            })).ToList();
            // A hole callout's counterbore line sits under its size: ⌴Ø11 ↧6.5.
            var sizeName = ordered.FirstOrDefault(s => s.Item.Kind == "Diameter")?.Item.AuthoredName;
            var extras = sizeName is null ? [] : projected.Where(p => p.Name.StartsWith(sizeName + ".Counterbore", StringComparison.Ordinal)).ToArray();
            if (extras.Length > 0)
            {
                var cbDiameter = extras.FirstOrDefault(e => e.Name.EndsWith(".CounterboreDiameter", StringComparison.Ordinal));
                var cbDepth = extras.FirstOrDefault(e => e.Name.EndsWith(".CounterboreDepth", StringComparison.Ordinal));
                var text = (cbDiameter is null ? "" : "{CB}Ø" + PmiDrafting.Number(cbDiameter.Value!.Value)) + (cbDepth is null ? "" : " {DEPTH}" + PmiDrafting.Number(cbDepth.Value!.Value));
                var owner = cbDiameter ?? cbDepth!;
                blockList.Insert(1, (new Spec(owner, ordered[0].Targets, ordered[0].Feature), PmiDrafting.CalloutLine(text.Trim(), height)));
                foreach (var other in extras.Where(e => e != owner)) attachedTo[other.EntityId] = owner.EntityId;
                foreach (var e in extras) projected.Remove(e);
            }
            var blocks = blockList.ToArray();
            var gap = .35 * height;
            var stackHeight = blocks.Sum(b => b.Block.Height) + gap * (blocks.Length - 1);
            var stackWidth = blocks.Max(b => b.Block.Width);
            var placed = group[0].Feature switch
            {
                HoleFeature hole => layout.PlaceHoleCallout(hole, center, stackWidth, stackHeight, points),
                PlanarFeature planar => layout.PlacePlanarCallout(planar, ordered[0], stackWidth, stackHeight, ordered.Any(s => s.Item.Kind == "Datum")),
                _ => throw new InvalidOperationException()
            };
            var groupColor = group[0].Feature is HoleFeature ? palette[nextColor++ % palette.Length] : null;
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
                    basics.GetValueOrDefault(spec.Item.EntityId), dimensionPlane, groupColor);
                if (i == 0 && placed.Drop > 1e-6)
                {
                    double[][] drop = [P(placed.Anchor), P(placed.Anchor + new Vector3D(0, 0, placed.Drop))];
                    emitted = emitted with { Lines = [drop, .. emitted.Lines], DraftingLines = [drop, .. emitted.DraftingLines!] };
                }
                output[spec.Item.EntityId] = i > 0 ? emitted with { Target = null } : emitted;
            }
        }

        // 3. Sizes projected from construction, drawn on their features.
        foreach (var feature in projected.Where(p => p.DimensionKind?.StartsWith("location ", StringComparison.Ordinal) != true)
                     .GroupBy(FeatureKey).OrderBy(g => g.Key, StringComparer.Ordinal))
        {
            var items = feature.ToArray();
            var faces = items.SelectMany(Patches).ToArray();
            if (items.All(i => i.DimensionKind is "thickness" or "length" or "width"))
            {
                foreach (var item in items) output[item.EntityId] = OverallDimension(item, Patches(item), layout, height);
                continue;
            }
            // A callout on the feature: one line per characteristic, size first.
            string Prefix(Step242SemanticPmiInspectionItem i) => i.Quantity is > 1 ? $"{i.Quantity}X " : "";
            var length = items.FirstOrDefault(i => i.DimensionKind == "length"); var width = items.FirstOrDefault(i => i.DimensionKind == "width");
            var lines = new List<(Step242SemanticPmiInspectionItem Item, string Text)>();
            if (length is not null && width is not null)
            { lines.Add((length, $"{Prefix(length)}{PmiDrafting.Number(length.Value!.Value)} × {PmiDrafting.Number(width.Value!.Value)}")); attachedTo[width.EntityId] = length.EntityId; }
            foreach (var item in items.Where(i => i != length && i != width || length is null || width is null).OrderBy(i => i.DimensionKind switch
                { "diameter" => 0, "length" or "width" => 1, "depth" => 2, "height" => 3, "radius" => 4, _ => 5 }))
            {
                if (attachedTo.ContainsKey(item.EntityId) || lines.Any(l => l.Item == item)) continue;
                var value = PmiDrafting.Number(item.Value!.Value);
                lines.Add((item, item.DimensionKind switch
                {
                    "diameter" => $"{Prefix(item)}Ø{value}",
                    "radius" => $"{Prefix(item)}R{value}",
                    "depth" => $"{{DEPTH}}{value}",
                    "height" => $"{value} HIGH",
                    "chamfer" => $"{Prefix(item)}{value} × 45°",
                    "length" => $"{Prefix(item)}{value} LONG",
                    "width" => $"{Prefix(item)}{value} WIDE",
                    var other => throw new InvalidOperationException($"pmi-display-kind-unsupported: projected {other} '{item.Name}' has no callout yet.")
                }));
            }
            var blocks = lines.Select(l => (l.Item, Block: PmiDrafting.CalloutLine(l.Text, height))).ToArray();
            var gap = .2 * height;
            var stackHeight = blocks.Sum(b => b.Block.Height) + gap * (blocks.Length - 1);
            var stackWidth = blocks.Max(b => b.Block.Width);
            var diameterFaces = items.Where(i => i.DimensionKind == "diameter").SelectMany(Patches).ToArray();
            var round = diameterFaces.Select(f => body.GetFaceSurface(f.FaceId).Cylinder).FirstOrDefault(c => c is not null);
            var placed = layout.PlaceFeatureCallout(faces, items, center, stackWidth, stackHeight, points, round);
            var featureColor = featureColors[feature.Key] = palette[nextColor++ % palette.Length];
            var y = placed.Y + stackHeight;
            for (var k = 0; k < blocks.Length; k++)
            {
                y -= blocks[k].Block.Height;
                var canvas = new PmiCanvas();
                canvas.Append(blocks[k].Block.Canvas, placed.X, y);
                if (k == 0) canvas.Append(placed.Leader, 0, 0);
                y -= gap;
                var item = blocks[k].Item;
                var lines3 = Lines3D(canvas, placed.Plane, 0).ToList();
                var drafting = Draft3D(canvas, placed.Plane).ToList();
                if (k == 0 && placed.Drop > 1e-6)
                {
                    double[][] drop = [P(placed.Anchor), P(placed.Anchor + new Vector3D(0, 0, placed.Drop))];
                    lines3.Insert(0, drop); drafting.Insert(0, drop);
                }
                output[item.EntityId] = new PmiDisplayAnnotation("pmi:" + item.EntityId, "Dimension", item.Name, blocks[k].Block is var _ ? lines[k].Text.Replace("{DEPTH}", "↧").Replace("{CB}", "⌴") : "",
                    item.Value, item.TolerancePlus, item.ToleranceMinus, item.GeometricFaceEntityIds, P(placed.Anchor), P(placed.Plane.At(placed.X, y)), height, lines3)
                { TextFill = Fill3D(canvas.Fill, canvas.FillIndices, placed.Plane, layout.Lift * 2), Panel = Fill3D(canvas.Panel, canvas.PanelIndices, placed.Plane, -layout.Lift),
                  DraftingLines = drafting, LeaderColor = featureColor };
            }
        }
        foreach (var item in projected.Where(p => p.DimensionKind?.StartsWith("location ", StringComparison.Ordinal) == true))
        {
            var canvas = basics.GetValueOrDefault(item.EntityId) ?? throw new InvalidOperationException($"pmi-display-location-unsupported: '{item.Name}'.");
            output[item.EntityId] = new PmiDisplayAnnotation("pmi:" + item.EntityId, "Dimension", item.Name, PmiDrafting.Number(item.Value!.Value),
                item.Value, item.TolerancePlus, item.ToleranceMinus, item.GeometricFaceEntityIds, P(dimensionPlane.At(0, 0)), P(dimensionPlane.At(0, 0)), height,
                Lines3D(canvas, dimensionPlane, 0))
            { TextFill = Fill3D(canvas.Fill, canvas.FillIndices, dimensionPlane, layout.Lift * 2), Panel = Fill3D(canvas.Panel, canvas.PanelIndices, dimensionPlane, -layout.Lift),
              DraftingLines = Draft3D(canvas, dimensionPlane),
              LeaderColor = featureColors.FirstOrDefault(c => c.Key.Split('+').Contains(FeatureKey(item))).Value };
        }
        foreach (var (item, normal, a, b) in between)
        {
            var text = PmiDrafting.Number(item.Value!.Value) + (item.TolerancePlus is { } plus && item.ToleranceMinus is { } minus
                ? (Math.Abs(plus - minus) < 1e-12 ? $" ±{PmiDrafting.Number(plus)}" : $" +{PmiDrafting.Number(plus)}/-{PmiDrafting.Number(minus)}") : "");
            if (fromModel.Contains(item.EntityId)) text = $"({text})";
            var (canvas, plane) = layout.Between(normal, a, b, text);
            var anchor = plane.At(canvas.Lines[0][0].X, canvas.Lines[0][0].Y);
            output[item.EntityId] = new PmiDisplayAnnotation("pmi:" + item.EntityId, "Dimension", item.Name, text, item.Value, item.TolerancePlus, item.ToleranceMinus,
                item.GeometricFaceEntityIds, P(anchor), P(anchor), height, Lines3D(canvas, plane, 0))
            { TextFill = Fill3D(canvas.Fill, canvas.FillIndices, plane, layout.Lift * 2), Panel = Fill3D(canvas.Panel, canvas.PanelIndices, plane, -layout.Lift),
              DraftingLines = Draft3D(canvas, plane) };
        }
        // Records drawn on another record's line keep their own entry, pointing at the same place.
        foreach (var (attached, owner) in attachedTo)
        {
            var host = output[owner]; var item = inspection.Items.Single(i => i.EntityId == attached);
            output[attached] = host with { Id = "pmi:" + attached, Kind = "Dimension", Name = item.Name, Value = item.Value, TolerancePlus = item.TolerancePlus,
                ToleranceMinus = item.ToleranceMinus, StepFaceEntities = item.GeometricFaceEntityIds, Lines = [[host.Anchor, host.LabelPosition]], DraftingLines = [], TextFill = null, Panel = null, Target = null };
        }

        // 4. General notes: one block in the front plane below the part, clear of the datum callouts.
        if (notesBlock is not null)
        {
            var plane = new PmiPlane(new Point3D(0, minY, 0), new Vector3D(1, 0, 0), new Vector3D(0, 0, 1));
            var block = notesBlock!;
            // Notes go last, below the part on the right, clear of every callout in the default view.
            var (x, top) = layout.PlaceNotes(plane, block.Width, block.Height);
            var canvas = new PmiCanvas();
            canvas.Append(block.Canvas, x, top - block.Height);
            var at = P(plane.At(x, top));
            foreach (var (note, index) in notes.Concat(unlocated).Select((n, k) => (n, k)))
                output[note.EntityId] = new PmiDisplayAnnotation("pmi:" + note.EntityId, note.Kind == "Annotation" ? "Note" : note.Kind, note.Name,
                    note.Kind == "Annotation" ? note.Text ?? "" : "NOT LOCATED ON PART: " + CalloutText(note),
                    null, null, null, [], at, at, height, index == 0 ? Lines3D(canvas, plane, 0) : [[at, at]])
                {
                    DraftingLines = index == 0 ? Draft3D(canvas, plane) : null,
                    TextFill = index == 0 ? Fill3D(canvas.Fill, canvas.FillIndices, plane, layout.Lift * 2) : null,
                    Panel = index == 0 ? Fill3D(canvas.Panel, canvas.PanelIndices, plane, -layout.Lift) : null
                };
        }
        foreach (var id in fromModel.Where(output.ContainsKey).Where(id => !output[id].Text.StartsWith('(')))
            output[id] = output[id] with { Text = $"({output[id].Text})" };
        return inspection.Items.Where(i => output.ContainsKey(i.EntityId)).Select(i => output[i.EntityId]).ToArray();
    }

    /// <summary>One-line text of a record, as its callout reads.</summary>
    private static string CalloutText(Step242SemanticPmiInspectionItem item) => item.Kind switch
    {
        "Datum" => "DATUM " + item.Name,
        "Diameter" or "Dimension" => item.Value is null ? item.Name + " (no value in file)" : PmiDrafting.Size(item),
        _ => $"{item.Kind} {PmiDrafting.ZonePrefix(item.ZoneForms)}{(item.Value is { } v ? PmiDrafting.Number(v) : "?")}"
            + string.Concat(item.ToleranceParameters.Select(p => " " + p))
            + (item.DatumReferenceFrames.Count > 0 ? " | " + string.Join(" | ", item.DatumReferenceFrames.Select(f => string.Join("-", f.Labels))) : "")
    };

    /// <summary>The nominal a size or location the file states without a value has on the model: a cylinder's
    /// diameter, or the separation of parallel planes. Null when the faces do not determine one.</summary>
    public static double? ModelNominal(Step242SemanticPmiInspectionItem item, SurfaceGeometry[] faces)
    {
        if (item.DimensionKind == "diameter" && faces.Select(f => f.Cylinder?.Radius).Where(r => r is not null).Distinct().ToArray() is [var r]) return 2 * r!.Value;
        if (item.DimensionKind is "linear distance" or "distance" && faces.All(f => f.Plane is not null))
        {
            var n = faces[0].Plane!.Value.Normal.ToVector();
            var offsets = faces.Select(f => (f.Plane!.Value.Origin - Point3D.Origin).Dot(n)).ToArray();
            if (faces.All(f => f.Plane!.Value.Normal.ToVector().Cross(n).Length <= 1e-7)) return offsets.Max() - offsets.Min() is var d && d > 1e-9 ? d : null;
        }
        return null;
    }

    /// <summary>Model edges for presentation, excluding parameter seams and section splits inside one smooth face.</summary>
    public static IReadOnlyList<double[][]> EdgeLines(string step, DisplayTessellationOptions? options = null)
    {
        var imported = Step242Importer.ImportBody(step);
        if (!imported.IsSuccess) throw new InvalidOperationException("pmi-display-body-unavailable");
        var body = imported.Value;
        // The viewport's own tessellation, so target patches lie on the displayed faces.
        var tessellation = BrepDisplayTessellator.Tessellate(body, options ?? DisplayTessellationOptions.ForViewport(body));
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

    private static PmiDisplayAnnotation OverallDimension(Step242SemanticPmiInspectionItem item, DisplayFaceMeshPatch[] faces, Layout layout, double height)
    {
        var text = (item.Quantity is > 1 ? $"{item.Quantity}X " : "") + PmiDrafting.Number(item.Value!.Value)
            + (item.TolerancePlus is { } plus && item.ToleranceMinus is { } minus ? (Math.Abs(plus - minus) < 1e-12 ? $" ±{PmiDrafting.Number(plus)}" : $" +{PmiDrafting.Number(plus)}/-{PmiDrafting.Number(minus)}") : "");
        var (canvas, plane) = layout.Overall(item.DimensionKind!, faces.SelectMany(f => f.Positions).ToArray(), text);
        var anchor = canvas.Lines[0][0];
        return new PmiDisplayAnnotation("pmi:" + item.EntityId, "Dimension", item.Name, text, item.Value, item.TolerancePlus, item.ToleranceMinus,
            item.GeometricFaceEntityIds, P(plane.At(anchor.X, anchor.Y)), P(plane.At(anchor.X, anchor.Y)), height, Lines3D(canvas, plane, 0))
        { TextFill = Fill3D(canvas.Fill, canvas.FillIndices, plane, layout.Lift * 2), Panel = Fill3D(canvas.Panel, canvas.PanelIndices, plane, -layout.Lift),
          DraftingLines = Draft3D(canvas, plane) };
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
        PmiCanvas? dimensions, PmiPlane dimensionPlane, double[]? color = null)
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
        var drafting = Draft3D(canvas, plane).Concat(dimensions is null ? [] : Draft3D(dimensions, dimensionPlane)).ToArray();
        return new("pmi:" + item.EntityId, item.Kind, item.AuthoredName ?? item.Name, text, item.Value, item.TolerancePlus, item.ToleranceMinus,
            item.GeometricFaceEntityIds, P(anchor), P(plane.At(label.X, label.Y)), height, lines)
        { Target = Highlight(spec, height), TextFill = fill, Panel = panel, DraftingLines = drafting, LeaderColor = color };
    }

    private static PmiDisplayMesh? Merge(PmiDisplayMesh? a, PmiDisplayMesh? b) => a is null ? b : b is null ? a
        : new(a.Positions.Concat(b.Positions).ToArray(), a.Indices.Concat(b.Indices.Select(i => i + a.Positions.Length / 3)).ToArray());

    private static IReadOnlyList<double[][]> Lines3D(PmiCanvas canvas, PmiPlane plane, double lift) =>
        canvas.Lines.Concat(canvas.GlyphLines).Select(line => line.Select(p => P(plane.At(p.X, p.Y, lift))).ToArray()).ToArray();

    private static IReadOnlyList<double[][]> Draft3D(PmiCanvas canvas, PmiPlane plane) =>
        canvas.Lines.Select(line => line.Select(p => P(plane.At(p.X, p.Y))).ToArray()).ToArray();

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
    /// Baseline dimensions in one horizontal plane: boxed basic dimensions from each planar datum a
    /// Position control references to every controlled hole axis, and authored locations from an
    /// axis face to a feature's centre. Equal values share a tier only when both are basic or both not.
    /// </summary>
    private static Dictionary<int, PmiCanvas> BasicDimensions(IReadOnlyList<Spec> specs, IReadOnlyDictionary<string, Spec> datums,
        IReadOnlyList<(int Owner, Vector3D Normal, double DatumOffset, Point3D Point, bool Boxed)> locations, Layout layout)
    {
        var result = new Dictionary<int, PmiCanvas>();
        var requests = new List<(int Owner, Vector3D Normal, double DatumOffset, Point3D Point, bool Boxed)>(locations);
        foreach (var position in specs.Where(s => s.Item.Kind == "Position" && s.Feature is HoleFeature))
            foreach (var label in position.Item.DatumReferences)
                if (datums.TryGetValue(label, out var datum) && datum.Feature is PlanarFeature { } face && Math.Abs(face.Normal.Z) < 1e-7)
                {
                    var origin = datum.Targets[0].Positions[0];
                    foreach (var cylinder in ((HoleFeature)position.Feature).Cylinders)
                        if (Math.Abs(cylinder.Axis.ToVector().Z) > 1 - 1e-7)
                            requests.Add((position.Item.EntityId, face.Normal, (origin - Point3D.Origin).Dot(face.Normal), cylinder.Origin, true));
                }
        int tiersX = 0, tiersY = 0;
        foreach (var direction in requests.GroupBy(r => (Math.Round(r.Normal.X), Math.Round(r.Normal.Y))))
        {
            var values = direction.Select(r => (r, Value: Math.Abs((r.Point - Point3D.Origin).Dot(r.Normal) - r.DatumOffset)))
                .GroupBy(v => (Math.Round(v.Value, 6), v.r.Boxed, v.r.Owner)).Select(g => g.First()).OrderBy(v => v.Value).ToArray();
            var tiers = values.Select(v => (Math.Round(v.Value, 6), v.r.Boxed)).Distinct().ToList();
            foreach (var (request, value) in values)
            {
                var tier = tiers.IndexOf((Math.Round(value, 6), request.Boxed));
                var canvas = result.TryGetValue(request.Owner, out var existing) ? existing : result[request.Owner] = new PmiCanvas();
                layout.BasicDimension(canvas, request.Normal, request.DatumOffset, request.Point, value, tier, request.Boxed);
            }
            if (Math.Abs(direction.Key.Item1) > .5) tiersX = Math.Max(tiersX, tiers.Count); else tiersY = Math.Max(tiersY, tiers.Count);
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

        // Labels in different planes can still overlap on screen. The default view looks from the front
        // left, above; label rectangles are also kept apart in that view's projection.
        private static readonly Vector3D ViewRight = Direction3D.Create(new Vector3D(0, 0, 1).Cross(new Vector3D(-1, -1.27, 1.1))).ToVector();
        private static readonly Vector3D ViewUp = Direction3D.Create(new Vector3D(-1, -1.27, 1.1).Cross(ViewRight)).ToVector();
        private readonly List<(double X0, double Y0, double X1, double Y1)> screen = [];
        private static (double X0, double Y0, double X1, double Y1) Screen(PmiPlane plane, (double X0, double Y0, double X1, double Y1) r)
        {
            var corners = new[] { plane.At(r.X0, r.Y0), plane.At(r.X1, r.Y0), plane.At(r.X1, r.Y1), plane.At(r.X0, r.Y1) }
                .Select(p => (X: (p - Point3D.Origin).Dot(ViewRight), Y: (p - Point3D.Origin).Dot(ViewUp))).ToArray();
            return (corners.Min(c => c.X), corners.Min(c => c.Y), corners.Max(c => c.X), corners.Max(c => c.Y));
        }

        private bool Free(PmiPlane plane, (double X0, double Y0, double X1, double Y1) r, bool outsideBody, bool label = true)
        {
            if (label)
            {
                var v = Screen(plane, r); var gap = .6 * h;
                if (screen.Any(o => !(v.X1 + gap <= o.X0 || v.X0 - gap >= o.X1 || v.Y1 + gap <= o.Y0 || v.Y0 - gap >= o.Y1))) return false;
            }
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

        private void Occupy(PmiPlane plane, (double X0, double Y0, double X1, double Y1) r, bool label = true)
        {
            if (label) screen.Add(Screen(plane, r));
            if (!occupied.TryGetValue(Key(plane), out var list)) occupied[Key(plane)] = list = [];
            list.Add(r);
        }

        /// <summary>Leader from the hole's visible rim, out and away from the part, with a horizontal shoulder.</summary>
        public Placement PlaceHoleCallout(HoleFeature hole, Point3D center, double width, double height, Point3D[] bodyPoints)
        {
            // The callout points at the hole nearest the default front-left view.
            var cylinder = hole.Cylinders.OrderBy(c => c.Origin.X + c.Origin.Y).First();
            var r = cylinder.Radius;
            var level = bodyPoints.Where(p => Math.Pow(p.X - cylinder.Origin.X, 2) + Math.Pow(p.Y - cylinder.Origin.Y, 2) <= Math.Pow(2.2 * r + h, 2)).Max(p => p.Z);
            // The callout belongs to the controlled wall: when it starts below the entry
            // (inside a counterbore), the leader drops to the wall's rim.
            var wallTop = hole.Walls.SelectMany(w => w).SelectMany(p => p.Positions).Max(p => p.Z);
            return PlaceRadial((cylinder.Origin.X, cylinder.Origin.Y), r, level, Math.Min(level, wallTop), center, width, height);
        }

        /// <summary>A callout on a composed feature: a pocket at its floor, a boss at its top rim, a chamfer on its face.</summary>
        public Placement PlaceFeatureCallout(DisplayFaceMeshPatch[] faces, IReadOnlyList<Step242SemanticPmiInspectionItem> items, Point3D center,
            double width, double height, Point3D[] bodyPoints, CylinderSurface? cylinder)
        {
            var points = faces.SelectMany(f => f.Positions).ToArray();
            var top = points.Max(p => p.Z);
            if (items.Any(i => i.DimensionKind == "depth"))
            {
                // Point at the floor centre of the pocket nearest the default view, from the rim level.
                var floorZ = points.Min(p => p.Z);
                var floor = faces.Where(f => f.Positions.All(p => Math.Abs(p.Z - floorZ) < 1e-6))
                    .OrderBy(f => f.Positions.Average(p => p.X + p.Y)).First().Positions;
                return PlaceRadial(((floor.Min(p => p.X) + floor.Max(p => p.X)) / 2, (floor.Min(p => p.Y) + floor.Max(p => p.Y)) / 2), 0, top, floorZ, center, width, height);
            }
            if (cylinder is { } round)
            {
                // A boss: point at its top rim.
                var rim = faces.Where(f => f.Positions.Any()).SelectMany(f => f.Positions)
                    .Where(p => Math.Abs(Math.Sqrt(Math.Pow(p.X - round.Origin.X, 2) + Math.Pow(p.Y - round.Origin.Y, 2)) - round.Radius) < 1e-6).ToArray();
                var rimTop = rim.Length > 0 ? rim.Max(p => p.Z) : top;
                return PlaceRadial((round.Origin.X, round.Origin.Y), round.Radius, rimTop, rimTop, center, width, height);
            }
            if (items.Any(i => i.DimensionKind == "chamfer"))
            {
                // The chamfer nearest the default view, at its middle.
                var front = faces.OrderBy(f => f.Positions.Average(p => p.Y)).First().Positions;
                var mid = (X: (front.Min(p => p.X) + front.Max(p => p.X)) / 2, Y: (front.Min(p => p.Y) + front.Max(p => p.Y)) / 2);
                return PlaceRadial(mid, 0, front.Max(p => p.Z), (front.Min(p => p.Z) + front.Max(p => p.Z)) / 2, center, width, height);
            }
            var axis = ((points.Min(p => p.X) + points.Max(p => p.X)) / 2, (points.Min(p => p.Y) + points.Max(p => p.Y)) / 2);
            var radius = Math.Min(points.Max(p => p.X) - points.Min(p => p.X), points.Max(p => p.Y) - points.Min(p => p.Y)) / 2;
            return PlaceRadial(axis, radius, top, top, center, width, height);
        }

        private Placement PlaceRadial((double X, double Y) axis, double r, double level, double anchorZ, Point3D center, double width, double height)
        {
            var plane = new PmiPlane(new Point3D(0, 0, level), new Vector3D(1, 0, 0), new Vector3D(0, 1, 0));
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
                    var anchor = new Point3D(tip.X, tip.Y, anchorZ);
                    return new(plane, x, elbow.Y - height / 2, leader, anchor) { Drop = level - anchorZ };
                }
            throw new InvalidOperationException("pmi-display-layout-no-space: a callout has no free position.");
        }

        /// <summary>
        /// An overall size outside the part: length behind it, width to its right (both in the plane of
        /// the walls' top edges), thickness up its front right corner.
        /// </summary>
        /// <summary>A dimension between two parallel planes with normal <paramref name="n"/>: drawn in a plane containing
        /// the normal, beside the part, with extension lines from each face.</summary>
        public (PmiCanvas Canvas, PmiPlane Plane) Between(Vector3D n, IReadOnlyList<Point3D> a, IReadOnlyList<Point3D> b, string text)
        {
            var normal = n * (1 / n.Length);
            // The world axis least aligned with the normal gives the direction the dimension stands off along.
            Vector3D[] axes = [new(1, 0, 0), new(0, 1, 0), new(0, 0, 1)];
            var w = axes.OrderBy(x => Math.Abs(x.Dot(normal))).First();
            var u = w - normal * w.Dot(normal); u = u * (1 / u.Length);
            var plane = new PmiPlane(Point3D.Origin, u, normal);
            Point3D[] corners = [.. from x in new[] { minX, maxX } from y in new[] { minY, maxY } from z in new[] { minZ, maxZ } select new Point3D(x, y, z)];
            var reach = corners.Max(c => plane.Local(c).X);
            var (ta, tb) = (plane.Local(a[0]).Y, plane.Local(b[0]).Y);
            var (ea, eb) = (a.Max(p => plane.Local(p).X), b.Max(p => plane.Local(p).X));
            var at = reach + 3 * h;
            var canvas = new PmiCanvas();
            canvas.Line((ea + .4 * h, ta), (at + .6 * h, ta));
            canvas.Line((eb + .4 * h, tb), (at + .6 * h, tb));
            canvas.Line((at, ta), (at, tb));
            Arrow(canvas, (at, ta), (0, ta - tb)); Arrow(canvas, (at, tb), (0, tb - ta));
            var label = PmiDrafting.TextBlock(text, h);
            canvas.Append(label.Canvas, at + .6 * h, (ta + tb) / 2 - label.Height / 2);
            return (canvas, plane);
        }

        public (PmiCanvas Canvas, PmiPlane Plane) Overall(string kind, Point3D[] points, string text)
        {
            var canvas = new PmiCanvas();
            PmiPlane plane; (double X, double Y) a, b, offset;
            if (kind == "thickness")
            {
                plane = new PmiPlane(new Point3D(0, minY, 0), new Vector3D(1, 0, 0), new Vector3D(0, 0, 1));
                a = (maxX, points.Min(p => p.Z)); b = (maxX, points.Max(p => p.Z)); offset = (1, 0);
            }
            else
            {
                plane = new PmiPlane(new Point3D(0, 0, points.Max(p => p.Z)), new Vector3D(1, 0, 0), new Vector3D(0, 1, 0));
                (a, b, offset) = kind == "length"
                    ? ((points.Min(p => p.X), maxY), (points.Max(p => p.X), maxY), (0d, 1d))
                    : ((maxX, points.Min(p => p.Y)), (maxX, points.Max(p => p.Y)), (1d, 0d));
            }
            var label = PmiDrafting.TextBlock(text, h);
            for (var distance = 3 * h; distance < 60 * h; distance += h)
            {
                var la = (X: a.X + offset.X * distance, Y: a.Y + offset.Y * distance); var lb = (X: b.X + offset.X * distance, Y: b.Y + offset.Y * distance);
                var mid = ((la.X + lb.X) / 2, (la.Y + lb.Y) / 2);
                var rect = (mid.Item1 - label.Width / 2, mid.Item2 - label.Height / 2, mid.Item1 + label.Width / 2, mid.Item2 + label.Height / 2);
                if (!Free(plane, rect, false)) continue;
                Occupy(plane, (Math.Min(la.X, lb.X) - .5 * h, Math.Min(la.Y, lb.Y) - .5 * h, Math.Max(la.X, lb.X) + .5 * h, Math.Max(la.Y, lb.Y) + .5 * h), label: false);
                Occupy(plane, rect);
                canvas.Line((a.X + offset.X * .4 * h, a.Y + offset.Y * .4 * h), (la.X + offset.X * .6 * h, la.Y + offset.Y * .6 * h));
                canvas.Line((b.X + offset.X * .4 * h, b.Y + offset.Y * .4 * h), (lb.X + offset.X * .6 * h, lb.Y + offset.Y * .6 * h));
                canvas.Line(la, lb);
                var along = (X: lb.X - la.X, Y: lb.Y - la.Y);
                Arrow(canvas, la, (-along.X, -along.Y)); Arrow(canvas, lb, along);
                canvas.Append(label.Canvas, rect.Item1, rect.Item2);
                return (canvas, plane);
            }
            throw new InvalidOperationException("pmi-display-layout-no-space: an overall dimension has no free position.");
        }

        /// <summary>Reserve the band a set of baseline dimensions occupies, extension lines included.</summary>
        public void ReserveDimensionBands(int tiersAlongX, int tiersAlongY)
        {
            var plane = new PmiPlane(new Point3D(0, 0, DimensionLevel), new Vector3D(1, 0, 0), new Vector3D(0, 1, 0));
            var spacing = 2.6 * h; var first = 3.2 * h;
            DimensionDepth = first + (Math.Max(tiersAlongX, 1) - 1) * spacing + 1.2 * h;
            if (tiersAlongX > 0) Occupy(plane, (minX - h, minY - first - (tiersAlongX - 1) * spacing - 1.2 * h, maxX + h, minY), label: false);
            if (tiersAlongY > 0) Occupy(plane, (minX - first - (tiersAlongY - 1) * spacing - 1.6 * h, minY - h, minX, maxY + h), label: false);
        }

        public (double X, double Top) PlaceNotes(PmiPlane plane, double width, double height)
        {
            for (var top = minZ - 3 * h; top > minZ - 200 * h; top -= h)
            {
                var rect = (maxX - width, top - height, maxX, top);
                if (!Free(plane, rect, false)) continue;
                Occupy(plane, rect);
                return (maxX - width, top);
            }
            throw new InvalidOperationException("pmi-display-layout-no-space: the notes have no free position.");
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
            // Straight out along the face normal first; on a crowded part, the block slides sideways in steps of its own
            // size (the leader then runs obliquely) and reaches further, before giving up.
            var dirLength = Math.Max(1e-9, Math.Sqrt(dir.X * dir.X + dir.Y * dir.Y));
            var across = (X: -dir.Y / dirLength, Y: dir.X / dirLength);
            var slides = new[] { 0d }.Concat(Enumerable.Range(1, 24).SelectMany(k => new[] { k, -k }).Select(k => k * (Math.Abs(across.X) > .5 ? width : height) * 1.1));
            foreach (var (reach, slide) in slides.SelectMany(sl => Enumerable.Range(0, sl == 0 ? 157 : 60).Select(i => (1.6 * h + i * .5 * h * (sl == 0 ? 1 : 3), sl))))
            {
                var end = (X: anchor.X + dir.X * reach + across.X * slide, Y: anchor.Y + dir.Y * reach + across.Y * slide);
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
        public void BasicDimension(PmiCanvas canvas, Vector3D datumNormal, double datumOffset, Point3D axis, double value, int tier, bool boxed = true)
        {
            // Dimensions along X sit in front of the part; dimensions along Y sit to its left.
            var alongX = Math.Abs(datumNormal.X) > .5;
            var plane = new PmiPlane(new Point3D(0, 0, DimensionLevel), new Vector3D(1, 0, 0), new Vector3D(0, 1, 0));
            var spacing = 2.6 * h; var first = 3.2 * h;
            var datumCoordinate = datumOffset * (alongX ? datumNormal.X : datumNormal.Y);
            var featureCoordinate = alongX ? axis.X : axis.Y;
            var label = PmiDrafting.TextBlock(PmiDrafting.Number(value), h, boxed);
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
                Occupy(plane, (Math.Min(datumCoordinate, featureCoordinate), y - label.Height / 2, Math.Max(datumCoordinate, featureCoordinate), y + label.Height / 2), label: false);
                Occupy(plane, (cx, y - label.Height / 2, cx + label.Width, y + label.Height / 2));
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
                Occupy(plane, (x - label.Width / 2, Math.Min(datumCoordinate, featureCoordinate), x + label.Width / 2, Math.Max(datumCoordinate, featureCoordinate)), label: false);
                Occupy(plane, (x - label.Width / 2, cy, x + label.Width / 2, cy + label.Height));
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
