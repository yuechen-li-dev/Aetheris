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

    /// <summary>Project an already compiled product, retaining its authored PMI admission checks.
    /// The browser has no source file to reopen; STEP bytes remain the geometry authority.</summary>
    public static DrawingCompileResult GenerateFromStep(string step, string name, string sourceDocument,
        IReadOnlyList<string> expectedPmi, string outputDirectory)
    {
        try { return GeneratePart(sourceDocument, step, name, "AP242", step, expectedPmi, outputDirectory, null, new()); }
        catch (InvalidOperationException e) when (e.Message.StartsWith("drawing-", StringComparison.Ordinal)) { return Failure(e.Message); }
    }

    public static IReadOnlyList<string> AuthoredPmiNames(FirmamentStepExportResult product)
    {
        var bound = product.RuntimeAdmissionDocument?.BoundPmi;
        return (bound?.Datums ?? []).Concat(bound?.Dimensions ?? []).Concat(bound?.Controls ?? []).Select(p => p.Name).ToArray();
    }

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
            expectedPmi = AuthoredPmiNames(compiled.Value);
            step = compiled.Value.StepText;
            name = compiled.Value.DocumentName ?? name;
            sourceKind = "FirmamentPart";
        }
        else if (Path.GetExtension(path).ToLowerInvariant() is not (".step" or ".stp"))
            return Failure("drawing-input-unsupported: Generate expects a Firmament part or STEP BRep; use drawing compile for authored Assembly drawings.");

        return GeneratePart(path, source, name, sourceKind, step, expectedPmi, outputDirectory, signoff, options);
    }

    private static DrawingCompileResult GeneratePart(string path, string source, string name, string sourceKind, string step,
        IReadOnlyList<string> expectedPmi, string outputDirectory, DrawingSignoffIr? signoff, DrawingGenerationOptions options)
    {
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
            && !inspected.Items.Any(item => item.EntityId == entity.Id) && !inspected.StandardMirrors.Contains(entity.Id)).Select(entity => $"#{entity.Id}:{entity.Name}").ToArray();
        if (unsupportedPmi.Length > 0) return Failure($"drawing-pmi-family-unsupported: These AP242 semantic entities require a qualified readback adapter: {string.Join(", ", unsupportedPmi)}. They were not omitted from a successful drawing.");
        // Records read through the AP242 schema (foreign producers) carry every measure converted to millimetres and
        // radians from its own unit; Aetheris' native subset is read in file units and needs a millimetre file.
        var schemaRead = inspected.Items.All(i => i.Target.StartsWith("shape-aspect:", StringComparison.Ordinal));
        if (inspected.Items.Count > 0 && Math.Abs(parsed.Value.SourceMillimetresPerUnit - 1) > 1e-12 && !(options.ReferenceOnly && schemaRead))
            return Failure("drawing-pmi-units-unsupported: Semantic PMI generation currently requires millimetre AP242 measures. Geometry-only reference views can use normalized imported units.");

        var body = imported.Value;
        var faceMap = body.FaceOrientationReport?.Faces.Where(f => f.SourceEvidence.SourceEntityId.HasValue)
            .ToDictionary(f => f.SourceEvidence.SourceEntityId!.Value, f => f.FaceId) ?? [];
        if ((options.ReferenceOnly ? ValidateReferencePmi(inspected.Items, faceMap) : ValidateDrawingPmi(inspected.Items, faceMap)) is { } pmiError) return Failure(pmiError);
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
        var interiors = new Dictionary<string, (Vector3D Normal, IReadOnlyList<Point3D> Points)[]>(StringComparer.Ordinal);
        var surfaceAnchors = new HashSet<(int Page, string Reference)>();
        var patches = new Dictionary<int, Aetheris.Kernel.Core.Brep.Tessellation.DisplayFaceMeshPatch>();
        if (options.ReferenceOnly && Aetheris.Kernel.Core.Brep.Tessellation.BrepDisplayTessellator.Tessellate(body,
                Aetheris.Kernel.Core.Brep.Tessellation.DisplayTessellationOptions.ForViewport(body)) is { IsSuccess: true } tessellated)
            foreach (var patch in tessellated.Value.FacePatches) patches[patch.FaceId.Value] = patch;
        static Point3D[] Centres(Aetheris.Kernel.Core.Brep.Tessellation.DisplayFaceMeshPatch patch) => Enumerable.Range(0, patch.TriangleIndices.Count / 3)
            .Select(t => { var a = patch.Positions[patch.TriangleIndices[3 * t]]; var b = patch.Positions[patch.TriangleIndices[3 * t + 1]]; var c = patch.Positions[patch.TriangleIndices[3 * t + 2]];
                return new Point3D((a.X + b.X + c.X) / 3, (a.Y + b.Y + c.Y) / 3, (a.Z + b.Z + c.Z) / 3); }).ToArray();
        var cylinders = new Dictionary<string, CylinderSurface>(StringComparer.Ordinal);
        var assigned = definitions.ToDictionary(v => v.Name, _ => new List<string>());
        // Views carry a balloon per requirement; the requirement itself is one row of the PMI schedule, read in one
        // place instead of along crossing leaders. Datums keep their letter.
        var schedule = new List<IReadOnlyList<string>>();
        var balloon = 0;
        // Authored balloon placement (Label records in the source's Pmi block), keyed by each PMI reference.
        var labelDiagnostics = new List<string>();
        var directives = sourceKind == "AP242" ? [] : DrawingLabelDirectives.Read(source, labelDiagnostics);
        if (labelDiagnostics.Count > 0) return Failure(string.Join("; ", labelDiagnostics));
        var labels = new Dictionary<string, (string? View, DrawingLabelPlacement Placement)>(StringComparer.Ordinal);
        // "#N" names an entity of an imported STEP file; the exported part renumbers entities, so the record is
        // found again by its kind and name.
        var importedItems = directives.Any(d => d.Record.StartsWith('#'))
            ? FirmamentV2.FirmamentV2Parser.Parse(source, Path.GetDirectoryName(path)).Document?.Solids.Select(s => s.InlineStep?.SourceText).OfType<string>()
                .SelectMany(text => Step242SemanticPmiInspector.Inspect(text).Items).ToArray() ?? []
            : [];
        foreach (var directive in directives)
        {
            var origin = directive.Record.StartsWith('#') ? importedItems.Where(i => $"#{i.EntityId}" == directive.Record).ToArray() : [];
            var matches = inspected.Items.Where(i => directive.Record.StartsWith('#')
                ? origin.Length == 1 && i.Kind == origin[0].Kind && i.Name == origin[0].Name
                : (i.AuthoredName ?? i.Name) == directive.Record).ToArray();
            if (matches.Length == 0 && origin.Length == 1)
            {
                // A record the producer left unnamed carries a name made from its entity number, which the export
                // renumbers; such records keep their order among records of their kind.
                bool Same(Step242SemanticPmiInspectionItem i) => i.Kind == origin[0].Kind && i.DimensionKind == origin[0].DimensionKind;
                var ordinal = importedItems.Where(Same).ToList().IndexOf(origin[0]);
                var candidates = inspected.Items.Where(Same).ToArray();
                if (ordinal >= 0 && candidates.Length == importedItems.Count(Same) && candidates[ordinal].Value == origin[0].Value) matches = [candidates[ordinal]];
            }
            if (matches.Length != 1) return Failure($"drawing-label-record-unresolved: Label '{directive.Name}' names '{directive.Record}', which {(matches.Length == 0 ? "is not a" : "is more than one")} PMI record of the exported part.");
            var item = matches[0];
            labels[$"step:{item.EntityId}:{item.AuthoredName ?? item.Name}"] = (directive.View is { } v ? DrawingLabelDirectives.Views[v] : null,
                new(directive.Side is { } side ? Enum.Parse<DrawingBalloonSide>(side, true) : null, directive.At is { } at ? new DrawingPoint2(at.X, at.Y) : null));
        }
        foreach (var inspectedItem in inspected.Items)
        {
            var item = inspectedItem with { Name = inspectedItem.AuthoredName ?? inspectedItem.Name };
            var dimensionKind = item.DimensionKind?.ToLowerInvariant();
            var reference = $"step:{item.EntityId}:{item.Name}";
            var faces = item.GeometricFaceEntityIds.Where(faceMap.ContainsKey).Select(id => body.Topology.GetFace(faceMap[id])).ToArray();
            var edges = faces.SelectMany(f => f.LoopIds).Select(body.Topology.GetLoop).SelectMany(l => l.CoedgeIds)
                .Select(body.Topology.GetCoedge).Select(c => c.EdgeId.Value).ToHashSet();
            var points = samples.Boundaries.Where(line => line.EdgeId is { } id && edges.Contains(id)).SelectMany(line => line.Points).ToArray();
            // A record attached to auxiliary geometry (datum target points, isolated centre lines) is located by it.
            // Auxiliary geometry carried in its own representation can sit in another frame; points that fall outside
            // the part's envelope do not locate anything on it and are treated as no location.
            var auxiliary = faces.Length == 0 && options.ReferenceOnly
                ? item.GeometricItemEntityIds.SelectMany(g => Step242AuxiliaryGeometry.Points(parsed.Value, g)).ToArray() : [];
            if (auxiliary.Length > 0)
            {
                var all = samples.Boundaries.SelectMany(l => l.Points).ToArray();
                var margin = .1 * Math.Max(all.Max(p => p.X) - all.Min(p => p.X), Math.Max(all.Max(p => p.Y) - all.Min(p => p.Y), all.Max(p => p.Z) - all.Min(p => p.Z)));
                if (auxiliary.Any(p => p.X < all.Min(q => q.X) - margin || p.X > all.Max(q => q.X) + margin || p.Y < all.Min(q => q.Y) - margin
                        || p.Y > all.Max(q => q.Y) + margin || p.Z < all.Min(q => q.Z) - margin || p.Z > all.Max(q => q.Z) + margin))
                    auxiliary = [];
            }
            if (faces.Length == 0 && options.ReferenceOnly)
            {
                var id0 = item.Kind == "Datum" ? item.Name : (++balloon).ToString(CultureInfo.InvariantCulture);
                if (auxiliary.Length == 0)
                {
                    // Nothing in the file says where it applies: listed, not ballooned. A guessed leader would mislead.
                    schedule.Add([.. ScheduleRow(id0, item, null).Take(4), $"#{item.EntityId} NOT LOCATED"]);
                    continue;
                }
                schedule.Add(ScheduleRow(id0, item, null));
                var auxReference = $"step:{item.EntityId}:{item.Name}";
                pmi.Add(auxReference, new(auxReference, item.Kind == "Datum" ? DrawingAnnotationKind.Datum : DrawingAnnotationKind.Note, id0, item.Value, item.Target,
                    $"{path}; STEP #{item.EntityId}; auxiliary geometry {string.Join(",", item.GeometricItemEntityIds)}; source SHA256 {StableHash(source)}"));
                targets.Add(auxReference, auxiliary);
                assigned[labels.TryGetValue(auxReference, out var auxLabel) && auxLabel.View is { } auxView ? auxView : definitions[0].Name].Add(auxReference);
                continue;
            }
            if (points.Length == 0) return Failure($"drawing-pmi-target-unprojectable: '{item.Name}' has no supported bound edge samples.");
            var kind = dimensionKind is "holediameter" or "diameter" ? DrawingAnnotationKind.DiameterDimension : item.Kind switch
            {
                "Diameter" => DrawingAnnotationKind.DiameterDimension,
                "Datum" => DrawingAnnotationKind.Datum,
                "Position" => DrawingAnnotationKind.FeatureControlFrame,
                "Annotation" => DrawingAnnotationKind.Note,
                _ => DrawingAnnotationKind.LinearDimension
            };
            var nominalFromModel = item.Kind == "Dimension" && item.Value is null
                ? Visualization.PmiDisplayProjection.ModelNominal(item, faces.Select(f => body.TryGetFaceSurfaceGeometry(f.Id, out var g) ? g : null).OfType<SurfaceGeometry>().ToArray()) : null;
            var id = item.Kind == "Datum" ? item.Name : (++balloon).ToString(CultureInfo.InvariantCulture);
            schedule.Add(ScheduleRow(id, item, nominalFromModel));
            var display = item.Kind == "Datum" ? item.Name : id;
            pmi.Add(reference, new(reference, kind, display, item.Value ?? nominalFromModel, item.Target, $"{path}; STEP #{item.EntityId}; faces {string.Join(",", item.GeometricFaceEntityIds)}; source SHA256 {StableHash(source)}"));
            targets.Add(reference, points);
            // Points inside each planar target face (its tessellation's triangle centres): a leader to a surface seen
            // face-on ends on the surface with a dot, not on an edge of it.
            interiors[reference] = faces.Select(f => (Face: f, Surface: body.TryGetFaceSurfaceGeometry(f.Id, out var g) ? g : null))
                .Where(f => f.Surface?.Plane is not null && patches.ContainsKey(f.Face.Id.Value))
                .Select(f => (Normal: f.Surface!.Plane!.Value.Normal.ToVector(), Points: (IReadOnlyList<Point3D>)Centres(patches[f.Face.Id.Value]))).ToArray();
            var first = faces[0];
            body.TryGetFaceSurfaceGeometry(first.Id, out var surface);
            var axis = surface?.Cylinder?.Axis.ToVector() ?? surface?.Plane?.Normal.ToVector();
            if (kind == DrawingAnnotationKind.DiameterDimension && surface?.Cylinder is { } cylinder
                && item.Value is { } nominal && Math.Abs(nominal - 2 * cylinder.Radius) < 1e-6)
                cylinders[reference] = cylinder;
            var candidates = definitions.Select((view, i) => new JudgmentCandidate<int>(view.Name, _ => true,
                _ => axis is { } a ? Math.Abs(a.X * view.Direction[0] + a.Y * view.Direction[1] + a.Z * view.Direction[2]) : 0,
                TieBreakerPriority: i)).ToArray();
            var selected = labels.TryGetValue(reference, out var authored) && authored.View is { } authoredView
                ? authoredView : new JudgmentEngine<int>().Evaluate(0, candidates).Selection!.Value.Candidate.Name;
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
            inspected.Items.Count == 0 ? "NO SUPPORTED SEMANTIC PMI FOUND. GEOMETRY VIEWS ONLY." : "BALLOONS IDENTIFY ASSOCIATED FACES; EACH REQUIREMENT IS ONE ROW OF THE PMI SCHEDULE.",
            "VALUES IN PARENTHESES ARE MODEL NOMINALS FOR SIZES THE FILE STATES WITHOUT A VALUE (REFERENCE)." };
        var requested = new List<(string Name, double[] Direction, DrawingProjectionKind Projection, string[] Pmi)>();
        foreach (var view in definitions)
        {
            var chunks = assigned[view.Name].Chunk(18).ToArray();
            foreach (var chunk in chunks) requested.Add((view.Name, view.Direction, DrawingProjectionKind.Orthographic, chunk));
        }
        var pages = new List<DrawingPageIr>();
        var overview = OverviewViews(name, body);
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
            // Balloons ring the geometry, so the view takes the sheet's middle with one balloon ring of margin on every
            // side (26..271 x 41..140 mm); the projection keeps 12 mm of its own inside the rectangle it is given.
            var geometryViewport = new DrawingRect(14, 29, 269, 123);
            var view = DrawingProjectionEngine.Project([new(name, name, body)], request.Name, request.Projection,
                DrawingHiddenLinePolicy.Wireframe, request.Direction, request.Pmi, geometryViewport, new(page, "B2"));
            view = AtScale(view, StandardScale(view.Scale));
            DrawingPoint2 Project(Point3D p)
            {
                var c = view.Camera!;
                return new(c.OriginX + view.Scale * (p.X * c.Right[0] + p.Y * c.Right[1] + p.Z * c.Right[2]),
                    c.OriginY - view.Scale * (p.X * c.Up[0] + p.Y * c.Up[1] + p.Z * c.Up[2]));
            }
            // A leader ends on the feature where it reads best: the sample of its outline nearest the outline's middle
            // (a hole's rim, the middle of an edge-on face), not an arbitrary extreme point.
            var anchors = request.Pmi.ToDictionary(reference => reference, reference =>
            {
                var d = request.Direction;
                var facing = interiors.GetValueOrDefault(reference)?.Where(f => Math.Abs(f.Normal.X * d[0] + f.Normal.Y * d[1] + f.Normal.Z * d[2]) > .9)
                    .SelectMany(f => f.Points).Select(Project).ToArray() ?? [];
                if (facing.Length > 0)
                {
                    // Inside the face, near its middle.
                    var centre = new DrawingPoint2(facing.Average(p => p.X), facing.Average(p => p.Y));
                    surfaceAnchors.Add((page, reference));
                    return facing.MinBy(p => Math.Pow(p.X - centre.X, 2) + Math.Pow(p.Y - centre.Y, 2));
                }
                var projected = targets[reference].Select(Project).ToArray();
                var mid = new DrawingPoint2(projected.Average(p => p.X), projected.Average(p => p.Y));
                return projected.MinBy(p => Math.Pow(p.X - mid.X, 2) + Math.Pow(p.Y - mid.Y, 2));
            }, StringComparer.Ordinal);
            view = view with { Viewport = viewport, SemanticAnchors = anchors };
            var fields = new Dictionary<string, string> { ["Product"] = name, ["Units"] = "mm", ["Scale"] = DrawingScaleText.Ratio(view.Scale),
                ["View"] = request.Name, ["Sheet"] = $"{page}/{requested.Count + 1}", ["Status"] = "REFERENCE ONLY" };
            var info = DrawingTitleBlockLayout.Create(metadata, fields, width, height, new(page, "D5"));
            pages.Add(new(page, DrawingPageOrientation.Landscape, width, height, new(12, 20, 273, 145), [view], [], [], notes,
                scheme, info, LocateNotes(notes, page, scheme, DrawingTitleBlockLayout.NotesArea(width, height))));
        }
        // The PMI schedule: one row per requirement, as many sheets as it needs.
        const int rowsPerSheet = 15;
        var scheduleSheets = schedule.Chunk(rowsPerSheet).ToArray();
        var total = pages.Count + scheduleSheets.Length;
        pages = pages.Select(p => p with { InformationBlock = p.InformationBlock is { } block
            ? DrawingTitleBlockLayout.Create(metadata, new Dictionary<string, string>(block.Fields) { ["Sheet"] = $"{p.PageNumber}/{total}" }, width, height, new(p.PageNumber, "D5")) : null }).ToList();
        foreach (var (rows, index) in scheduleSheets.Select((r, i) => (r, i)))
        {
            var page = pages.Count + 1; var scheme = Zones(width, height);
            var table = new DrawingTableIr(scheduleSheets.Length > 1 ? $"PMI SCHEDULE ({index + 1}/{scheduleSheets.Length})" : "PMI SCHEDULE",
                ["ID", "CHARACTERISTIC", "REQUIREMENT (mm)", "DATUMS", "STEP"], rows, name,
                "AP242 semantic PMI read through the schema; values converted to millimetres and degrees from each measure's unit.",
                DrawingTableKind.Design, new(12, 30, 273, 7 * (rows.Length + 1)), new(page, "B2"));
            var fields = new Dictionary<string, string> { ["Product"] = name, ["Units"] = "mm", ["Scale"] = "-", ["View"] = "PMI SCHEDULE",
                ["Sheet"] = $"{page}/{total}", ["Status"] = "REFERENCE ONLY" };
            pages.Add(new(page, DrawingPageOrientation.Landscape, width, height, new(12, 20, 273, 145), [], [], [table], notes, scheme,
                DrawingTitleBlockLayout.Create(metadata, fields, width, height, new(page, "D5")), LocateNotes(notes, page, scheme, DrawingTitleBlockLayout.NotesArea(width, height))));
        }
        // Balloons for each view sheet; the general layout then audits them with everything else on the sheet.
        var balloonDiagnostics = new List<string>();
        var assignedByView = new Dictionary<(int, string), IReadOnlyList<string>>();
        pages = pages.Select(page =>
        {
            if (page.PageNumber == 1 || page.Views.Count != 1 || page.Views[0].AssignedPmi.Count == 0) return page;
            var view = page.Views[0];
            assignedByView[(page.PageNumber, view.Identity)] = view.AssignedPmi;
            var keepouts = new List<DrawingRect> { new(view.Viewport.X, view.Viewport.Y, view.Viewport.Width, 6) };
            keepouts.AddRange((page.LocatedNotes ?? []).Select(n => n.Bounds));
            if (page.InformationBlock is { } info) keepouts.Add(info.Bounds);
            keepouts.AddRange(page.Annotations.Select(a => a.SelectedCandidate.Body));
            var strokes = page.Annotations.SelectMany(a => a.Strokes).ToArray();
            var requests = view.AssignedPmi.Select(reference =>
            {
                var semantic = pmi[reference];
                var placement = labels.TryGetValue(reference, out var label) && (label.Placement.Side is not null || label.Placement.At is not null) ? label.Placement : null;
                return new DrawingBalloonLayout.Request(reference, semantic.Display, semantic.Kind, semantic.Provenance, semantic.Kind == DrawingAnnotationKind.Datum, placement)
                    { SurfaceAnchor = surfaceAnchors.Contains((page.PageNumber, reference)) };
            }).ToArray();
            var (balloons, problems) = DrawingBalloonLayout.Place(view, requests, keepouts, strokes);
            balloonDiagnostics.AddRange(problems);
            return page with { Annotations = [.. page.Annotations, .. balloons], Views = [view with { AssignedPmi = [] }] };
        }).ToList();
        if (balloonDiagnostics.Count > 0) return new(false, null, balloonDiagnostics);
        var layout = DrawingAnnotationLayout.Layout(pages, pmi);
        if (layout.Diagnostics.Count > 0) return new(false, null, layout.Diagnostics);
        layout = layout with { Pages = layout.Pages.Select(page => page with { Views = page.Views.Select(v =>
            assignedByView.TryGetValue((page.PageNumber, v.Identity), out var assignedPmi) ? v with { AssignedPmi = assignedPmi } : v).ToArray() }).ToArray() };
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

    /// <summary>
    /// The first sheet: TOP over FRONT, RIGHT beside FRONT (third-angle, aligned), at one standard scale fitted to the
    /// part's real extents in the area above the title block; the isometric takes the free top-right quadrant at its own
    /// standard scale. Ortho views draw hidden edges thin, grey and dashed so the visible outline dominates, and drop them
    /// when they outweigh the visible edges; the isometric
    /// shows visible edges and curved-face contours only, so it reads as a solid rather than a tangle of rear edges.
    /// </summary>
    private static DrawingViewIr[] OverviewViews(string name, BrepBody body)
    {
        // Area for views: inside the border, above the title block and notes (y 157), with room for each view's label.
        const double left = 14, top = 22, right = 283, bottom = 154, gap = 14, label = 6;
        DrawingViewIr Raw(string view, double[] direction, DrawingProjectionKind projection, DrawingHiddenLinePolicy policy) =>
            DrawingProjectionEngine.Project([new(name, name, body)], view, projection, policy, direction, [], new(0, 0, 124, 124), new(1, "B2"));
        var topView = Raw("TOP", [0, 0, 1], DrawingProjectionKind.Orthographic, DrawingHiddenLinePolicy.VisibleAndHidden);
        var front = Raw("FRONT", [0, -1, 0], DrawingProjectionKind.Orthographic, DrawingHiddenLinePolicy.VisibleAndHidden);
        var side = Raw("RIGHT", [1, 0, 0], DrawingProjectionKind.Orthographic, DrawingHiddenLinePolicy.VisibleAndHidden);
        var iso = Raw("ISOMETRIC", [1, 1, 1], DrawingProjectionKind.Isometric, DrawingHiddenLinePolicy.VisibleOnly);
        // Hidden edges help a simple part and bury a dense one: a view whose hidden linework outweighs its visible
        // linework shows visible edges only.
        static double Length(IEnumerable<DrawingProjectedPrimitiveIr> ps) =>
            ps.Sum(p => p.Points.Zip(p.Points.Skip(1), (u, v) => Math.Sqrt(Math.Pow(u.X - v.X, 2) + Math.Pow(u.Y - v.Y, 2))).Sum());
        DrawingViewIr Declutter(DrawingViewIr v) =>
            Length(v.Primitives.Where(p => p.Kind == DrawingPrimitiveKind.Hidden)) > Length(v.Primitives.Where(p => p.Kind != DrawingPrimitiveKind.Hidden))
                ? v with { Primitives = v.Primitives.Where(p => p.Kind != DrawingPrimitiveKind.Hidden).ToArray(), HiddenLinePolicy = DrawingHiddenLinePolicy.VisibleOnly } : v;
        topView = Declutter(topView); front = Declutter(front); side = Declutter(side);
        (double W, double H) Model(DrawingViewIr v) => (v.GeometryBounds.Width / v.Scale, v.GeometryBounds.Height / v.Scale);
        var (topW, topH) = Model(topView); var (frontW, frontH) = Model(front); var (sideW, sideH) = Model(side);
        // Columns: max(TOP, FRONT) width | RIGHT width. Rows: TOP height | max(FRONT, RIGHT) height. Each row carries a label.
        var fit = Math.Min((right - left - gap) / (Math.Max(topW, frontW) + sideW),
            (bottom - top - gap - 2 * label) / (topH + Math.Max(frontH, sideH)));
        var scale = StandardScale(fit);
        var column = Math.Max(topW, frontW) * scale; var row = topH * scale; var lower = Math.Max(frontH, sideH) * scale;
        var x0 = left; var x1 = x0 + column + gap; var sideRight = x1 + sideW * scale;
        var y0 = top + label; var y1 = y0 + row + gap + label;
        DrawingViewIr Put(DrawingViewIr view, double s, double centreX, double centreY)
        {
            var scaled = AtScale(view, s); var c = scaled.GeometryBounds.Center;
            return Translate(scaled, centreX - c.X, centreY - c.Y);
        }
        var placed = new List<DrawingViewIr> { Put(topView, scale, x0 + column / 2, y0 + row / 2) };
        // The isometric takes whichever free region shows it largest: beside TOP above RIGHT, or right of RIGHT at full
        // height (tall or narrow parts leave the second one wider).
        var (isoW, isoH) = Model(iso);
        var regions = new[]
        {
            (Name: "beside-top", Area: new DrawingRect(x1, top + label, right - x1, y1 - label - gap / 2 - (top + label))),
            (Name: "right-of-right", Area: new DrawingRect(sideRight + gap, top + label, right - sideRight - gap, bottom - top - label))
        };
        double Fit(DrawingRect area) => Math.Min(area.Width / isoW, area.Height / isoH);
        var chosen = new JudgmentEngine<int>().Evaluate(0, regions.Select((r, i) => new JudgmentCandidate<int>(r.Name,
            _ => r.Area.Width > 20 && r.Area.Height > 20,
            // A larger standard scale wins; at equal scale the conventional top-right place does.
            _ => StandardScale(Math.Max(1e-9, Fit(r.Area))), TieBreakerPriority: i)).ToArray()).Selection?.Candidate.Name;
        var isoArea = regions.FirstOrDefault(r => r.Name == chosen).Area;
        if (chosen is not null)
            placed.Add(Put(iso, StandardScale(Fit(isoArea)), isoArea.X + isoArea.Width / 2, isoArea.Y + isoArea.Height / 2));
        placed.Add(Put(front, scale, x0 + column / 2, y1 + lower / 2));
        placed.Add(Put(side, scale, x1 + sideW * scale / 2, y1 + lower / 2));
        // Centre the whole composition in the area; each label sits just above its geometry.
        var minX = placed.Min(v => v.GeometryBounds.X); var maxX = placed.Max(v => v.GeometryBounds.Right);
        var minY = placed.Min(v => v.GeometryBounds.Y) - label; var maxY = placed.Max(v => v.GeometryBounds.Bottom);
        var dx = (left + right - minX - maxX) / 2; var dy = (top + bottom - minY - maxY) / 2;
        return placed.Select(v =>
        {
            var moved = Translate(v, dx, dy); var g = moved.GeometryBounds;
            return moved with { Viewport = new(g.X, g.Y - label, Math.Max(g.Width, 40), g.Height + label) };
        }).ToArray();
    }

    private static DrawingViewIr Translate(DrawingViewIr view, double dx, double dy)
    {
        DrawingPoint2 Move(DrawingPoint2 p) => new(p.X + dx, p.Y + dy);
        var camera = view.Camera!;
        return view with
        {
            Primitives = view.Primitives.Select(p => p with { Points = p.Points.Select(Move).ToArray() }).ToArray(),
            GeometryBounds = new(view.GeometryBounds.X + dx, view.GeometryBounds.Y + dy, view.GeometryBounds.Width, view.GeometryBounds.Height),
            SemanticAnchors = view.SemanticAnchors.ToDictionary(a => a.Key, a => Move(a.Value), StringComparer.Ordinal),
            Camera = camera with { OriginX = camera.OriginX + dx, OriginY = camera.OriginY + dy }
        };
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

    /// <summary>Reference sheets list every record; location and values may be absent and are shown as such.</summary>
    private static string? ValidateReferencePmi(IReadOnlyList<Step242SemanticPmiInspectionItem> items,
        IReadOnlyDictionary<int, Aetheris.Kernel.Core.Topology.FaceId> faceMap)
    {
        // Records without faces are located by auxiliary geometry, or listed as not located; none is dropped.
        _ = items; _ = faceMap;
        return null;
    }

    private static string SizePrefix(Step242SemanticPmiInspectionItem item) => Visualization.PmiDrafting.SizePrefix(item);

    /// <summary>One PMI schedule row: ID, characteristic, requirement in millimetres (angles in degrees), datums, source.</summary>
    private static IReadOnlyList<string> ScheduleRow(string id, Step242SemanticPmiInspectionItem item, double? nominalFromModel)
    {
        var angle = item.ValueUnit == "rad";
        string N(double v) => (angle ? (v * 180 / Math.PI).ToString("0.####", CultureInfo.InvariantCulture) + "°" : v.ToString("0.####", CultureInfo.InvariantCulture));
        var kind = item.DimensionKind?.ToLowerInvariant();
        var diameter = item.Kind == "Diameter" || kind is "diameter" or "holediameter";
        var characteristic = item.Kind switch
        {
            "Datum" => "DATUM FEATURE",
            "Annotation" => "NOTE",
            "Dimension" or "Diameter" => diameter ? "SIZE (DIAMETER)" : angle ? "ANGLE" : kind is "linear distance" or "distance" ? "DISTANCE" : "SIZE (" + (item.DimensionKind ?? "LINEAR").ToUpperInvariant() + ")",
            var other => string.Concat(other.Select((c, i) => i > 0 && char.IsUpper(c) ? " " + c : c.ToString())).ToUpperInvariant()
        };
        var quantity = item.Quantity is > 1 ? $"{item.Quantity}X " : "";
        string requirement;
        if (item.Kind == "Datum") requirement = "-";
        else if (item.Kind == "Annotation") requirement = item.Text ?? "";
        else if (item.Value is { } value)
        {
            requirement = quantity + SizePrefix(item) + N(value);
            if (item.TolerancePlus is { } plus && item.ToleranceMinus is { } minus)
                requirement += Math.Abs(plus - minus) < 1e-12 ? " ±" + N(plus)
                    : $" {(Math.Abs(plus) < 1e-12 ? "0" : "+" + N(plus))}/{(Math.Abs(minus) < 1e-12 ? "0" : "-" + N(minus))}";
            if (item.Kind is not ("Dimension" or "Diameter"))
                requirement = (item.ZoneForms.Any(f => f.Contains("cylind", StringComparison.OrdinalIgnoreCase)) ? "Ø" : "") + requirement
                    + string.Concat(item.Modifiers.Select(m => m switch { "MAXIMUM_MATERIAL_REQUIREMENT" => " (M)", "LEAST_MATERIAL_REQUIREMENT" => " (L)",
                        "FREE_STATE" => " (F)", "STATISTICAL_TOLERANCE" => " (ST)", "COMMON_ZONE" => " CZ", "TANGENT_PLANE" => " (T)", _ => " " + m }))
                    + string.Concat(item.ToleranceParameters.Select(p => " " + p));
            requirement += string.Concat(item.ValueQualifiers.Select(q => " " + q));
            if (item.Depth is { } depth) requirement += " DEPTH " + N(depth);
        }
        else requirement = nominalFromModel is { } model ? $"({quantity}{SizePrefix(item)}{N(model)}) NO VALUE IN FILE" : "NO VALUE IN FILE";
        var datums = item.DatumReferenceFrames.Count > 0 ? string.Join(" | ", item.DatumReferenceFrames.Select(f => string.Join("-", f.Labels)))
            : item.DatumReferences.Count > 0 ? string.Join(" | ", item.DatumReferences) : "-";
        return [id, characteristic, requirement, datums, "#" + item.EntityId];
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
