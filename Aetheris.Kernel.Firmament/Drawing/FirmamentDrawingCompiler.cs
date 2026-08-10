using System.Diagnostics;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using Aetheris.Kernel.Core.Step242;
using Aetheris.Kernel.Firmament.Assembly;
using Aetheris.Kernel.Firmament.FirmamentV2;
using Aetheris.Semantics;

namespace Aetheris.Kernel.Firmament.Drawing;

/// <summary>Drawing M0B compiler. Part and Assembly products share one DrawingIR and sibling
/// SVG/React/PDF presentation contract. Assembly occurrences remain independently projected.</summary>
public static class FirmamentDrawingCompiler
{
    public const string DrawingMissing = "drawing-missing-declaration";
    public const string DrawingSourceUnknown = "drawing-source-product-unknown";
    public const string DrawingViewMissing = "drawing-primary-view-required";
    public const string DrawingPmiUnknown = "drawing-pmi-reference-unknown";
    public const string DrawingConceptUnsatisfied = "drawing-concept-require-unsatisfied";
    public const string DrawingLayoutImpossible = "drawing-layout-impossible";
    public const string DrawingMetadataUnknown = "drawing-metadata-static-unknown";
    public const string DrawingMetadataRequiredFieldMissing = "drawing-metadata-required-field-missing";
    public const string DrawingRevisionInvalid = "drawing-revision-semver-invalid";
    public const string DrawingDateInvalid = "drawing-date-iso-invalid";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true, PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new JsonStringEnumConverter() }
    };

    public static DrawingCompileResult Compile(string sourcePath, string outputDirectory)
    {
        var fullSourcePath = Path.GetFullPath(sourcePath); var output = Path.GetFullPath(outputDirectory);
        if (!File.Exists(fullSourcePath)) return Failure($"drawing-source-file-missing: {fullSourcePath}");
        Directory.CreateDirectory(output);
        var source = File.ReadAllText(fullSourcePath, Encoding.UTF8).Replace("\r\n", "\n", StringComparison.Ordinal);
        var parse = ParseDrawingLanguage(source);
        if (parse.Diagnostics.Count > 0 || parse.Drawing is null) return new(false, null, parse.Diagnostics);
        var drawingSource = parse.Drawing;
        var metadataResult = ResolveMetadata(source, drawingSource);
        if (metadataResult.Diagnostics.Count > 0) return new(false, null, metadataResult.Diagnostics);
        var metadata = metadataResult.Metadata!;
        var geometrySource = EraseDrawingDeclarations(source, parse.Ranges);

        var sourceWatch = Stopwatch.StartNew();
        var bodies = new List<DrawingProjectionBody>();
        var pmi = new Dictionary<string, PmiPresentation>(StringComparer.Ordinal);
        IReadOnlyList<DrawingTableIr> designTables = [];
        DrawingBomIr? bom = null;
        var bomMilliseconds = 0d;
        string sourceKind;
        var isAssembly = Regex.IsMatch(geometrySource, $@"\bAssembly\s+{Regex.Escape(drawingSource.Source)}\s*\{{", RegexOptions.CultureInvariant);
        if (isAssembly)
        {
            sourceKind = "Assembly";
            var temporary = Path.Combine(Path.GetDirectoryName(fullSourcePath)!, $".{Path.GetFileNameWithoutExtension(fullSourcePath)}.{Guid.NewGuid():N}.drawing-clean.firmament");
            try
            {
                File.WriteAllText(temporary, geometrySource, new UTF8Encoding(false));
                var assembly = new AssemblyM1Pipeline().CompileFile(temporary);
                if (!assembly.IsSuccess || assembly.Ir is null || assembly.Geometry is null)
                    return new(false, null, assembly.Diagnostics.Select(item => $"{item.Code}: {item.Message}").ToArray());
                if (!string.Equals(assembly.Ir.Name, drawingSource.Source, StringComparison.Ordinal)) return Failure($"{DrawingSourceUnknown}: {drawingSource.Source}");
                var instances = assembly.Ir.Instances.ToDictionary(item => item.StableId, StringComparer.Ordinal);
                foreach (var pair in assembly.Geometry.InstanceBodies.OrderBy(pair => instances[pair.Key].Path.ToString(), StringComparer.Ordinal))
                {
                    var instance = instances[pair.Key];
                    bodies.Add(new(instance.Path.ToString(), instance.DefinitionIdentity, pair.Value));
                }
                NormalizeAssemblyPmi(assembly.Ir, pmi);
                if (drawingSource.Bom) { var bomWatch = Stopwatch.StartNew(); bom = NormalizeBom(assembly.Ir); bomWatch.Stop(); bomMilliseconds = bomWatch.Elapsed.TotalMilliseconds; }
            }
            finally { if (File.Exists(temporary)) File.Delete(temporary); }
        }
        else
        {
            sourceKind = "Part";
            var exact = FirmamentBuildAndExport.CompileSource(geometrySource, Path.GetDirectoryName(fullSourcePath));
            if (!exact.IsSuccess) return new(false, null, exact.Diagnostics.Select(item => $"{item.Source}: {item.Message}").ToArray());
            var imported = Step242Importer.ImportBody(exact.Value.StepText);
            if (!imported.IsSuccess) return new(false, null, imported.Diagnostics.Select(item => $"{item.Source}: {item.Message}").ToArray());
            var semantic = FirmamentV2Parser.Parse(geometrySource, Path.GetDirectoryName(fullSourcePath));
            if (!semantic.IsSuccess || semantic.Document is null) return new(false, null, semantic.Diagnostics);
            if (!string.Equals(drawingSource.Source, semantic.Document.ModelName, StringComparison.Ordinal))
                return Failure($"{DrawingSourceUnknown}: '{drawingSource.Source}' does not name authoritative Model '{semantic.Document.ModelName}'.");
            bodies.Add(new(drawingSource.Source, drawingSource.Source, imported.Value));
            foreach (var item in NormalizePmi(semantic.Document)) pmi[item.Key] = item.Value;
            designTables = NormalizeTables(drawingSource, semantic.Document);
        }
        sourceWatch.Stop();

        var unknownPmi = drawingSource.Views.SelectMany(view => view.Pmi).Distinct(StringComparer.Ordinal).Where(reference => !pmi.ContainsKey(reference)).Order(StringComparer.Ordinal).ToArray();
        if (unknownPmi.Length > 0) return Failure($"{DrawingPmiUnknown}: {string.Join(", ", unknownPmi)}");
        var conceptFailures = ValidateConcept(drawingSource, parse.Concept, metadata);
        if (conceptFailures.Count > 0) return new(false, null, conceptFailures);

        var projectionWatch = Stopwatch.StartNew();
        var pages = ProjectAndAllocate(bodies, drawingSource, designTables, bom, metadata);
        projectionWatch.Stop();
        var layoutWatch = Stopwatch.StartNew();
        var layout = DrawingAnnotationLayout.Layout(pages, pmi);
        layoutWatch.Stop();
        if (layout.Diagnostics.Count > 0) return new(false, null, layout.Diagnostics);
        pages = LocateAnnotations(layout.Pages);

        var specialization = StableHash($"{drawingSource.Template}|{drawingSource.Source}|{drawingSource.Name}|{drawingSource.Metadata}")[..16];
        var staticSources = pages.SelectMany(page => page.Tables).Select(table => table.SourceIdentity)
            .Append(metadata.StaticIdentity ?? "legacy-inline").Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray();
        var arguments = new Dictionary<string, string>(StringComparer.Ordinal) { ["Product"] = drawingSource.Source };
        if (drawingSource.Metadata is not null) arguments["Metadata"] = drawingSource.Metadata;
        var provenance = new DrawingProvenanceIr(drawingSource.Source, parse.Concept?.Name, drawingSource.Template, arguments, specialization, staticSources, sourceKind);
        var typography = new DrawingTypographyIr("Inter", "Embedded TrueType / Type0 Identity-H", "Embedded Inter advance widths shared by layout and PDF", new Dictionary<string, double> { ["body"] = 2.8, ["label"] = 3.2, ["title"] = 4.0 });
        var preliminary = new DrawingIr(drawingSource.Name, metadata, provenance, pages, layout.Evidence,
            new(sourceWatch.Elapsed.TotalMilliseconds, projectionWatch.Elapsed.TotalMilliseconds, layoutWatch.Elapsed.TotalMilliseconds, 0, 0, bomMilliseconds), [],
            "Exact B-rep occurrence edges; bounded segment-split occlusion against deterministic face tessellation. Unsupported patches are conservative and explicit.", typography);

        var stem = Sanitize(drawingSource.Name); var irPath = Path.Combine(output, $"{stem}.drawing.json");
        var svgPath = Path.Combine(output, $"{stem}.svg"); var pdfPath = Path.Combine(output, $"{stem}.pdf");
        var validationPath = Path.Combine(output, $"{stem}.validation.json");
        var renderWatch = Stopwatch.StartNew(); File.WriteAllText(svgPath, DrawingSvgRenderer.Render(preliminary), new UTF8Encoding(false)); renderWatch.Stop();
        var pdfWatch = Stopwatch.StartNew(); DrawingVectorPdfWriter.Write(preliminary, pdfPath); pdfWatch.Stop();
        var measured = preliminary.Performance with { RenderMilliseconds = renderWatch.Elapsed.TotalMilliseconds, PdfMilliseconds = pdfWatch.Elapsed.TotalMilliseconds };
        var drawing = preliminary with { Performance = new(0, 0, 0, 0, 0, 0) };
        var json = JsonSerializer.Serialize(drawing, JsonOptions); File.WriteAllText(irPath, json, new UTF8Encoding(false));
        var irHash = HashBytes(Encoding.UTF8.GetBytes(json)); var pdfHash = HashBytes(File.ReadAllBytes(pdfPath));
        var validation = new
        {
            success = true, schema = drawing.SchemaVersion, authoritativeSource = drawing.Provenance.SourceProductIdentity, sourceKind,
            pageCount = drawing.Pages.Count, a4Only = drawing.Pages.All(page => (page.WidthMillimetres, page.HeightMillimetres) is (210, 297) or (297, 210)),
            vectorPdf = true, rasterImages = 0, embeddedFont = "Inter", searchableText = true,
            occurrenceCount = bodies.Count, bomRows = bom?.Items.Count ?? 0,
            visibleSegments = drawing.Pages.SelectMany(page => page.Views).Sum(view => view.VisibilityEvidence?.VisibleSegments ?? 0),
            hiddenSegments = drawing.Pages.SelectMany(page => page.Views).Sum(view => view.VisibilityEvidence?.HiddenSegments ?? 0),
            splitPoints = drawing.Pages.SelectMany(page => page.Views).Sum(view => view.VisibilityEvidence?.SplitPointCount ?? 0),
            drawing.LayoutEvidence.TextModelCollisionsAfter, drawing.LayoutEvidence.TextTextCollisionsAfter, drawing.LayoutEvidence.FailedAnnotationCount,
            drawingIrSha256 = irHash, pdfSha256 = pdfHash, measuredPerformance = measured
        };
        File.WriteAllText(validationPath, JsonSerializer.Serialize(validation, JsonOptions), new UTF8Encoding(false));
        return new(true, new(drawing, irPath, svgPath, pdfPath, validationPath, pdfHash, irHash), []);
    }

    private static IReadOnlyList<DrawingPageIr> ProjectAndAllocate(IReadOnlyList<DrawingProjectionBody> bodies, ParsedDrawing drawing,
        IReadOnlyList<DrawingTableIr> designTables, DrawingBomIr? bom, DrawingMetadataIr metadata)
    {
        var landscape = drawing.Orientation == DrawingPageOrientation.Landscape;
        var width = landscape ? 297d : 210d; var height = landscape ? 210d : 297d;
        var content = new DrawingRect(10, 16, width - 20, height - 40); var scheme = Zones(width, height);
        var viewAreaHeight = content.Height - 22; var columns = Math.Max(1, drawing.Views.Count <= 2 ? drawing.Views.Count : 2);
        var rows = (int)Math.Ceiling(drawing.Views.Count / (double)columns); const double gap = 8;
        var cellWidth = (content.Width - gap * Math.Max(0, columns - 1)) / columns;
        var cellHeight = (viewAreaHeight - gap * Math.Max(0, rows - 1)) / Math.Max(1, rows);
        var views = new List<DrawingViewIr>();
        for (var index = 0; index < drawing.Views.Count; index++)
        {
            var viewport = new DrawingRect(content.X + (index % columns) * (cellWidth + gap), content.Y + (index / columns) * (cellHeight + gap), cellWidth, cellHeight);
            var location = new DrawingLocationIr(1, ZoneAt(scheme, viewport.Center));
            var source = drawing.Views[index];
            views.Add(DrawingProjectionEngine.Project(bodies, source.Name, source.Projection, source.HiddenLines, source.Direction, source.Pmi, viewport, location));
        }
        var notes = LocateNotes(drawing.Notes, 1, scheme, new DrawingRect(content.X, height - 31, Math.Max(20, content.Width - 100), 16));
        var tables = new List<DrawingTableIr>(designTables); if (bom is not null) tables.Add(bom.Table);
        var rowCapacity = Math.Max(1, (int)Math.Floor((content.Height - 25) / 7d) - 1);
        var slices = new List<(DrawingTableIr Table, DrawingBomIr? Bom)>();
        foreach (var table in tables)
        {
            var count = Math.Max(1, (int)Math.Ceiling(table.Rows.Count / (double)rowCapacity));
            for (var slice = 0; slice < count; slice++)
            {
                var sliced = table with { Identity = count == 1 ? table.Identity : $"{table.Identity} ({slice + 1}/{count})", Rows = table.Rows.Skip(slice * rowCapacity).Take(rowCapacity).ToArray() };
                slices.Add((sliced, table.Kind == DrawingTableKind.BillOfMaterials && bom is not null ? bom with { Table = sliced } : null));
            }
        }
        var totalPages = 1 + slices.Count;
        var info = InfoBlock(metadata, drawing, views, 1, width, height, scheme, totalPages);
        var page1 = new DrawingPageIr(1, drawing.Orientation, width, height, content, views, [], [], drawing.Notes, scheme, info, notes);
        if (slices.Count == 0) return [page1];
        var pages = new List<DrawingPageIr> { page1 };
        for (var index = 0; index < slices.Count; index++)
        {
            var pageNumber = index + 2; var slice = slices[index];
            var bounds = new DrawingRect(content.X, content.Y + 10, content.Width, Math.Min(content.Height - 25, 8 + slice.Table.Rows.Count * 7));
            var located = slice.Table with { Bounds = bounds, Location = new(pageNumber, ZoneAt(scheme, bounds.Center)) };
            pages.Add(new(pageNumber, drawing.Orientation, width, height, content, [], [], [located], [], scheme,
                InfoBlock(metadata, drawing, views, pageNumber, width, height, scheme, totalPages), [], slice.Bom is null ? null : slice.Bom with { Table = located }));
        }
        return pages;
    }

    private static DrawingPageZoneSchemeIr Zones(double width, double height)
    {
        // Keep the top zone labels clear of the drawing title and revision header.
        // The lower edge remains at the established 12 mm margin.
        var border = new DrawingRect(7, 15, width - 14, height - 27); var rows = new[] { "A", "B", "C", "D" }; var columns = new[] { "1", "2", "3", "4", "5", "6" };
        var zones = new List<DrawingPageZoneIr>();
        for (var row = 0; row < rows.Length; row++) for (var column = 0; column < columns.Length; column++)
            zones.Add(new(rows[row] + columns[column], new(border.X + column * border.Width / columns.Length, border.Y + row * border.Height / rows.Length, border.Width / columns.Length, border.Height / rows.Length)));
        return new(rows.Length, columns.Length, rows, columns, zones);
    }

    private static string ZoneAt(DrawingPageZoneSchemeIr scheme, DrawingPoint2 point) => scheme.Zones.FirstOrDefault(zone =>
        point.X >= zone.Bounds.X && point.X <= zone.Bounds.Right && point.Y >= zone.Bounds.Y && point.Y <= zone.Bounds.Bottom)?.Address
        ?? scheme.Zones.OrderBy(zone => Math.Abs(zone.Bounds.Center.X - point.X) + Math.Abs(zone.Bounds.Center.Y - point.Y)).First().Address;

    private static DrawingInformationBlockIr InfoBlock(DrawingMetadataIr metadata, ParsedDrawing drawing, IReadOnlyList<DrawingViewIr> views, int page, double width, double height, DrawingPageZoneSchemeIr scheme, int pageCount)
    {
        var bounds = new DrawingRect(width - 122, height - 34, 115, 22);
        var scales = views.Count == 0 ? "-" : views.All(view => Math.Abs(view.Scale - views[0].Scale) < 1e-6) ? $"×{views[0].Scale:0.###}" : "MULTIPLE";
        var fields = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["Company"] = metadata.Company ?? "Aetheris", ["Title"] = metadata.Title.Length > 24 ? (drawing.Bom ? metadata.ProductName + " Assembly" : metadata.Description ?? metadata.ProductName) : metadata.Title, ["Product"] = metadata.ProductName,
            ["Part"] = metadata.PartNumber ?? "-", ["Revision"] = metadata.Revision?.ToString() ?? "-", ["Author"] = metadata.Author ?? "-",
            ["Date"] = metadata.Date ?? "-", ["Scale"] = scales, ["Page"] = $"{page}/{pageCount}"
        };
        return new(bounds, new(page, ZoneAt(scheme, bounds.Center)), fields);
    }

    private static IReadOnlyList<DrawingNoteIr> LocateNotes(IReadOnlyList<string> notes, int page, DrawingPageZoneSchemeIr scheme, DrawingRect area) => notes.Select((text, index) =>
        new DrawingNoteIr($"note:{index + 1}", text, new(area.X, area.Y + index * 4, area.Width, 4), new(page, ZoneAt(scheme, new(area.X + area.Width / 2, area.Y + index * 4 + 2))))).ToArray();

    private static IReadOnlyList<DrawingPageIr> LocateAnnotations(IReadOnlyList<DrawingPageIr> pages) => pages.Select(page => page with
    {
        Annotations = page.Annotations.Select(annotation => annotation with { Location = new(page.PageNumber, ZoneAt(page.ZoneScheme!, annotation.SelectedCandidate.Body.Center)) }).ToArray()
    }).ToArray();

    private static DrawingBomIr NormalizeBom(AssemblyIr assembly)
    {
        var groups = assembly.Instances.Where(instance => instance.Kind == AssemblyInstanceKind.Part)
            .GroupBy(instance => instance.DefinitionIdentity, StringComparer.Ordinal).OrderBy(group => group.Key, StringComparer.Ordinal).ToArray();
        var items = groups.Select((group, index) => new DrawingBomItemIr(index + 1, group.Key, FriendlyDefinition(group.Key), group.Count(), null, null,
            group.Select(instance => instance.Path.ToString()).Order(StringComparer.Ordinal).ToArray())).ToArray();
        var rows = items.Select(item => (IReadOnlyList<string>)[item.Item.ToString(CultureInfo.InvariantCulture), item.DefinitionIdentity, item.Description, item.Quantity.ToString(CultureInfo.InvariantCulture), item.PartNumber ?? "-", item.Revision ?? "-"]).ToArray();
        var table = new DrawingTableIr("BOM", ["ITEM", "DEFINITION", "DESCRIPTION", "QTY", "PART NO.", "REV"], rows, assembly.StableId,
            "AssemblyIR flattened leaf-part occurrences aggregated by definition identity", DrawingTableKind.BillOfMaterials);
        return new("BOM", "Flattened leaf parts; aggregate identical definition identities; deterministic lexical ordering.", items, table);
    }

    private static string FriendlyDefinition(string identity)
    {
        var generic = identity.IndexOf('<'); return generic > 0 ? identity[..generic] : identity;
    }

    private static void NormalizeAssemblyPmi(AssemblyIr assembly, Dictionary<string, PmiPresentation> result)
    {
        foreach (var instance in assembly.Instances)
        {
            void Visit(SemanticValue value, string path)
            {
                var current = string.IsNullOrEmpty(path) ? instance.Path.ToString() : instance.Path + "." + path;
                if (value.TryBinding<TolerancedDimensionBinding>(out var dimension))
                    result[current] = new(current, DrawingAnnotationKind.LinearDimension, FormatEngineeringValue(dimension.Nominal, dimension.Unit,
                        new(FirmamentV2ToleranceKind.Asymmetric, Math.Abs(dimension.UpperTolerance), Math.Abs(dimension.LowerTolerance), dimension.Unit, FirmamentV2PrimitiveType.Length, new(0, 0))), dimension.Nominal, value.StableIdentity, "AssemblyIR SemanticValue");
                foreach (var member in value.ExposedMembers) Visit(member.Value, string.IsNullOrEmpty(path) ? member.Key : path + "." + member.Key);
            }
            Visit(instance.SemanticRoot, string.Empty);
        }
        foreach (var relation in assembly.DimensionalRelations)
            result[relation.StableId] = new(relation.StableId, DrawingAnnotationKind.LinearDimension, $"{relation.Nominal:0.###} {relation.Unit}", relation.Nominal, relation.StableId, "AssemblyIR DimensionalRelation");
        foreach (var stack in assembly.ToleranceStackups)
            result[stack.Name] = new(stack.Name, DrawingAnnotationKind.LinearDimension, $"{stack.Nominal:0.###} {stack.Unit} [{stack.WorstCaseMinimum:0.###}, {stack.WorstCaseMaximum:0.###}]", stack.Nominal, stack.Name, "AssemblyIR ToleranceStackup");
    }

    private static Dictionary<string, PmiPresentation> NormalizePmi(FirmamentV2Document document)
    {
        var result = new Dictionary<string, PmiPresentation>(StringComparer.Ordinal);
        foreach (var record in document.BoundPmi?.Datums ?? []) result[record.Name] = new(record.Name, DrawingAnnotationKind.Datum, record.Name, null, record.Targets.FirstOrDefault() ?? record.Name, "Firmament Semantic PMI");
        foreach (var record in document.BoundPmi?.Dimensions ?? [])
        {
            var nominal = record.DimensionValue?.NumericValue; var value = nominal.HasValue ? FormatEngineeringValue(nominal.Value, record.DimensionValue!.Unit, record.DimensionTolerance) : record.Name;
            result[record.Name] = new(record.Name, record.Kind == FirmamentV2PmiKind.HoleDiameter ? DrawingAnnotationKind.DiameterDimension : DrawingAnnotationKind.LinearDimension,
                record.Kind == FirmamentV2PmiKind.HoleDiameter ? $"Ø{value}" : value, nominal, record.Targets.FirstOrDefault() ?? record.Name, record.ProjectionSource ?? "Firmament Semantic PMI");
        }
        foreach (var record in document.BoundPmi?.Controls ?? []) result[record.Name] = new(record.Name, DrawingAnnotationKind.FeatureControlFrame,
            $"{record.Kind.ToString().ToUpperInvariant()} | {record.ControlTolerance?.NumericValue:0.###} | {string.Join(" | ", record.DatumRefs)}", null, record.Targets.FirstOrDefault() ?? record.Name, "Firmament Semantic PMI");
        return result;
    }

    private static string FormatEngineeringValue(double nominal, string? unit, FirmamentV2Tolerance? tolerance)
    {
        var value = $"{nominal:0.###} {unit ?? "mm"}"; if (tolerance is null) return value;
        return Math.Abs(tolerance.Plus - tolerance.Minus) < 1e-12 ? $"{value} ±{tolerance.Plus:0.###}" : $"{value} +{tolerance.Plus:0.###}/-{tolerance.Minus:0.###}";
    }

    private static IReadOnlyList<DrawingTableIr> NormalizeTables(ParsedDrawing drawing, FirmamentV2Document document)
    {
        if (string.IsNullOrWhiteSpace(drawing.Table)) return [];
        var table = document.StaticAuthoring?.Tables?.FirstOrDefault(candidate => candidate.Name == drawing.Table); if (table is null) return [];
        var columns = table.Columns.Keys.Order(StringComparer.Ordinal).ToArray();
        var rows = Enumerable.Range(0, table.RowCount).Select(row => (IReadOnlyList<string>)columns.Select(column => table.Columns[column][row]).ToArray()).ToArray();
        return [new(table.Name, columns, rows, table.Name, "Firmament Static Table; shared compile-time source")];
    }

    private static IReadOnlyList<string> ValidateConcept(ParsedDrawing drawing, ParsedConcept? concept, DrawingMetadataIr metadata)
    {
        if (concept is null || drawing.Concept != concept.Name) return [];
        var failures = new List<string>();
        foreach (var requirement in concept.Requirements)
        {
            var satisfied = requirement switch { "PrimaryView" => drawing.Views.Any(view => view.Projection == DrawingProjectionKind.Orthographic), "ManufacturingPmi" => drawing.Views.Any(view => view.Pmi.Count > 0),
                "Material" => !string.IsNullOrWhiteSpace(metadata.Material), "DesignTable" => !string.IsNullOrWhiteSpace(drawing.Table), "BOM" => drawing.Bom,
                "RevisionMetadata" => metadata.Revision is not null, _ => false };
            if (!satisfied) failures.Add($"{DrawingConceptUnsatisfied}: {concept.Name}.{requirement}");
        }
        return failures;
    }

    private static MetadataResult ResolveMetadata(string source, ParsedDrawing drawing)
    {
        var diagnostics = new List<string>(); var fields = new Dictionary<string, string>(StringComparer.Ordinal); string? provenance = null;
        if (drawing.Metadata is not null)
        {
            var records = ParseStaticRecords(source); if (!records.TryGetValue(drawing.Metadata, out var record)) return new(null, [$"{DrawingMetadataUnknown}: {drawing.Metadata}"]);
            fields = new(record.Fields, StringComparer.Ordinal); provenance = record.Provenance;
            var missing = new[] { "Company", "Author", "PartNumber", "Revision", "Date", "Description" }.Where(field => !fields.ContainsKey(field)).ToArray();
            if (missing.Length > 0) diagnostics.Add($"{DrawingMetadataRequiredFieldMissing}: {drawing.Metadata}: {string.Join(",", missing)}");
        }
        string? Value(string name, string? fallback = null) => fields.TryGetValue(name, out var value) ? TrimValue(value) : fallback;
        var revisionSource = Value("Revision", drawing.Revision); DrawingSemanticVersionIr? revision = null;
        var revisionMatch = Regex.Match(revisionSource ?? "", @"^(?<major>0|[1-9]\d*)\.(?<minor>0|[1-9]\d*)\.(?<patch>0|[1-9]\d*)$", RegexOptions.CultureInvariant);
        if (!revisionMatch.Success) diagnostics.Add($"{DrawingRevisionInvalid}: {revisionSource ?? "missing"}");
        else revision = new(int.Parse(revisionMatch.Groups["major"].Value, CultureInfo.InvariantCulture), int.Parse(revisionMatch.Groups["minor"].Value, CultureInfo.InvariantCulture), int.Parse(revisionMatch.Groups["patch"].Value, CultureInfo.InvariantCulture));
        var date = Value("Date"); if (date is not null && !DateOnly.TryParseExact(date, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out _)) diagnostics.Add($"{DrawingDateInvalid}: {date}");
        var metadata = new DrawingMetadataIr(Value("Title", drawing.Title ?? drawing.Name)!, drawing.Source, Value("PartNumber", drawing.PartNumber), revision,
            Value("Material", drawing.Material), drawing.Name, drawing.Template, Value("Company"), Value("Author"), date, Value("Description"), drawing.Metadata, provenance);
        return new(metadata, diagnostics);
    }

    private static Dictionary<string, StaticRecord> ParseStaticRecords(string source)
    {
        var result = new Dictionary<string, StaticRecord>(StringComparer.Ordinal);
        foreach (Match header in Regex.Matches(source, @"\bStatic\s+(?<name>[A-Za-z_]\w*)\s*:\s*(?<type>[A-Za-z_]\w*)\s*=\s*\k<type>\s*\{", RegexOptions.CultureInvariant))
        {
            var block = ReadBlock(source, source.IndexOf('{', header.Index)); if (block is null) continue;
            result[header.Groups["name"].Value] = new(ParseFields(block.Value.Body), "StaticRecord:" + header.Groups["name"].Value);
        }
        var pending = Regex.Matches(source, @"\bStatic\s+(?<name>[A-Za-z_]\w*)\s*(?::\s*[A-Za-z_]\w*)?\s*=\s*(?<base>[A-Za-z_]\w*)\s+with\s*\{", RegexOptions.CultureInvariant).Cast<Match>().ToList();
        while (pending.Count > 0)
        {
            var progressed = false;
            foreach (var item in pending.ToArray())
            {
                if (!result.TryGetValue(item.Groups["base"].Value, out var basis)) continue;
                var block = ReadBlock(source, source.IndexOf('{', item.Index)); if (block is null) { pending.Remove(item); continue; }
                var next = new Dictionary<string, string>(basis.Fields, StringComparer.Ordinal); foreach (var field in ParseFields(block.Value.Body)) next[field.Key] = field.Value;
                result[item.Groups["name"].Value] = new(next, basis.Provenance + "; derivedFrom:" + item.Groups["base"].Value + "; overrides:" + string.Join(",", ParseFields(block.Value.Body).Keys.Order(StringComparer.Ordinal)));
                pending.Remove(item); progressed = true;
            }
            if (!progressed) break;
        }
        return result;
    }

    private static Dictionary<string, string> ParseFields(string body) => Regex.Matches(body, "\\b(?<name>[A-Za-z_]\\w*)\\s*:\\s*(?<value>\"[^\"]*\"|\\d+\\.\\d+\\.\\d+|\\d{4}-\\d{2}-\\d{2}|[^;\\n}]+)", RegexOptions.CultureInvariant)
        .Cast<Match>().ToDictionary(match => match.Groups["name"].Value, match => match.Groups["value"].Value.Trim(), StringComparer.Ordinal);

    private static ParseResult ParseDrawingLanguage(string source)
    {
        var diagnostics = new List<string>(); var ranges = new List<(int Start, int Length)>(); ParsedConcept? concept = null;
        var conceptMatch = Regex.Match(source, @"\bConcept\s+Drawing\s+(?<name>[A-Za-z_]\w*)\s*\{", RegexOptions.CultureInvariant);
        if (conceptMatch.Success) { var block = ReadBlock(source, conceptMatch.Index + conceptMatch.Length - 1); if (block is null) diagnostics.Add("drawing-concept-malformed"); else { concept = new(conceptMatch.Groups["name"].Value, Regex.Matches(block.Value.Body, @"\bRequire\s+(?<name>[A-Za-z_]\w*)\s*;?").Select(item => item.Groups["name"].Value).ToArray()); ranges.Add((conceptMatch.Index, block.Value.End - conceptMatch.Index + 1)); } }
        ParsedTemplate? template = null;
        var templateMatch = Regex.Match(source, @"\bTemplate\s*<(?<parameters>[^>]+)>\s*Drawing\s+(?<name>[A-Za-z_]\w*)\s*(?::\s*(?<concept>[A-Za-z_]\w*))?\s*\{", RegexOptions.CultureInvariant);
        if (templateMatch.Success) { var block = ReadBlock(source, templateMatch.Index + templateMatch.Length - 1); if (block is null) diagnostics.Add("drawing-template-malformed"); else { var parameters = Regex.Matches(templateMatch.Groups["parameters"].Value, @"(?<name>[A-Za-z_]\w*)\s*:\s*(?<type>[A-Za-z_]\w*)").Cast<Match>().ToDictionary(item => item.Groups["name"].Value, item => item.Groups["type"].Value, StringComparer.Ordinal); template = new(templateMatch.Groups["name"].Value, parameters, templateMatch.Groups["concept"].Value, block.Value.Body); ranges.Add((templateMatch.Index, block.Value.End - templateMatch.Index + 1)); } }
        ParsedDrawing? drawing = null;
        var application = Regex.Match(source, @"\bDrawing\s+(?<name>[A-Za-z_]\w*)\s*=\s*(?<template>[A-Za-z_]\w*)\s*<(?<arguments>[^>]+)>\s*;?", RegexOptions.CultureInvariant);
        if (application.Success && template is not null)
        {
            var args = Regex.Matches(application.Groups["arguments"].Value, @"(?<name>[A-Za-z_]\w*)\s*:\s*(?<value>[A-Za-z_]\w*)").Cast<Match>().ToDictionary(item => item.Groups["name"].Value, item => item.Groups["value"].Value, StringComparer.Ordinal);
            if (application.Groups["template"].Value != template.Name) diagnostics.Add("drawing-template-unknown");
            else { var body = template.Body; foreach (var parameter in template.Parameters.Keys) if (args.TryGetValue(parameter, out var value)) body = Regex.Replace(body, $@"\b{Regex.Escape(parameter)}\b", value); drawing = ParseDrawingBody(application.Groups["name"].Value, template.Name, template.Concept, args.GetValueOrDefault("Product") ?? args.GetValueOrDefault("Item"), args.GetValueOrDefault("Metadata"), body, diagnostics); }
            ranges.Add((application.Index, application.Length));
        }
        else
        {
            var literal = Regex.Match(source, @"\bDrawing\s+(?<name>[A-Za-z_]\w*)\s*(?::\s*(?<concept>[A-Za-z_]\w*))?\s*\{", RegexOptions.CultureInvariant);
            if (literal.Success) { var block = ReadBlock(source, literal.Index + literal.Length - 1); if (block is not null) { drawing = ParseDrawingBody(literal.Groups["name"].Value, "Literal", literal.Groups["concept"].Value, null, null, block.Value.Body, diagnostics); ranges.Add((literal.Index, block.Value.End - literal.Index + 1)); } }
        }
        if (drawing is null && diagnostics.Count == 0) diagnostics.Add(DrawingMissing); return new(drawing, concept, ranges, diagnostics);
    }

    private static ParsedDrawing ParseDrawingBody(string name, string template, string concept, string? product, string? metadata, string body, List<string> diagnostics)
    {
        string? Field(string field) => Regex.Match(body, $@"\b{Regex.Escape(field)}\s*:\s*(?<value>[^;\n}}]+)") is { Success: true } match ? TrimValue(match.Groups["value"].Value) : null;
        var source = product ?? Field("Source"); if (string.IsNullOrWhiteSpace(source)) diagnostics.Add("drawing-source-required");
        var views = new List<ParsedView>();
        foreach (Match match in Regex.Matches(body, @"\bView\s+(?<name>[A-Za-z_]\w*)\s*\{"))
        {
            var block = ReadBlock(body, match.Index + match.Length - 1); if (block is null) continue;
            var directionMatch = Regex.Match(block.Value.Body, @"\bDirection\s*:\s*(?<direction>\[[^\]]+\]|[+-][XYZ])"); if (!directionMatch.Success) { diagnostics.Add("drawing-view-direction-required:" + match.Groups["name"].Value); continue; }
            var direction = ParseDirection(directionMatch.Groups["direction"].Value); var projection = Regex.IsMatch(block.Value.Body, @"\bProjection\s*:\s*Isometric") || direction.Count(value => Math.Abs(value) > 1e-12) > 1 ? DrawingProjectionKind.Isometric : DrawingProjectionKind.Orthographic;
            var hidden = Regex.IsMatch(block.Value.Body, @"\bHiddenLines\s*:\s*VisibleAndHidden") ? DrawingHiddenLinePolicy.VisibleAndHidden : DrawingHiddenLinePolicy.VisibleOnly;
            var assignedMatch = Regex.Match(block.Value.Body, @"\bPMI\s*:\s*\[(?<items>[^\]]*)\]", RegexOptions.IgnoreCase); var assigned = assignedMatch.Success ? assignedMatch.Groups["items"].Value.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries) : [];
            views.Add(new(match.Groups["name"].Value, direction, projection, hidden, assigned));
        }
        if (views.Count == 0) diagnostics.Add(DrawingViewMissing);
        var notesMatch = Regex.Match(body, @"\bNotes\s*:\s*\[(?<items>[^\]]*)\]"); var notes = notesMatch.Success ? Regex.Matches(notesMatch.Groups["items"].Value, "\"(?<text>[^\"]*)\"").Select(item => item.Groups["text"].Value).ToArray() : [];
        return new(name, template, concept, source ?? "", Field("Orientation") == "Portrait" ? DrawingPageOrientation.Portrait : DrawingPageOrientation.Landscape, views,
            Field("Title"), Field("PartNumber"), Field("Revision"), Field("Material"), Field("Table"), notes, metadata ?? Field("Metadata"), string.Equals(Field("BOM"), "true", StringComparison.OrdinalIgnoreCase));
    }

    private static IReadOnlyList<double> ParseDirection(string source) => source.Trim() switch { "+X" => [1, 0, 0], "-X" => [-1, 0, 0], "+Y" => [0, 1, 0], "-Y" => [0, -1, 0], "+Z" => [0, 0, 1], "-Z" => [0, 0, -1], _ => source.Trim().Trim('[', ']').Split(',', StringSplitOptions.TrimEntries).Select(value => double.Parse(value, CultureInfo.InvariantCulture)).ToArray() };
    private static (string Body, int End)? ReadBlock(string source, int open) { if (open < 0 || open >= source.Length || source[open] != '{') return null; var depth = 0; var quoted = false; for (var index = open; index < source.Length; index++) { if (source[index] == '"' && (index == 0 || source[index - 1] != '\\')) quoted = !quoted; if (quoted) continue; if (source[index] == '{') depth++; else if (source[index] == '}' && --depth == 0) return (source[(open + 1)..index], index); } return null; }
    private static string EraseDrawingDeclarations(string source, IReadOnlyList<(int Start, int Length)> ranges) { var chars = source.ToCharArray(); foreach (var range in ranges) for (var index = range.Start; index < range.Start + range.Length; index++) if (chars[index] != '\n') chars[index] = ' '; return new(chars); }
    private static string TrimValue(string value) => value.Trim().Trim('"');
    private static string StableHash(string value) => HashBytes(Encoding.UTF8.GetBytes(value));
    private static string HashBytes(byte[] bytes) => Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
    private static string Sanitize(string value) => Regex.Replace(value, "[^A-Za-z0-9._-]", "-");
    private static DrawingCompileResult Failure(string diagnostic) => new(false, null, [diagnostic]);

    private sealed record StaticRecord(IReadOnlyDictionary<string, string> Fields, string Provenance);
    private sealed record MetadataResult(DrawingMetadataIr? Metadata, IReadOnlyList<string> Diagnostics);
    private sealed record ParsedConcept(string Name, IReadOnlyList<string> Requirements);
    private sealed record ParsedTemplate(string Name, IReadOnlyDictionary<string, string> Parameters, string Concept, string Body);
    private sealed record ParsedView(string Name, IReadOnlyList<double> Direction, DrawingProjectionKind Projection, DrawingHiddenLinePolicy HiddenLines, IReadOnlyList<string> Pmi);
    private sealed record ParsedDrawing(string Name, string Template, string Concept, string Source, DrawingPageOrientation Orientation, IReadOnlyList<ParsedView> Views,
        string? Title, string? PartNumber, string? Revision, string? Material, string? Table, IReadOnlyList<string> Notes, string? Metadata, bool Bom);
    private sealed record ParseResult(ParsedDrawing? Drawing, ParsedConcept? Concept, IReadOnlyList<(int Start, int Length)> Ranges, IReadOnlyList<string> Diagnostics);
    internal sealed record PmiPresentation(string Identity, DrawingAnnotationKind Kind, string Display, double? Nominal, string Target, string Provenance);
}
