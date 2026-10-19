using System.Globalization;
using Aetheris.Kernel.Core.Brep;
using Aetheris.Kernel.Core.Brep.Queries;
using Aetheris.Kernel.Core.Math;
using Aetheris.Kernel.Core.Step242;
using Aetheris.Kernel.Core.Topology;

namespace Aetheris.Kernel.Firmament.Drawing;

public static partial class FirmamentDrawingCompiler
{
    private sealed record HoleGroup(string Identity, double Diameter, double? Plus, double? Minus);

    private static DrawingCompileResult GenerateDefinedPart(string path, string source, string name, string sourceKind,
        BrepBody body, IReadOnlyList<Step242SemanticPmiInspectionItem> pmi, IReadOnlyDictionary<int, FaceId> faceMap,
        DrawingSignoffIr? signoff, DrawingGenerationOptions options, string outputDirectory)
    {
        if (!BrepRectangularPlateQuery.TryDefine(body, out var plate, out var reason))
            return Failure($"drawing-definition-incomplete: {reason} Use an authored drawing with complete dimensions, or --reference-only for explicitly incomplete visualization.");
        var groups = new List<HoleGroup>();
        var holes = new List<DrawingDefinedHoleIr>();
        var consumed = new HashSet<int>();
        foreach (var hole in plate!.Holes)
        {
            var sizePmi = pmi.Where(item => (item.Kind == "Diameter" || item.DimensionKind?.ToLowerInvariant() is "diameter" or "holediameter")
                && item.GeometricFaceEntityIds.Any(id => faceMap.GetValueOrDefault(id) == hole.Wall)).ToArray();
            if (sizePmi.Any(item => item.Value is not { } value || Math.Abs(value - hole.Diameter) > 1e-7))
                return Failure($"drawing-pmi-geometry-mismatch: Diameter PMI on face {hole.Wall.Value} disagrees with its exact cylinder. A conflicting dimension was not emitted.");
            if (sizePmi.Any(item => item.Depth is { } depth && Math.Abs(depth - plate.Thickness) > 1e-7))
                return Failure($"drawing-pmi-geometry-mismatch: Hole depth PMI on face {hole.Wall.Value} disagrees with the through-hole thickness.");
            if (sizePmi.Any(item => item.Quantity is { } quantity && quantity != plate.Holes.Count(h => item.GeometricFaceEntityIds.Any(id => faceMap.GetValueOrDefault(id) == h.Wall))))
                return Failure($"drawing-pmi-geometry-mismatch: Hole quantity PMI on face {hole.Wall.Value} disagrees with its associated physical holes.");
            if (sizePmi.Select(i => (i.TolerancePlus, i.ToleranceMinus)).Distinct().Count() > 1)
                return Failure($"drawing-pmi-conflicting-requirements: Face {hole.Wall.Value} has conflicting diameter limits.");
            var plus = sizePmi.FirstOrDefault()?.TolerancePlus; var minus = sizePmi.FirstOrDefault()?.ToleranceMinus;
            if (new[] { plus, minus }.Any(v => v is { } n && (!double.IsFinite(n) || n < 0)))
                return Failure("drawing-pmi-value-unavailable: Diameter tolerance must be finite and nonnegative.");
            if (minus is { } m && hole.Diameter - m <= 0) return Failure("drawing-pmi-value-unavailable: Diameter limits must be positive.");
            if (plus is { } p && minus is { } lower && p + lower <= 0) return Failure("drawing-tolerance-zero-width: A zero-width diameter acceptance interval is not an admitted manufacturing requirement.");
            var group = groups.FirstOrDefault(g => g.Diameter == hole.Diameter && g.Plus == plus && g.Minus == minus);
            if (group is null) { group = new("D" + (groups.Count + 1), hole.Diameter, plus, minus); groups.Add(group); }
            var authoredIdentities = sizePmi.Select(i => i.Target.Split('.').Last()).Distinct(StringComparer.Ordinal).ToArray();
            var authoredIdentity = authoredIdentities.Length == 1 ? authoredIdentities[0] : null;
            var identity = authoredIdentity is { Length: > 0 } && authoredIdentity.All(c => char.IsLetterOrDigit(c) || c == '_')
                && holes.All(h => h.Identity != authoredIdentity) ? authoredIdentity : "H" + hole.Wall.Value;
            if (holes.Any(h => h.Identity == identity)) identity = "Face" + hole.Wall.Value;
            holes.Add(new(identity, hole.X - plate.Origin.X, hole.Y - plate.Origin.Y, hole.Diameter, "THRU", group.Identity,
                sizePmi.Select(i => $"STEP #{i.EntityId}: {i.AuthoredName ?? i.Name}; {i.Target}").ToArray()));
            foreach (var item in sizePmi) consumed.Add(item.EntityId);
        }
        var datums = pmi.Where(i => i.Kind == "Datum").ToArray();
        foreach (var datum in datums) consumed.Add(datum.EntityId);
        var unconsumed = pmi.Where(i => !consumed.Contains(i.EntityId)).ToArray();
        if (unconsumed.Length > 0) return Failure($"drawing-definition-pmi-unsupported: Complete plate planning cannot yet place these requirements without losing their meaning: {string.Join(", ", unconsumed.Select(i => i.AuthoredName ?? i.Name))}. They were not omitted; use an authored drawing or --reference-only.");
        if (groups.Count > 4) return Failure("drawing-layout-impossible: More than four distinct hole specifications require an authored hole-schedule sheet.");

        var missing = new List<string>();
        if (options.LinearToleranceMillimetres is null) missing.Add("Stock-size and X/Y coordinate tolerances are unspecified.");
        if (options.AngularToleranceDegrees is null) missing.Add("General angular tolerance is unspecified.");
        if (groups.Any(g => g.Plus is null || g.Minus is null)) missing.Add("One or more hole diameter tolerances are unspecified.");
        if (string.IsNullOrWhiteSpace(options.Material)) missing.Add("Material is unspecified.");
        var web = MinimumHoleWeb(plate, holes, groups, options);
        if (web is <= 0 || options.LinearToleranceMillimetres is { } stockTolerance && stockTolerance >= Math.Min(plate.Width, Math.Min(plate.Height, plate.Thickness)))
            return Failure("drawing-tolerance-envelope-invalid: Declared limits permit zero stock thickness, intersecting holes or an edge breakout. Revise the requirements; a nominal picture does not establish admissible limits.");
        var evidence = new DrawingDefinitionEvidenceIr(true, options.LinearToleranceMillimetres.HasValue && options.AngularToleranceDegrees.HasValue && groups.All(g => g.Plus.HasValue && g.Minus.HasValue),
            !string.IsNullOrWhiteSpace(options.Material), false, "RectangularPlateWithZThroughHoles", plate.CoveredFaces.Select(f => f.Value).ToArray(), missing,
            new Dictionary<string, double> { ["Width"] = plate.Width, ["Height"] = plate.Height, ["Thickness"] = plate.Thickness }, holes, options.LinearToleranceMillimetres)
        { AngularToleranceDegrees = options.AngularToleranceDegrees, MinimumHoleWebAtDeclaredLimitsMillimetres = web };
        var metadata = new DrawingMetadataIr(name + " - dimensioned plate", name, null, null, options.Material,
            name + "Drawing", "CompletePlateDimensions", Author: signoff?.Drawn.Name, Date: signoff?.Drawn.Date,
            Description: "Exact nominal dimensions; manufacturing release and process capability require engineering review") { Signoff = signoff };
        var notes = new List<string> { "DIMENSIONS IN mm. DO NOT SCALE DRAWING.", "X0: LEFT FINISHED EDGE; Y0: BOTTOM FINISHED EDGE.",
            "TABLE X/Y FROM X0/Y0; (0,0) AT THEIR INTERSECTION.",
            options.LinearToleranceMillimetres.HasValue ? "TABLE X/Y LIMITS APPLY INDEPENDENTLY TO EACH AXIS." : "STOCK / X/Y LIMITS UNSPECIFIED - NOT FOR MANUFACTURE.",
            options.AngularToleranceDegrees is { } a ? $"UNLESS SPECIFIED: ANGLES ±{Number(a)}°." : "ANGLE LIMITS UNSPECIFIED - NOT FOR MANUFACTURE.",
            "X/Y AT TOP FACE; HOLE AXES NOMINALLY NORMAL; THRU.",
            "NOMINAL GEOMETRY COMPLETE; PROCESS CAPABILITY UNVERIFIED." };
        if (string.IsNullOrWhiteSpace(options.Material)) notes.Add("MATERIAL UNSPECIFIED - NOT FOR MANUFACTURE.");
        if (groups.Any(g => g.Plus is null || g.Minus is null)) notes.Add("HOLE SIZE LIMITS UNSPECIFIED - NOT FOR MANUFACTURE.");
        var chunks = holes.Count == 0 ? new[] { Array.Empty<DrawingDefinedHoleIr>() } : holes.Chunk(12).ToArray();
        var pages = new List<DrawingPageIr>();
        var scale = StandardScale(Math.Min(Math.Min(105 / plate.Width, 75 / plate.Height), 12 / plate.Thickness));
        var top = PlaceDefinitionView(body, name, "TOP", [0, 0, 1], new(30, 40, 110, 75), scale, new(12, 18, 273, 135));
        var front = PlaceDefinitionView(body, name, "FRONT", [0, -1, 0], new(30, 136, 110, 12), scale, new(12, 123, 273, 30));
        var annotations = new List<DrawingAnnotationIr>();
        DrawingPoint2 Project(DrawingViewIr view, Point3D point) => new(view.Camera!.OriginX + view.Scale * (point.X * view.Camera.Right[0] + point.Y * view.Camera.Right[1] + point.Z * view.Camera.Right[2]),
            view.Camera.OriginY - view.Scale * (point.X * view.Camera.Up[0] + point.Y * view.Camera.Up[1] + point.Z * view.Camera.Up[2]));
        string Linear(double value) => Number(value) + (options.LinearToleranceMillimetres is { } t ? " ±" + Number(t) : "");
        var b = top.GeometryBounds; var f = front.GeometryBounds;
        annotations.Add(Measure("Width", "TOP", Linear(plate.Width), new(b.X, b.Y), new(b.Right, b.Y), b.Y - 9, true));
        annotations.Add(Measure("Height", "TOP", Linear(plate.Height), new(b.Right, b.Y), new(b.Right, b.Bottom), b.Right + 8, false));
        annotations.Add(Measure("Thickness", "FRONT", Linear(plate.Thickness), new(f.Right, f.Y), new(f.Right, f.Bottom), f.Right + 8, false));
        // Drawing coordinate references, not additional source/GD&T datums. The exact
        // stock planes establish zero; the source's datum PMI remains unchanged.
        var xZero = Project(top, new(plate.Origin.X, plate.Origin.Y + plate.Height * .45, plate.Origin.Z + plate.Thickness));
        var yZero = Project(top, new(plate.Origin.X + plate.Width * .6, plate.Origin.Y, plate.Origin.Z + plate.Thickness));
        var origin = Project(top, new(plate.Origin.X, plate.Origin.Y, plate.Origin.Z + plate.Thickness));
        annotations.Add(DefinedLabel("coordinate:X0", "TOP", "X0", new(b.X - 19, xZero.Y - 3),
            [xZero, new(b.X - 9, xZero.Y)], xZero, $"Drawing X=0 reference: finished stock plane X={plate.Origin.X:R}; not additional AP242 PMI."));
        annotations.Add(DefinedLabel("coordinate:Y0", "TOP", "Y0", new(yZero.X - 3, b.Bottom + 3),
            [yZero, new(yZero.X, b.Bottom + 3)], yZero, $"Drawing Y=0 reference: finished stock plane Y={plate.Origin.Y:R}; not additional AP242 PMI."));
        annotations.Add(DefinedLabel("coordinate:origin", "TOP", "(0,0)", new(b.X - 19, b.Bottom + 3),
            [origin, new(b.X - 4, b.Bottom + 6)], origin, $"Top-face coordinate origin: ({plate.Origin.X:R}, {plate.Origin.Y:R}, {plate.Origin.Z + plate.Thickness:R}) mm."));
        var primitives = top.Primitives.ToList();
        foreach (var hole in holes)
        {
            var point = Project(top, new(plate.Origin.X + hole.X, plate.Origin.Y + hole.Y, plate.Origin.Z + plate.Thickness));
            var r = hole.Diameter * scale / 2;
            primitives.Add(new($"center:{hole.Identity}:x", DrawingPrimitiveKind.CenterMark, [new(point.X - r - 1, point.Y), new(point.X + r + 1, point.Y)], name, 0));
            primitives.Add(new($"center:{hole.Identity}:y", DrawingPrimitiveKind.CenterMark, [new(point.X, point.Y - r - 1), new(point.X, point.Y + r + 1)], name, 0));
            annotations.Add(DefinedLabel(hole.Identity, "TOP", hole.Identity, new(point.X + r + 1.5, point.Y - r - 5), [], point,
                $"Coordinate schedule: {hole.X:R}, {hole.Y:R}; {string.Join("; ", hole.SourceReferences)}"));
        }
        top = top with { Primitives = primitives };
        // The qualified family proves these internal cylinder silhouettes. A cylindrical
        // parameter seam is not an external physical edge in a manufacturing side view.
        var stockIds = plate.StockEdges.Select(e => $"edge:{e.Value}").ToHashSet();
        var frontEdges = front.Primitives.Where(p => p.SourceEdgeIdentity is { } id && stockIds.Contains(id)).ToList();
        var hiddenLines = new HashSet<(double X, double Radius)>();
        foreach (var hole in holes)
        {
            if (!hiddenLines.Add((hole.X, hole.Diameter / 2))) continue;
            foreach (var dx in new[] { -hole.Diameter / 2, hole.Diameter / 2 })
            {
                var x = plate.Origin.X + hole.X + dx;
                frontEdges.Add(new($"internal:{hole.Identity}:{dx:R}", DrawingPrimitiveKind.Hidden,
                    [Project(front, new(x, plate.Origin.Y, plate.Origin.Z)), Project(front, new(x, plate.Origin.Y, plate.Origin.Z + plate.Thickness))], name, 0));
            }
        }
        front = front with { Primitives = frontEdges };
        foreach (var datum in datums)
        {
            var faceIds = datum.GeometricFaceEntityIds.Where(faceMap.ContainsKey).Select(id => faceMap[id]).Distinct().ToArray();
            if (faceIds.Length != 1 || !body.TryGetFaceSurfaceGeometry(faceIds[0], out var support) || support?.Plane is not { } plane)
                return Failure($"drawing-pmi-target-unlocated: Datum '{datum.Name}' does not identify a unique plate plane.");
            var n = plane.Normal.ToVector();
            var view = Math.Abs(n.Z) > .9 ? front : top;
            var location = Math.Abs(n.Z) > .9 ? new Point3D(plate.Origin.X + plate.Width * .75, plate.Origin.Y, plane.Origin.Z)
                : Math.Abs(n.X) > .9 ? new(plane.Origin.X, plate.Origin.Y + plate.Height * .75, plate.Origin.Z + plate.Thickness)
                : new(plate.Origin.X + plate.Width * .75, plane.Origin.Y, plate.Origin.Z + plate.Thickness);
            var anchor = Project(view, location);
            var label = DefinedLabel("datum:" + datum.Name, view.Identity, datum.Name, new(anchor.X - 3, anchor.Y - 12), [anchor, new(anchor.X, anchor.Y - 6)], anchor,
                $"STEP #{datum.EntityId}; face {faceIds[0].Value}; plane {plane.Origin}") with { Kind = DrawingAnnotationKind.Datum };
            annotations.Add(label);
        }
        for (var i = 0; i < groups.Count; i++)
        {
            var group = groups[i]; var members = holes.Where(h => h.Group == group.Identity).ToArray();
            var limits = group.Plus is { } plus && group.Minus is { } minus ? $" +{Number(plus)}/-{Number(minus)}" : " (TOL UNSPECIFIED)";
            var display = $"{group.Identity}: {members.Length}X Ø{Number(group.Diameter)}{limits} THRU";
            annotations.Add(DefinedLabel("group:" + group.Identity, "TOP", display, new(190, 116 + i * 8), [], default,
                string.Join("; ", members.SelectMany(h => h.SourceReferences))) with { Kind = DrawingAnnotationKind.DiameterDimension });
        }
        foreach (var (chunk, index) in chunks.Select((c, i) => (c, i)))
        {
            var number = index + 1;
            var zones = Zones(297, 210);
            var coordinateLimit = options.LinearToleranceMillimetres is { } tolerance ? " ±" + Number(tolerance) : " (TOL?)";
            IReadOnlyList<DrawingTableIr> tables = chunk.Length == 0 ? [] : [new("HOLE COORDINATES (mm)", ["HOLE", "X" + coordinateLimit, "Y" + coordinateLimit, "TYPE"],
                chunk.Select(h => (IReadOnlyList<string>)new[] { h.Identity, Number(h.X), Number(h.Y), h.Group }).ToArray(), name,
                "Exact cylinder axes relative to finished stock planes; no sampled or chained dimensions.", DrawingTableKind.HoleCoordinates,
                new(190, 30, 95, 7 * (chunk.Length + 1)), new(number, "B5"))];
            if (tables.SelectMany(t => t.Rows.Prepend(t.Columns)).SelectMany(r => r).Any(value => DrawingInterFont.Load().MeasureMillimetres(value, 2.8) > 95d / 4 - 2))
                return Failure("drawing-layout-impossible: A hole-schedule value exceeds its measured cell width. Use an authored wider schedule; values were not truncated.");
            var fields = new Dictionary<string, string> { ["Units"] = "mm", ["Scale"] = "×" + Number(scale), ["Sheet"] = $"{number}/{chunks.Length}",
                ["Status"] = missing.Count == 0 ? "DIMENSIONED - REVIEW" : "INCOMPLETE REQUIREMENTS" };
            pages.Add(new(number, DrawingPageOrientation.Landscape, 297, 210, new(12, 18, 273, 135),
                index == 0 ? [top, front] : [], index == 0 ? annotations : [], tables, notes, zones,
                DrawingTitleBlockLayout.Create(metadata, fields, 297, 210, new(number, "D5")),
                LocateNotes(notes, number, zones, DrawingTitleBlockLayout.NotesArea(297, 210))));
        }
        var layout = DrawingAnnotationLayout.Layout(pages, new Dictionary<string, PmiPresentation>());
        if (layout.Diagnostics.Count > 0) return new(false, null, layout.Diagnostics);
        var result = new DrawingIr(name + "Drawing", metadata, new(name, null, "CompletePlateDimensions",
            new Dictionary<string, string> { ["Source"] = path, ["SHA256"] = StableHash(source), ["Requirements"] = $"linear={options.LinearToleranceMillimetres?.ToString("R", CultureInfo.InvariantCulture) ?? "unspecified"}; angular={options.AngularToleranceDegrees?.ToString("R", CultureInfo.InvariantCulture) ?? "unspecified"}; material={options.Material ?? "unspecified"}" },
            StableHash(source)[..16], [], sourceKind), LocateAnnotations(layout.Pages), layout.Evidence, new(0, 0, 0, 0, 0), [],
            "Exact analytic plate definition; shared trimmed-curve wireframe; coordinate dimensions from finished left/bottom edges.") { DefinitionEvidence = evidence };
        return WriteArtifacts(result, Path.GetFullPath(outputDirectory), 1, 0);
    }

    private static DrawingViewIr PlaceDefinitionView(BrepBody body, string name, string identity, double[] direction, DrawingRect slot, double scale, DrawingRect allocation)
    {
        var view = DrawingProjectionEngine.Project([new(name, name, body)], identity, DrawingProjectionKind.Orthographic, DrawingHiddenLinePolicy.Wireframe,
            direction, [], new(slot.X, slot.Y, Math.Max(slot.Width, 25), Math.Max(slot.Height, 25)), new(1, "B2"));
        view = AtScale(view, scale);
        var dx = slot.Center.X - view.GeometryBounds.Center.X; var dy = slot.Center.Y - view.GeometryBounds.Center.Y;
        DrawingPoint2 Move(DrawingPoint2 p) => new(p.X + dx, p.Y + dy);
        return view with { Viewport = allocation, GeometryBounds = view.GeometryBounds with { X = view.GeometryBounds.X + dx, Y = view.GeometryBounds.Y + dy },
            Primitives = view.Primitives.Select(p => p with { Points = p.Points.Select(Move).ToArray() }).ToArray(),
            Camera = view.Camera! with { OriginX = view.Camera.OriginX + dx, OriginY = view.Camera.OriginY + dy } };
    }

    private static DrawingAnnotationIr Measure(string identity, string view, string text, DrawingPoint2 a, DrawingPoint2 b, double offset, bool horizontal)
    {
        var start = horizontal ? new DrawingPoint2(a.X, offset) : new(offset, a.Y);
        var end = horizontal ? new DrawingPoint2(b.X, offset) : new(offset, b.Y);
        var width = DrawingInterFont.Load().MeasureMillimetres(text, 3.2) + 3;
        var body = horizontal ? new DrawingRect((a.X + b.X - width) / 2, offset - 3, width, 6)
            : new DrawingRect(offset + 2, (a.Y + b.Y) / 2 - 3, width, 6);
        return DefinedLabel("dimension:" + identity, view, text, new(body.X, body.Y), [], a, "Exact distance between stock planes; no display-envelope measurement.") with
        { Kind = DrawingAnnotationKind.LinearDimension, DimensionLines = [new(start, end, true, true), new(a, start), new(b, end)] };
    }

    private static DrawingAnnotationIr DefinedLabel(string identity, string view, string text, DrawingPoint2 at, IReadOnlyList<DrawingPoint2> leader,
        DrawingPoint2 anchor, string provenance)
    {
        var width = DrawingInterFont.Load().MeasureMillimetres(text, 3.2) + 3;
        var candidate = new DrawingAnnotationCandidateIr("definition:" + identity, new(at.X, at.Y, width, 6), leader, "definition", 0, 0, []);
        return new(identity, identity, view, DrawingAnnotationKind.Note, text, anchor, candidate, [candidate], provenance);
    }

    private static string Number(double number) => number.ToString("R", CultureInfo.InvariantCulture);

    private static double? MinimumHoleWeb(RectangularPlateDefinition plate, IReadOnlyList<DrawingDefinedHoleIr> holes, IReadOnlyList<HoleGroup> groups, DrawingGenerationOptions options)
    {
        if (holes.Count == 0 || options.LinearToleranceMillimetres is not { } t || options.AngularToleranceDegrees is not { } angle
            || groups.Any(g => g.Plus is null)) return null;
        // Independent Cartesian entry-center limits plus maximum cylinder-axis lean.
        // The screen assumes orthogonal planar stock; it does not qualify stock form,
        // surface finish, general angular stacks, inspection uncertainty or a process.
        var lean = (plate.Thickness + t) * Math.Tan(angle * Math.PI / 180);
        double Radius(DrawingDefinedHoleIr h) => (h.Diameter + groups.Single(g => g.Identity == h.Group).Plus!.Value) / (2 * Math.Cos(angle * Math.PI / 180));
        var minimum = double.PositiveInfinity;
        for (var i = 0; i < holes.Count; i++)
        {
            var h = holes[i]; var r = Radius(h);
            minimum = Math.Min(minimum, new[] { h.X - t - lean - r, h.Y - t - lean - r,
                plate.Width - t - h.X - t - lean - r, plate.Height - t - h.Y - t - lean - r }.Min());
            foreach (var other in holes.Skip(i + 1))
            {
                var dx = Math.Max(0, Math.Abs(h.X - other.X) - 2 * t - 2 * lean);
                var dy = Math.Max(0, Math.Abs(h.Y - other.Y) - 2 * t - 2 * lean);
                minimum = Math.Min(minimum, Math.Sqrt(dx * dx + dy * dy) - r - Radius(other));
            }
        }
        return minimum;
    }
}
