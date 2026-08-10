using System.Diagnostics;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using Aetheris.Kernel.Core.Brep;
using Aetheris.Kernel.Core.Geometry;
using Aetheris.Kernel.Core.Math;
using Aetheris.Kernel.Core.Step242;
using Aetheris.Kernel.Firmament.FirmamentV2;

namespace Aetheris.Kernel.Firmament.Drawing;

/// <summary>
/// Bounded M0 Drawing compiler. The source product is compiled through the ordinary
/// Firmament exact-geometry route; Drawing declarations are erased only after they
/// have been normalized to DrawingIR.
/// </summary>
public static class FirmamentDrawingCompiler
{
    public const string DrawingMissing = "drawing-missing-declaration";
    public const string DrawingSourceUnknown = "drawing-source-product-unknown";
    public const string DrawingViewMissing = "drawing-primary-view-required";
    public const string DrawingPmiUnknown = "drawing-pmi-reference-unknown";
    public const string DrawingConceptUnsatisfied = "drawing-concept-require-unsatisfied";
    public const string DrawingLayoutImpossible = "drawing-layout-impossible";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new JsonStringEnumConverter() }
    };

    public static DrawingCompileResult Compile(string sourcePath, string outputDirectory)
    {
        var fullSourcePath = Path.GetFullPath(sourcePath);
        var output = Path.GetFullPath(outputDirectory);
        if (!File.Exists(fullSourcePath)) return Failure($"drawing-source-file-missing: {fullSourcePath}");
        Directory.CreateDirectory(output);

        var source = File.ReadAllText(fullSourcePath, Encoding.UTF8).Replace("\r\n", "\n", StringComparison.Ordinal);
        var parse = ParseDrawingLanguage(source);
        if (parse.Diagnostics.Count > 0 || parse.Drawing is null) return new(false, null, parse.Diagnostics);

        var geometrySource = EraseDrawingDeclarations(source, parse.Ranges);
        var sourceWatch = Stopwatch.StartNew();
        var exact = FirmamentBuildAndExport.CompileSource(geometrySource, Path.GetDirectoryName(fullSourcePath));
        sourceWatch.Stop();
        if (!exact.IsSuccess)
            return new(false, null, exact.Diagnostics.Select(diagnostic => $"{diagnostic.Source}: {diagnostic.Message}").ToArray());

        var imported = Step242Importer.ImportBody(exact.Value.StepText);
        if (!imported.IsSuccess)
            return new(false, null, imported.Diagnostics.Select(diagnostic => $"{diagnostic.Source}: {diagnostic.Message}").ToArray());

        var semanticParse = FirmamentV2Parser.Parse(geometrySource, Path.GetDirectoryName(fullSourcePath));
        if (!semanticParse.IsSuccess || semanticParse.Document is null)
            return new(false, null, semanticParse.Diagnostics);

        if (!string.Equals(parse.Drawing.Source, semanticParse.Document.ModelName, StringComparison.Ordinal))
            return Failure($"{DrawingSourceUnknown}: '{parse.Drawing.Source}' does not name authoritative Model '{semanticParse.Document.ModelName}'.");

        var pmi = NormalizePmi(semanticParse.Document);
        var unknownPmi = parse.Drawing.Views.SelectMany(view => view.Pmi).Distinct(StringComparer.Ordinal)
            .Where(reference => !pmi.ContainsKey(reference)).Order(StringComparer.Ordinal).ToArray();
        if (unknownPmi.Length > 0) return Failure($"{DrawingPmiUnknown}: {string.Join(", ", unknownPmi)}");

        var conceptFailures = ValidateConcept(parse.Drawing, parse.Concept);
        if (conceptFailures.Count > 0) return new(false, null, conceptFailures);

        var projectionWatch = Stopwatch.StartNew();
        var pages = ProjectAndAllocate(imported.Value, parse.Drawing, semanticParse.Document, pmi);
        projectionWatch.Stop();

        var layoutWatch = Stopwatch.StartNew();
        var layout = DrawingAnnotationLayout.Layout(pages, pmi);
        layoutWatch.Stop();
        if (layout.Diagnostics.Count > 0) return new(false, null, layout.Diagnostics);

        var specialization = StableHash($"{parse.Drawing.Template}|{parse.Drawing.Source}|{parse.Drawing.Name}")[..16];
        var staticSources = layout.Pages.SelectMany(page => page.Tables).Select(table => table.SourceIdentity).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray();
        var metadata = new DrawingMetadataIr(parse.Drawing.Title ?? parse.Drawing.Name, parse.Drawing.Source,
            parse.Drawing.PartNumber, parse.Drawing.Revision, parse.Drawing.Material, parse.Drawing.Name, parse.Drawing.Template);
        var provenance = new DrawingProvenanceIr(parse.Drawing.Source, parse.Concept?.Name, parse.Drawing.Template,
            new Dictionary<string, string>(StringComparer.Ordinal) { ["Product"] = parse.Drawing.Source }, specialization, staticSources);

        var preliminary = new DrawingIr(parse.Drawing.Name, metadata, provenance, layout.Pages, layout.Evidence,
            new(sourceWatch.Elapsed.TotalMilliseconds, projectionWatch.Elapsed.TotalMilliseconds, layoutWatch.Elapsed.TotalMilliseconds, 0, 0),
            [], "Structured BRep edge projection; coincident projected edges are merged. M0 does not claim exact face-occlusion HLR.");

        var stem = Sanitize(parse.Drawing.Name);
        var irPath = Path.Combine(output, $"{stem}.drawing.json");
        var svgPath = Path.Combine(output, $"{stem}.svg");
        var pdfPath = Path.Combine(output, $"{stem}.pdf");
        var validationPath = Path.Combine(output, $"{stem}.validation.json");

        var renderWatch = Stopwatch.StartNew();
        var svg = DrawingSvgRenderer.Render(preliminary);
        File.WriteAllText(svgPath, svg, new UTF8Encoding(false));
        renderWatch.Stop();
        var pdfWatch = Stopwatch.StartNew();
        DrawingVectorPdfWriter.Write(preliminary, pdfPath);
        pdfWatch.Stop();

        var measuredPerformance = preliminary.Performance with { RenderMilliseconds = renderWatch.Elapsed.TotalMilliseconds, PdfMilliseconds = pdfWatch.Elapsed.TotalMilliseconds };
        // Timings are evidence, not drawing semantics. Keep them out of deterministic IR/PDF hashes.
        var drawing = preliminary with { Performance = new(0, 0, 0, 0, 0) };
        var json = JsonSerializer.Serialize(drawing, JsonOptions);
        File.WriteAllText(irPath, json, new UTF8Encoding(false));
        var irHash = HashBytes(Encoding.UTF8.GetBytes(json));
        var pdfHash = HashBytes(File.ReadAllBytes(pdfPath));
        var validation = new
        {
            success = true,
            schema = drawing.SchemaVersion,
            authoritativeSource = drawing.Provenance.SourceProductIdentity,
            pageCount = drawing.Pages.Count,
            a4Only = drawing.Pages.All(page => (page.WidthMillimetres == 210 && page.HeightMillimetres == 297) || (page.WidthMillimetres == 297 && page.HeightMillimetres == 210)),
            vectorPdf = true,
            rasterImages = 0,
            drawing.LayoutEvidence.TextModelCollisionsAfter,
            drawing.LayoutEvidence.TextTextCollisionsAfter,
            drawing.LayoutEvidence.FailedAnnotationCount,
            drawingIrSha256 = irHash,
            pdfSha256 = pdfHash,
            measuredPerformance
        };
        File.WriteAllText(validationPath, JsonSerializer.Serialize(validation, JsonOptions), new UTF8Encoding(false));
        return new(true, new(drawing, irPath, svgPath, pdfPath, validationPath, pdfHash, irHash), []);
    }

    private static IReadOnlyList<string> ValidateConcept(ParsedDrawing drawing, ParsedConcept? concept)
    {
        if (concept is null || !string.Equals(drawing.Concept, concept.Name, StringComparison.Ordinal)) return [];
        var failures = new List<string>();
        foreach (var requirement in concept.Requirements)
        {
            var satisfied = requirement switch
            {
                "PrimaryView" => drawing.Views.Any(view => view.Projection == DrawingProjectionKind.Orthographic),
                "ManufacturingPmi" => drawing.Views.Any(view => view.Pmi.Count > 0),
                "Material" => !string.IsNullOrWhiteSpace(drawing.Material),
                "DesignTable" => !string.IsNullOrWhiteSpace(drawing.Table),
                "RevisionMetadata" => !string.IsNullOrWhiteSpace(drawing.Revision),
                _ => false
            };
            if (!satisfied) failures.Add($"{DrawingConceptUnsatisfied}: {concept.Name}.{requirement}");
        }
        return failures;
    }

    private static Dictionary<string, PmiPresentation> NormalizePmi(FirmamentV2Document document)
    {
        var result = new Dictionary<string, PmiPresentation>(StringComparer.Ordinal);
        foreach (var record in document.BoundPmi?.Datums ?? [])
            result[record.Name] = new(record.Name, DrawingAnnotationKind.Datum, record.Name, null, record.Targets.FirstOrDefault() ?? record.Name, "Firmament Semantic PMI");
        foreach (var record in document.BoundPmi?.Dimensions ?? [])
        {
            var nominal = record.DimensionValue?.NumericValue;
            var tolerance = record.DimensionTolerance;
            var value = nominal.HasValue ? FormatEngineeringValue(nominal.Value, record.DimensionValue!.Unit, tolerance) : record.Name;
            result[record.Name] = new(record.Name,
                record.Kind == FirmamentV2PmiKind.HoleDiameter ? DrawingAnnotationKind.DiameterDimension : DrawingAnnotationKind.LinearDimension,
                record.Kind == FirmamentV2PmiKind.HoleDiameter ? $"Ø{value}" : value,
                nominal, record.Targets.FirstOrDefault() ?? record.Name, record.ProjectionSource ?? "Firmament Semantic PMI");
        }
        foreach (var record in document.BoundPmi?.Controls ?? [])
        {
            var tolerance = record.ControlTolerance?.NumericValue?.ToString("0.###", CultureInfo.InvariantCulture) ?? "";
            result[record.Name] = new(record.Name, DrawingAnnotationKind.FeatureControlFrame,
                $"{record.Kind.ToString().ToUpperInvariant()} | {tolerance} | {string.Join(" | ", record.DatumRefs)}", null,
                record.Targets.FirstOrDefault() ?? record.Name, "Firmament Semantic PMI");
        }
        return result;
    }

    private static string FormatEngineeringValue(double nominal, string? unit, FirmamentV2Tolerance? tolerance)
    {
        var value = $"{nominal:0.###} {unit ?? "mm"}";
        if (tolerance is null) return value;
        return Math.Abs(tolerance.Plus - tolerance.Minus) < 1e-12
            ? $"{value} ±{tolerance.Plus:0.###}"
            : $"{value} +{tolerance.Plus:0.###}/-{tolerance.Minus:0.###}";
    }

    private static IReadOnlyList<DrawingPageIr> ProjectAndAllocate(BrepBody body, ParsedDrawing drawing, FirmamentV2Document semanticDocument, IReadOnlyDictionary<string, PmiPresentation> pmi)
    {
        var landscape = drawing.Orientation == DrawingPageOrientation.Landscape;
        var width = landscape ? 297d : 210d;
        var height = landscape ? 210d : 297d;
        var content = new DrawingRect(10, 16, width - 20, height - 36);
        var viewAreaHeight = content.Height - 25;
        var columns = drawing.Views.Count <= 2 ? drawing.Views.Count : 2;
        var rows = (int)Math.Ceiling(drawing.Views.Count / (double)Math.Max(1, columns));
        var gap = 8d;
        var cellWidth = (content.Width - gap * Math.Max(0, columns - 1)) / Math.Max(1, columns);
        var cellHeight = (viewAreaHeight - gap * Math.Max(0, rows - 1)) / Math.Max(1, rows);
        var views = new List<DrawingViewIr>();
        for (var index = 0; index < drawing.Views.Count; index++)
        {
            var column = index % columns;
            var row = index / columns;
            var viewport = new DrawingRect(content.X + column * (cellWidth + gap), content.Y + row * (cellHeight + gap), cellWidth, cellHeight);
            views.Add(ProjectView(body, drawing.Views[index], viewport));
        }

        var tables = NormalizeTables(drawing, semanticDocument);
        var page1 = new DrawingPageIr(1, drawing.Orientation, width, height, content, views, [], [], drawing.Notes);
        if (tables.Count == 0) return [page1];
        // M0 deliberately flows tables to another readable A4 page instead of shrinking views.
        var tablePage = new DrawingPageIr(2, drawing.Orientation, width, height, content, [], [], tables, []);
        return [page1, tablePage];
    }

    private static IReadOnlyList<DrawingTableIr> NormalizeTables(ParsedDrawing drawing, FirmamentV2Document document)
    {
        if (string.IsNullOrWhiteSpace(drawing.Table)) return [];
        var table = document.StaticAuthoring?.Tables?.FirstOrDefault(candidate => string.Equals(candidate.Name, drawing.Table, StringComparison.Ordinal));
        if (table is null) return [];
        var columns = table.Columns.Keys.Order(StringComparer.Ordinal).ToArray();
        var rows = Enumerable.Range(0, table.RowCount)
            .Select(row => (IReadOnlyList<string>)columns.Select(column => table.Columns[column][row]).ToArray()).ToArray();
        return [new(table.Name, columns, rows, table.Name, "Firmament Static Table; shared compile-time source")];
    }

    private static DrawingViewIr ProjectView(BrepBody body, ParsedView view, DrawingRect viewport)
    {
        var direction = Normalize(new Vector3D(view.Direction[0], view.Direction[1], view.Direction[2]));
        var upCandidate = Math.Abs(direction.Z) > 0.9 ? new Vector3D(0, 1, 0) : new Vector3D(0, 0, 1);
        var right = Normalize(upCandidate.Cross(direction));
        var up = Normalize(direction.Cross(right));
        DrawingPoint2 Project(Point3D point) => new(point.X * right.X + point.Y * right.Y + point.Z * right.Z,
            point.X * up.X + point.Y * up.Y + point.Z * up.Z);
        double Depth(Point3D point) => point.X * direction.X + point.Y * direction.Y + point.Z * direction.Z;

        var raw = new List<(string Id, IReadOnlyList<DrawingPoint2> Points, double Depth)>();
        foreach (var edge in body.Topology.Edges.OrderBy(edge => edge.Id.Value))
        {
            if (IsCoplanarInternalEdge(body, edge.Id)) continue;
            if (!body.TryGetVertexPoint(edge.StartVertexId, out var start) || !body.TryGetVertexPoint(edge.EndVertexId, out var end)) continue;
            IReadOnlyList<Point3D> points;
            if (body.TryGetEdgeCurveGeometry(edge.Id, out var curve) && curve?.Kind == CurveGeometryKind.Circle3 && curve.Circle3 is { } circle)
                points = Enumerable.Range(0, 49).Select(i => circle.Evaluate(i * Math.Tau / 48d)).ToArray();
            else if (body.TryGetEdgeCurveGeometry(edge.Id, out curve) && curve?.Kind == CurveGeometryKind.Ellipse3 && curve.Ellipse3 is { } ellipse)
                points = Enumerable.Range(0, 49).Select(i => ellipse.Evaluate(i * Math.Tau / 48d)).ToArray();
            else points = [start, end];
            raw.Add(($"edge:{edge.Id.Value}", points.Select(Project).ToArray(), points.Average(Depth)));
        }
        if (raw.Count == 0) throw new InvalidOperationException("drawing-projection-no-exact-edges");
        var all = raw.SelectMany(item => item.Points).ToArray();
        var minX = all.Min(point => point.X); var maxX = all.Max(point => point.X);
        var minY = all.Min(point => point.Y); var maxY = all.Max(point => point.Y);
        var modelWidth = Math.Max(1e-9, maxX - minX); var modelHeight = Math.Max(1e-9, maxY - minY);
        var scale = Math.Min((viewport.Width - 24) / modelWidth, (viewport.Height - 24) / modelHeight);
        var originX = viewport.X + (viewport.Width - modelWidth * scale) / 2 - minX * scale;
        var originY = viewport.Y + (viewport.Height - modelHeight * scale) / 2 + maxY * scale;
        DrawingPoint2 Page(DrawingPoint2 point) => new(originX + point.X * scale, originY - point.Y * scale);
        var dedupe = new HashSet<string>(StringComparer.Ordinal);
        var projected = new List<DrawingProjectedPrimitiveIr>();
        foreach (var item in raw.OrderByDescending(item => item.Depth).ThenBy(item => item.Id, StringComparer.Ordinal))
        {
            var points = item.Points.Select(Page).ToArray();
            var key = CanonicalPolyline(points);
            if (!dedupe.Add(key)) continue;
            var kind = item.Points.Count > 2 ? DrawingPrimitiveKind.Silhouette : DrawingPrimitiveKind.Visible;
            projected.Add(new(item.Id, kind, points, null, item.Depth));
        }
        var geometryBounds = new DrawingRect(originX + minX * scale, originY - maxY * scale, modelWidth * scale, modelHeight * scale);
        var anchors = new Dictionary<string, DrawingPoint2>(StringComparer.Ordinal);
        var center = new DrawingPoint2(geometryBounds.X + geometryBounds.Width / 2, geometryBounds.Y + geometryBounds.Height / 2);
        foreach (var reference in view.Pmi) anchors[reference] = center;
        return new(view.Name, view.Projection, view.HiddenLines, new(direction.X, direction.Y), view.Direction, viewport, geometryBounds, scale, projected, anchors, view.Pmi);
    }

    private static bool IsCoplanarInternalEdge(BrepBody body, Aetheris.Kernel.Core.Topology.EdgeId edgeId)
    {
        var loopIds = body.Topology.Coedges.Where(coedge => coedge.EdgeId == edgeId).Select(coedge => coedge.LoopId).ToHashSet();
        var faces = body.Topology.Faces.Where(face => face.LoopIds.Any(loopIds.Contains)).ToArray();
        if (faces.Length != 2) return false;
        if (!body.TryGetFaceSurfaceGeometry(faces[0].Id, out var first) || !body.TryGetFaceSurfaceGeometry(faces[1].Id, out var second)
            || first?.Plane is not { } a || second?.Plane is not { } b) return false;
        var na = a.Normal.ToVector(); var nb = b.Normal.ToVector();
        var parallel = Math.Abs(Math.Abs(na.Dot(nb)) - 1d) < 1e-8;
        var separation = Math.Abs((b.Origin - a.Origin).Dot(na));
        return parallel && separation < 1e-7;
    }

    private static string CanonicalPolyline(IReadOnlyList<DrawingPoint2> points)
    {
        string Key(IEnumerable<DrawingPoint2> value) => string.Join(";", value.Select(point => $"{Math.Round(point.X, 5)},{Math.Round(point.Y, 5)}"));
        var forward = Key(points); var reverse = Key(points.Reverse());
        return string.CompareOrdinal(forward, reverse) <= 0 ? forward : reverse;
    }

    private static Vector3D Normalize(Vector3D vector)
    {
        var length = Math.Sqrt(vector.Dot(vector));
        if (length < 1e-12) throw new InvalidOperationException("drawing-view-direction-zero");
        return vector * (1d / length);
    }

    private static ParseResult ParseDrawingLanguage(string source)
    {
        var diagnostics = new List<string>();
        var ranges = new List<(int Start, int Length)>();
        ParsedConcept? concept = null;
        var conceptMatch = Regex.Match(source, @"\bConcept\s+Drawing\s+(?<name>[A-Za-z_]\w*)\s*\{", RegexOptions.CultureInvariant);
        if (conceptMatch.Success)
        {
            var block = ReadBlock(source, conceptMatch.Index + conceptMatch.Length - 1);
            if (block is null) diagnostics.Add("drawing-concept-malformed");
            else
            {
                concept = new(conceptMatch.Groups["name"].Value,
                    Regex.Matches(block.Value.Body, @"\bRequire\s+(?<name>[A-Za-z_]\w*)\s*;?", RegexOptions.CultureInvariant)
                        .Select(match => match.Groups["name"].Value).ToArray());
                ranges.Add((conceptMatch.Index, block.Value.End - conceptMatch.Index + 1));
            }
        }

        ParsedTemplate? template = null;
        var templateMatch = Regex.Match(source, @"\bTemplate\s*<\s*(?<parameter>[A-Za-z_]\w*)\s*:\s*Product\s*>\s*Drawing\s+(?<name>[A-Za-z_]\w*)\s*(?::\s*(?<concept>[A-Za-z_]\w*))?\s*\{", RegexOptions.CultureInvariant);
        if (templateMatch.Success)
        {
            var block = ReadBlock(source, templateMatch.Index + templateMatch.Length - 1);
            if (block is null) diagnostics.Add("drawing-template-malformed");
            else
            {
                template = new(templateMatch.Groups["name"].Value, templateMatch.Groups["parameter"].Value,
                    templateMatch.Groups["concept"].Value, block.Value.Body);
                ranges.Add((templateMatch.Index, block.Value.End - templateMatch.Index + 1));
            }
        }

        ParsedDrawing? drawing = null;
        var application = Regex.Match(source, @"\bDrawing\s+(?<name>[A-Za-z_]\w*)\s*=\s*(?<template>[A-Za-z_]\w*)\s*<\s*Product\s*:\s*(?<product>[A-Za-z_]\w*)\s*>\s*;?", RegexOptions.CultureInvariant);
        if (application.Success && template is not null)
        {
            if (!string.Equals(application.Groups["template"].Value, template.Name, StringComparison.Ordinal)) diagnostics.Add("drawing-template-unknown");
            else drawing = ParseDrawingBody(application.Groups["name"].Value, template.Name, template.Concept, application.Groups["product"].Value,
                template.Body.Replace(template.Parameter, application.Groups["product"].Value, StringComparison.Ordinal), diagnostics);
            ranges.Add((application.Index, application.Length));
        }
        else
        {
            var literal = Regex.Match(source, @"\bDrawing\s+(?<name>[A-Za-z_]\w*)\s*(?::\s*(?<concept>[A-Za-z_]\w*))?\s*\{", RegexOptions.CultureInvariant);
            if (literal.Success)
            {
                var block = ReadBlock(source, literal.Index + literal.Length - 1);
                if (block is not null)
                {
                    drawing = ParseDrawingBody(literal.Groups["name"].Value, "Literal", literal.Groups["concept"].Value, null, block.Value.Body, diagnostics);
                    ranges.Add((literal.Index, block.Value.End - literal.Index + 1));
                }
            }
        }
        if (drawing is null && diagnostics.Count == 0) diagnostics.Add(DrawingMissing);
        return new(drawing, concept, ranges, diagnostics);
    }

    private static ParsedDrawing ParseDrawingBody(string name, string template, string concept, string? applicationProduct, string body, List<string> diagnostics)
    {
        string? Field(string field) => Regex.Match(body, $@"\b{Regex.Escape(field)}\s*:\s*(?<value>[^;\n}}]+)", RegexOptions.CultureInvariant) is { Success: true } match ? TrimValue(match.Groups["value"].Value) : null;
        var source = applicationProduct ?? Field("Source");
        if (string.IsNullOrWhiteSpace(source)) diagnostics.Add("drawing-source-required");
        var orientation = string.Equals(Field("Orientation"), "Portrait", StringComparison.Ordinal) ? DrawingPageOrientation.Portrait : DrawingPageOrientation.Landscape;
        var views = new List<ParsedView>();
        foreach (Match match in Regex.Matches(body, @"\bView\s+(?<name>[A-Za-z_]\w*)\s*\{", RegexOptions.CultureInvariant))
        {
            var block = ReadBlock(body, match.Index + match.Length - 1);
            if (block is null) continue;
            var directionMatch = Regex.Match(block.Value.Body, @"\bDirection\s*:\s*(?<direction>\[[^\]]+\]|[+-][XYZ])", RegexOptions.CultureInvariant);
            if (!directionMatch.Success) { diagnostics.Add($"drawing-view-direction-required: {match.Groups["name"].Value}"); continue; }
            var direction = ParseDirection(directionMatch.Groups["direction"].Value);
            var projection = string.Equals(Regex.Match(block.Value.Body, @"\bProjection\s*:\s*(?<value>\w+)").Groups["value"].Value, "Isometric", StringComparison.Ordinal)
                || direction.Count(value => Math.Abs(value) > 1e-12) > 1 ? DrawingProjectionKind.Isometric : DrawingProjectionKind.Orthographic;
            var hidden = string.Equals(Regex.Match(block.Value.Body, @"\bHiddenLines\s*:\s*(?<value>\w+)").Groups["value"].Value, "VisibleAndHidden", StringComparison.Ordinal)
                ? DrawingHiddenLinePolicy.VisibleAndHidden : DrawingHiddenLinePolicy.VisibleOnly;
            var pmiMatch = Regex.Match(block.Value.Body, @"\bPMI\s*:\s*\[(?<items>[^\]]*)\]", RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);
            var assigned = pmiMatch.Success ? pmiMatch.Groups["items"].Value.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries) : [];
            views.Add(new(match.Groups["name"].Value, direction, projection, hidden, assigned));
        }
        if (views.Count == 0) diagnostics.Add(DrawingViewMissing);
        var notesMatch = Regex.Match(body, @"\bNotes\s*:\s*\[(?<items>[^\]]*)\]", RegexOptions.CultureInvariant);
        var notes = notesMatch.Success ? Regex.Matches(notesMatch.Groups["items"].Value, "\"(?<text>[^\"]*)\"").Select(match => match.Groups["text"].Value).ToArray() : [];
        return new(name, template, concept, source ?? "", orientation, views, Field("Title"), Field("PartNumber"), Field("Revision"), Field("Material"), Field("Table"), notes);
    }

    private static IReadOnlyList<double> ParseDirection(string source) => source.Trim() switch
    {
        "+X" => [1, 0, 0], "-X" => [-1, 0, 0], "+Y" => [0, 1, 0], "-Y" => [0, -1, 0], "+Z" => [0, 0, 1], "-Z" => [0, 0, -1],
        _ => source.Trim().Trim('[', ']').Split(',', StringSplitOptions.TrimEntries).Select(value => double.Parse(value, CultureInfo.InvariantCulture)).ToArray()
    };

    private static (string Body, int End)? ReadBlock(string source, int open)
    {
        if (open < 0 || open >= source.Length || source[open] != '{') return null;
        var depth = 0; var quoted = false;
        for (var i = open; i < source.Length; i++)
        {
            if (source[i] == '"' && (i == 0 || source[i - 1] != '\\')) quoted = !quoted;
            if (quoted) continue;
            if (source[i] == '{') depth++;
            else if (source[i] == '}' && --depth == 0) return (source[(open + 1)..i], i);
        }
        return null;
    }

    private static string EraseDrawingDeclarations(string source, IReadOnlyList<(int Start, int Length)> ranges)
    {
        var chars = source.ToCharArray();
        foreach (var range in ranges)
            for (var i = range.Start; i < range.Start + range.Length; i++) if (chars[i] != '\n') chars[i] = ' ';
        return new string(chars);
    }

    private static string TrimValue(string value) => value.Trim().Trim('"');
    private static string StableHash(string value) => HashBytes(Encoding.UTF8.GetBytes(value));
    private static string HashBytes(byte[] bytes) => Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
    private static string Sanitize(string value) => Regex.Replace(value, "[^A-Za-z0-9._-]", "-");
    private static DrawingCompileResult Failure(string diagnostic) => new(false, null, [diagnostic]);

    private sealed record ParsedConcept(string Name, IReadOnlyList<string> Requirements);
    private sealed record ParsedTemplate(string Name, string Parameter, string Concept, string Body);
    private sealed record ParsedView(string Name, IReadOnlyList<double> Direction, DrawingProjectionKind Projection, DrawingHiddenLinePolicy HiddenLines, IReadOnlyList<string> Pmi);
    private sealed record ParsedDrawing(string Name, string Template, string Concept, string Source, DrawingPageOrientation Orientation,
        IReadOnlyList<ParsedView> Views, string? Title, string? PartNumber, string? Revision, string? Material, string? Table, IReadOnlyList<string> Notes);
    private sealed record ParseResult(ParsedDrawing? Drawing, ParsedConcept? Concept, IReadOnlyList<(int Start, int Length)> Ranges, IReadOnlyList<string> Diagnostics);
    internal sealed record PmiPresentation(string Identity, DrawingAnnotationKind Kind, string Display, double? Nominal, string Target, string Provenance);
}
