using System.Globalization;
using Aetheris.Kernel.Core.Brep;
using Aetheris.Kernel.Core.Geometry;
using Aetheris.Kernel.Core.Geometry.Surfaces;
using Aetheris.Kernel.Core.Judgment;
using Aetheris.Kernel.Core.Math;
using Aetheris.Kernel.Core.Step242;
using Aetheris.Kernel.Core.Visualization;

namespace Aetheris.Kernel.Firmament.Drawing;

public static partial class FirmamentDrawingCompiler
{
    /// <summary>Generate sheets from an admitted part or the supported AP242 semantic PMI subset.
    /// No manufacturing tolerances, datum schemes or missing dimensions are inferred.</summary>
    public static DrawingCompileResult Generate(string sourcePath, string outputDirectory, DrawingSignoffIr? signoff = null)
        => Generate(sourcePath, outputDirectory, signoff, new());

    public static DrawingCompileResult Generate(string sourcePath, string outputDirectory, DrawingSignoffIr? signoff, DrawingGenerationOptions options)
    {
        if (DrawingTitleBlockLayout.ValidateSignoff(signoff) is { } error) return Failure(error);
        if (options.LinearToleranceMillimetres is { } tolerance && (!double.IsFinite(tolerance) || tolerance <= 0))
            return Failure("drawing-linear-tolerance-invalid: Supply a finite positive tolerance in millimetres.");
        if (options.AngularToleranceDegrees is { } angle && (!double.IsFinite(angle) || angle <= 0 || angle >= 45))
            return Failure("drawing-angular-tolerance-invalid: Supply a finite positive angle below 45 degrees.");
        if (options.ReferenceOnly && (options.LinearToleranceMillimetres.HasValue || options.AngularToleranceDegrees.HasValue || options.Material is not null))
            return Failure("drawing-reference-options-unsupported: Reference-only visualization cannot consume manufacturing requirements. Omit --reference-only to plan dimensions.");
        try { return GenerateCore(sourcePath, outputDirectory, signoff, options); }
        catch (InvalidOperationException e) when (e.Message.StartsWith("drawing-", StringComparison.Ordinal)) { return Failure(e.Message); }
    }

    private static DrawingCompileResult GenerateCore(string sourcePath, string outputDirectory, DrawingSignoffIr? signoff, DrawingGenerationOptions options)
    {
        var path = Path.GetFullPath(sourcePath);
        if (!File.Exists(path)) return Failure($"drawing-source-file-missing: {path}");
        var source = File.ReadAllText(path);
        var name = Path.GetFileNameWithoutExtension(path);
        var step = source;
        var sourceKind = "AP242";
        IReadOnlyList<string> expectedPmi = [];
        if (Path.GetExtension(path).Equals(".firmament", StringComparison.OrdinalIgnoreCase))
        {
            if (IsDrawingSource(source))
            {
                if (options != new DrawingGenerationOptions()) return Failure("drawing-authored-options-unsupported: Generation options apply to automatic part/STEP drawings; keep authored drawing requirements in its metadata.");
                return Compile(path, outputDirectory, signoff);
            }
            var compiled = FirmamentBuildAndExport.CompileSource(source, Path.GetDirectoryName(path));
            if (!compiled.IsSuccess) return new(false, null, compiled.Diagnostics.Select(d => $"{d.Source}: {d.Message}").ToArray());
            var bound = compiled.Value.RuntimeAdmissionDocument?.BoundPmi;
            expectedPmi = (bound?.Datums ?? []).Concat(bound?.Dimensions ?? []).Concat(bound?.Controls ?? []).Select(p => p.Name).ToArray();
            step = compiled.Value.StepText;
            name = compiled.Value.DocumentName ?? name;
            sourceKind = "FirmamentPart";
        }
        else if (Path.GetExtension(path).ToLowerInvariant() is not (".step" or ".stp"))
            return Failure("drawing-input-unsupported: Generate expects a Firmament part or STEP BRep; use drawing compile for authored Assembly drawings.");

        var imported = Step242Importer.ImportBody(step);
        if (!imported.IsSuccess) return new(false, null, imported.Diagnostics.Select(d => $"{d.Source}: {d.Message}").ToArray());
        var inspected = Step242SemanticPmiInspector.Inspect(step);
        if (!inspected.Success) return new(false, null, inspected.Diagnostics);
        // The inspector's bounded emitted subset uses file-unit measure values. Do not
        // silently label foreign unit measures as mm before per-measure unit binding exists.
        var missingPmi = expectedPmi.Where(name => !inspected.Items.Any(item => (item.AuthoredName ?? item.Name) == name)).ToArray();
        if (missingPmi.Length > 0) return Failure($"drawing-pmi-not-exported: Authored PMI has no supported AP242 projection: {string.Join(", ", missingPmi)}. Use an authored Drawing or qualify the missing PMI export family before automatic generation.");
        var parsed = Step242SubsetParser.Parse(step);
        var unsupportedPmi = parsed.Value.Entities.Where(entity =>
            (Step242SubsetDecoder.TryGetConstructor(entity.Instance, "DIMENSIONAL_SIZE") is not null
             || Step242SubsetDecoder.TryGetConstructor(entity.Instance, "DIMENSIONAL_LOCATION") is not null
             || Step242SubsetDecoder.TryGetConstructor(entity.Instance, "GEOMETRIC_TOLERANCE") is not null
             || Step242SubsetDecoder.TryGetConstructor(entity.Instance, "DATUM_FEATURE") is not null)
            && !inspected.Items.Any(item => item.EntityId == entity.Id)).Select(entity => $"#{entity.Id}:{entity.Name}").ToArray();
        if (unsupportedPmi.Length > 0) return Failure($"drawing-pmi-family-unsupported: These AP242 semantic entities require a qualified readback adapter: {string.Join(", ", unsupportedPmi)}. They were not omitted from a successful drawing.");
        if (inspected.Items.Count > 0 && Math.Abs(parsed.Value.SourceMillimetresPerUnit - 1) > 1e-12)
            return Failure("drawing-pmi-units-unsupported: Semantic PMI generation currently requires millimetre AP242 measures. Geometry-only reference views can use normalized imported units.");

        var body = imported.Value;
        var faceMap = body.FaceOrientationReport?.Faces.Where(f => f.SourceEvidence.SourceEntityId.HasValue)
            .ToDictionary(f => f.SourceEvidence.SourceEntityId!.Value, f => f.FaceId) ?? [];
        if (ValidateDrawingPmi(inspected.Items, faceMap) is { } pmiError) return Failure(pmiError);
        if (!options.ReferenceOnly)
            return GenerateDefinedPart(path, source, name, sourceKind, body, inspected.Items, faceMap, signoff, options, outputDirectory);
        BrepWireframeGeometry samples;
        try { samples = BrepWireframeSvgRenderer.SampleGeometry(body, new(CurveSamples: 64, Density: 5)); }
        catch (InvalidOperationException e) { return Failure("drawing-projection-unsupported: " + e.Message); }
        if (samples.Evidence.UnsupportedCurveFamilies.Count > 0)
            return Failure($"drawing-edge-projection-unsupported: {string.Join(", ", samples.Evidence.UnsupportedCurveFamilies)}");
        var definitions = new[]
        {
            (Name: "TOP", Direction: new double[] { 0, 0, 1 }),
            (Name: "FRONT", Direction: new double[] { 0, -1, 0 }),
            (Name: "RIGHT", Direction: new double[] { 1, 0, 0 })
        };
        var pmi = new Dictionary<string, PmiPresentation>(StringComparer.Ordinal);
        var targets = new Dictionary<string, IReadOnlyList<Point3D>>(StringComparer.Ordinal);
        var cylinders = new Dictionary<string, CylinderSurface>(StringComparer.Ordinal);
        var assigned = definitions.ToDictionary(v => v.Name, _ => new List<string>());
        foreach (var inspectedItem in inspected.Items)
        {
            var item = inspectedItem with { Name = inspectedItem.AuthoredName ?? inspectedItem.Name };
            var dimensionKind = item.DimensionKind?.ToLowerInvariant();
            var reference = $"step:{item.EntityId}:{item.Name}";
            var faces = item.GeometricFaceEntityIds.Where(faceMap.ContainsKey).Select(id => body.Topology.GetFace(faceMap[id])).ToArray();
            var edges = faces.SelectMany(f => f.LoopIds).Select(body.Topology.GetLoop).SelectMany(l => l.CoedgeIds)
                .Select(body.Topology.GetCoedge).Select(c => c.EdgeId.Value).ToHashSet();
            var points = samples.Boundaries.Where(line => line.EdgeId is { } id && edges.Contains(id)).SelectMany(line => line.Points).ToArray();
            if (points.Length == 0) return Failure($"drawing-pmi-target-unprojectable: '{item.Name}' has no supported bound edge samples.");
            var kind = dimensionKind is "holediameter" or "diameter" ? DrawingAnnotationKind.DiameterDimension : item.Kind switch
            {
                "Diameter" => DrawingAnnotationKind.DiameterDimension,
                "Datum" => DrawingAnnotationKind.Datum,
                "Position" => DrawingAnnotationKind.FeatureControlFrame,
                "Annotation" => DrawingAnnotationKind.Note,
                _ => DrawingAnnotationKind.LinearDimension
            };
            var display = FormatStepPmi(item);
            pmi.Add(reference, new(reference, kind, display, item.Value, item.Target, $"{path}; STEP #{item.EntityId}; faces {string.Join(",", item.GeometricFaceEntityIds)}; source SHA256 {StableHash(source)}"));
            targets.Add(reference, points);
            var first = faces[0];
            body.TryGetFaceSurfaceGeometry(first.Id, out var surface);
            var axis = surface?.Cylinder?.Axis.ToVector() ?? surface?.Plane?.Normal.ToVector();
            if (kind == DrawingAnnotationKind.DiameterDimension && surface?.Cylinder is { } cylinder
                && item.Value is { } nominal && Math.Abs(nominal - 2 * cylinder.Radius) < 1e-6)
                cylinders[reference] = cylinder;
            var candidates = definitions.Select((view, i) => new JudgmentCandidate<int>(view.Name, _ => true,
                _ => axis is { } a ? Math.Abs(a.X * view.Direction[0] + a.Y * view.Direction[1] + a.Z * view.Direction[2]) : 0,
                TieBreakerPriority: i)).ToArray();
            var selected = new JudgmentEngine<int>().Evaluate(0, candidates).Selection!.Value.Candidate.Name;
            assigned[selected].Add(reference);
        }

        var width = 297d; var height = 210d;
        var metadata = new DrawingMetadataIr(name + " - engineering views", name, null, null, null,
            name + "Drawing", "AutomaticPmiSheets", Author: signoff?.Drawn.Name, Date: signoff?.Drawn.Date,
            Description: "Semantic PMI projection; manufacturing release requires engineering review") { Signoff = signoff };
        var envelope = samples.Boundaries.SelectMany(line => line.Points).ToArray();
        var notes = new[] { "DIMENSIONS: mm. PMI VALUES AND TOLERANCES RETAIN SOURCE AUTHORITY.",
            "INCOMPLETE GEOMETRY DEFINITION - NOT FOR MANUFACTURE.",
            FormattableString.Invariant($"REFERENCE ENVELOPE (mm): X ~{envelope.Max(p => p.X) - envelope.Min(p => p.X):0.###} / Y ~{envelope.Max(p => p.Y) - envelope.Min(p => p.Y):0.###} / Z ~{envelope.Max(p => p.Z) - envelope.Min(p => p.Z):0.###}. SAMPLED; NO TOLERANCE."),
            inspected.Items.Count == 0 ? "NO SUPPORTED SEMANTIC PMI FOUND. GEOMETRY VIEWS ONLY." : "LEADERS IDENTIFY ASSOCIATED FACES; LINEAR CALLOUTS DO NOT INFER MEASUREMENT ENDPOINTS." };
        var requested = new List<(string Name, double[] Direction, DrawingProjectionKind Projection, string[] Pmi)>();
        foreach (var view in definitions)
        {
            var chunks = assigned[view.Name].Chunk(6).ToArray();
            foreach (var chunk in chunks) requested.Add((view.Name, view.Direction, DrawingProjectionKind.Orthographic, chunk));
        }
        var pages = new List<DrawingPageIr>();
        var overviewSpecs = new[] { (Name: "TOP", Direction: new double[] { 0, 0, 1 }, Projection: DrawingProjectionKind.Orthographic),
            (Name: "ISOMETRIC WIREFRAME", Direction: new double[] { 1, 1, 1 }, Projection: DrawingProjectionKind.Isometric),
            (Name: "FRONT", Direction: new double[] { 0, -1, 0 }, Projection: DrawingProjectionKind.Orthographic),
            (Name: "RIGHT", Direction: new double[] { 1, 0, 0 }, Projection: DrawingProjectionKind.Orthographic) };
        var overview = overviewSpecs.Select((spec, i) =>
        {
            var allocation = new DrawingRect(12 + i % 2 * 138, 20 + i / 2 * 66, 135, 64);
            return DrawingProjectionEngine.Project([new(name, name, body)], spec.Name, spec.Projection, DrawingHiddenLinePolicy.Wireframe,
                spec.Direction, [], new(allocation.X + 5, allocation.Y, allocation.Width - 10, allocation.Height), new(1, "B2")) with { Viewport = allocation };
        }).ToArray();
        var commonScale = StandardScale(overview.Where(v => v.Projection == DrawingProjectionKind.Orthographic).Min(v => v.Scale));
        overview = overview.Select(view => AtScale(view, view.Projection == DrawingProjectionKind.Orthographic ? commonScale : StandardScale(view.Scale))).ToArray();
        var overviewScheme = Zones(width, height);
        pages.Add(new(1, DrawingPageOrientation.Landscape, width, height, new(12, 20, 273, 145), overview, [], [], notes, overviewScheme,
            DrawingTitleBlockLayout.Create(metadata, new Dictionary<string, string> { ["Product"] = name, ["Units"] = "mm", ["Scale"] = "SEE VIEWS",
                ["View"] = "OVERVIEW", ["Sheet"] = $"1/{requested.Count + 1}", ["Status"] = "REFERENCE ONLY" }, width, height, new(1, "D5")),
            LocateNotes(notes, 1, overviewScheme, DrawingTitleBlockLayout.NotesArea(width, height))));
        foreach (var request in requested)
        {
            var page = pages.Count + 1;
            var scheme = Zones(width, height);
            var viewport = new DrawingRect(12, 20, 273, 134);
            // Reserve a real callout column rather than squeezing every label around a full-width model.
            var geometryViewport = new DrawingRect(18, 26, 175, 117);
            var view = DrawingProjectionEngine.Project([new(name, name, body)], request.Name, request.Projection,
                DrawingHiddenLinePolicy.Wireframe, request.Direction, request.Pmi, geometryViewport, new(page, "B2"));
            view = AtScale(view, StandardScale(view.Scale));
            DrawingPoint2 Project(Point3D p)
            {
                var c = view.Camera!;
                return new(c.OriginX + view.Scale * (p.X * c.Right[0] + p.Y * c.Right[1] + p.Z * c.Right[2]),
                    c.OriginY - view.Scale * (p.X * c.Up[0] + p.Y * c.Up[1] + p.Z * c.Up[2]));
            }
            var anchors = request.Pmi.ToDictionary(reference => reference, reference => targets[reference].Select(Project)
                .OrderByDescending(p => p.X).ThenBy(p => p.Y).First(), StringComparer.Ordinal);
            view = view with { Viewport = viewport, SemanticAnchors = anchors };
            var fields = new Dictionary<string, string> { ["Product"] = name, ["Units"] = "mm", ["Scale"] = view.Scale.ToString("0.###", CultureInfo.InvariantCulture),
                ["View"] = request.Name, ["Sheet"] = $"{page}/{requested.Count + 1}", ["Status"] = "REFERENCE ONLY" };
            var info = DrawingTitleBlockLayout.Create(metadata, fields, width, height, new(page, "D5"));
            pages.Add(new(page, DrawingPageOrientation.Landscape, width, height, new(12, 20, 273, 145), [view], ReferenceEnvelope(view), [], notes,
                scheme, info, LocateNotes(notes, page, scheme, DrawingTitleBlockLayout.NotesArea(width, height))));
        }
        var layout = DrawingAnnotationLayout.Layout(pages, pmi);
        if (layout.Diagnostics.Count > 0) return new(false, null, layout.Diagnostics);
        var dimensioned = layout.Pages.Select(page => page with
        {
            Annotations = page.Annotations.Select(annotation =>
            {
                if (!cylinders.TryGetValue(annotation.SemanticReference, out var cylinder)) return annotation;
                var view = page.Views.Single(v => v.Identity == annotation.AssignedView);
                var axis = cylinder.Axis.ToVector(); var direction = view.Direction3;
                if (Math.Abs(axis.X * direction[0] + axis.Y * direction[1] + axis.Z * direction[2]) < 1 - 1e-8) return annotation;
                var points = targets[annotation.SemanticReference];
                var v = points.Max(p => (p - cylinder.Origin).Dot(axis) * (axis.X * direction[0] + axis.Y * direction[1] + axis.Z * direction[2]));
                v *= axis.X * direction[0] + axis.Y * direction[1] + axis.Z * direction[2];
                var center = cylinder.Origin + axis * v;
                var camera = view.Camera!;
                var projected = new DrawingPoint2(camera.OriginX + view.Scale * (center.X * camera.Right[0] + center.Y * camera.Right[1] + center.Z * camera.Right[2]),
                    camera.OriginY - view.Scale * (center.X * camera.Up[0] + center.Y * camera.Up[1] + center.Z * camera.Up[2]));
                var r = cylinder.Radius * view.Scale;
                // Small holes use the rim leader alone; three arrowheads inside a tiny circle are unreadable.
                if (2 * r < 8) return annotation;
                return annotation with { DimensionLines = [new(new(projected.X - r, projected.Y), new(projected.X + r, projected.Y), true, true)] };
            }).ToArray()
        }).ToArray();
        var drawing = new DrawingIr(name + "Drawing", metadata,
            new(name, null, "AutomaticPmiSheets", new Dictionary<string, string> { ["Source"] = path, ["SHA256"] = StableHash(source) }, StableHash(source)[..16], [], sourceKind),
            LocateAnnotations(dimensioned), layout.Evidence, new(0, 0, 0, 0, 0), [],
            "Shared BRepWireframe sampler: trimmed exact edge curves and optional surface isolines. Wireframe intentionally displays rear edges; no HLR completeness claim.",
            new("Inter", "Embedded TrueType / Type0 Identity-H", "Embedded Inter advance widths", new Dictionary<string, double> { ["body"] = 2.8, ["label"] = 3.2, ["title"] = 4.0 }))
        { DefinitionEvidence = new(false, false, false, false, "ReferenceOnly", [], ["Complete nominal dimensions and manufacturing requirements were not planned."], new Dictionary<string, double>(), []) };
        return WriteArtifacts(drawing, Path.GetFullPath(outputDirectory), 1, 0);
    }

    private static double StandardScale(double fit)
    {
        var decade = Math.Pow(10, Math.Floor(Math.Log10(fit)));
        return new[] { 1d, 2d, 5d }.Where(m => m * decade <= fit * (1 + 1e-12)).Max() * decade;
    }

    private static string? ValidateDrawingPmi(IReadOnlyList<Step242SemanticPmiInspectionItem> items,
        IReadOnlyDictionary<int, Aetheris.Kernel.Core.Topology.FaceId> faceMap)
    {
        foreach (var item in items)
        {
            var name = item.AuthoredName ?? item.Name;
            var kind = item.DimensionKind?.ToLowerInvariant();
            if (item.Kind == "Dimension" && kind is not ("distance" or "lineardistancetodatum" or "diameter" or "holediameter"))
                return $"drawing-pmi-family-unsupported: Dimension '{name}' has unqualified kind '{item.DimensionKind ?? "unknown"}'. A millimetre label was not guessed.";
            if (!item.GeometricFaceEntityIds.Any(faceMap.ContainsKey))
                return $"drawing-pmi-target-unlocated: '{name}' (STEP #{item.EntityId}, target '{item.Target}') has no imported geometric face association. A model-center leader would be misleading.";
            if (item.Kind is "Dimension" or "Diameter" or "Position" && (item.Value is not { } value || !double.IsFinite(value)
                || (item.Kind == "Diameter" || kind is "diameter" or "holediameter") && value <= 0))
                return $"drawing-pmi-value-unavailable: '{name}' (STEP #{item.EntityId}) has no admitted finite nominal value. The drawing did not substitute zero.";
            if (item.Depth is { } depth && (!double.IsFinite(depth) || depth <= 0)) return $"drawing-pmi-value-unavailable: '{name}' has an invalid hole depth.";
        }
        return null;
    }

    private static DrawingViewIr AtScale(DrawingViewIr view, double scale)
    {
        var ratio = scale / view.Scale; var center = view.GeometryBounds.Center;
        DrawingPoint2 Map(DrawingPoint2 p) => new(center.X + (p.X - center.X) * ratio, center.Y + (p.Y - center.Y) * ratio);
        var topLeft = Map(new(view.GeometryBounds.X, view.GeometryBounds.Y)); var camera = view.Camera!;
        var origin = Map(new(camera.OriginX, camera.OriginY));
        return view with { Scale = scale,
            Primitives = view.Primitives.Select(p => p with { Points = p.Points.Select(Map).ToArray() }).ToArray(),
            GeometryBounds = new(topLeft.X, topLeft.Y, view.GeometryBounds.Width * ratio, view.GeometryBounds.Height * ratio),
            Camera = camera with { OriginX = origin.X, OriginY = origin.Y } };
    }

    private static IReadOnlyList<DrawingAnnotationIr> ReferenceEnvelope(DrawingViewIr view)
    {
        var result = new List<DrawingAnnotationIr>(); var b = view.GeometryBounds;
        void Add(string axis, bool horizontal)
        {
            var text = FormattableString.Invariant($"~{(horizontal ? b.Width : b.Height) / view.Scale:0.###} mm REF ({axis})");
            var width = DrawingInterFont.Load().MeasureMillimetres(text, 3.2) + 3;
            var body = horizontal ? new DrawingRect(b.Center.X - width / 2, b.Bottom + 5, width, 6)
                : new DrawingRect(b.Right - width, b.Bottom + 15, width, 6);
            var start = horizontal ? new DrawingPoint2(b.X, b.Bottom + 8) : new DrawingPoint2(b.Right + 6, b.Y);
            var end = horizontal ? new DrawingPoint2(b.Right, b.Bottom + 8) : new DrawingPoint2(b.Right + 6, b.Bottom);
            var lines = horizontal
                ? new DrawingDimensionLineIr[] { new(start, end, true, true), new(new(b.X, b.Bottom + 1), new(b.X, b.Bottom + 10)), new(new(b.Right, b.Bottom + 1), new(b.Right, b.Bottom + 10)) }
                : new DrawingDimensionLineIr[] { new(start, end, true, true), new(new(b.Right + 1, b.Y), new(b.Right + 8, b.Y)), new(new(b.Right + 1, b.Bottom), new(b.Right + 8, b.Bottom)) };
            var leader = horizontal ? Array.Empty<DrawingPoint2>() : new[] { end, new DrawingPoint2(b.Right + 6, body.Center.Y), new DrawingPoint2(body.Right, body.Center.Y) };
            var candidate = new DrawingAnnotationCandidateIr("reference-envelope", body, leader, "reference", 0, 0, []);
            result.Add(new($"reference:{view.Identity}:{axis}", $"reference:{axis}", view.Identity, DrawingAnnotationKind.LinearDimension, text, start, candidate, [candidate],
                "Sampled projected-curve envelope; approximate reference only; no tolerance or manufacturing authority") { DimensionLines = lines });
        }
        if (view.Identity == "TOP") { Add("X", true); Add("Y", false); }
        if (view.Identity == "FRONT") Add("Z", false);
        return result;
    }

    private static string FormatStepPmi(Step242SemanticPmiInspectionItem item)
    {
        var quantity = item.Quantity is > 1 ? $"{item.Quantity}x " : "";
        if (item.Kind == "Datum") return $"DATUM {item.Name}";
        if (item.Kind == "Annotation") return $"{item.Name}: {item.Text}";
        if (item.Kind == "Position") return FormattableString.Invariant($"POSITION | {item.Value:0.###} | {string.Join(" | ", item.DatumReferences)}");
        var dimension = FormattableString.Invariant($"{item.Value:0.###} mm");
        if (item.TolerancePlus is not null || item.ToleranceMinus is not null)
        {
            var p = Math.Abs(item.TolerancePlus ?? 0); var m = Math.Abs(item.ToleranceMinus ?? 0);
            dimension += Math.Abs(p - m) < 1e-12 ? FormattableString.Invariant($" ±{p:0.###}") : FormattableString.Invariant($" +{p:0.###}/-{m:0.###}");
        }
        if (item.Depth is { } depth) dimension += FormattableString.Invariant($" DEPTH {depth:0.###} mm");
        return quantity + item.Name + ": " + (item.Kind == "Diameter" || item.DimensionKind?.ToLowerInvariant() is "holediameter" or "diameter" ? "Ø" : "") + dimension;
    }
}
