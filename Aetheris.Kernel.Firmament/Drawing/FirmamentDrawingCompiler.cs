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
using Aetheris.Kernel.Firmament.FirmamentV2.Syntax;
using Aetheris.Collaboration;

namespace Aetheris.Kernel.Firmament.Drawing;

/// <summary>Drawing M0B compiler. Part and Assembly products share one DrawingIR and sibling
/// SVG/React/PDF presentation contract. Assembly occurrences remain independently projected.</summary>
public static partial class FirmamentDrawingCompiler
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

    public static bool IsDrawingSource(string source)=>source.Contains("Drawing",StringComparison.Ordinal)
        && FirmamentSyntaxTraversal.Declarations(FirmamentSyntaxParser.Parse(source).Document).Any(d=>d.Type.Name.Text=="Drawing" && d.Modifiers.Count==0);

    public static DrawingCompileResult Compile(string sourcePath, string outputDirectory)
    {
        var frontend=new List<FirmamentV2ValidationDiagnostic>();
        return CompileCore(sourcePath,outputDirectory,frontend) with {FrontendDiagnostics=frontend};
    }
    private static DrawingCompileResult CompileCore(string sourcePath,string outputDirectory,List<FirmamentV2ValidationDiagnostic> frontend)
    {
        var fullSourcePath = Path.GetFullPath(sourcePath); var output = Path.GetFullPath(outputDirectory);
        if (!File.Exists(fullSourcePath)) return Failure($"drawing-source-file-missing: {fullSourcePath}");
        Directory.CreateDirectory(output);
        var source = File.ReadAllText(fullSourcePath, Encoding.UTF8);
        var reviewScan = FirmamentReviewCompiler.Compile(source, fullSourcePath);
        var drawingSourceText = FirmamentReviewCompiler.EraseDeclarations(source, reviewScan.SourceRanges);
        var dataBinding=new FirmamentDataBinding(drawingSourceText);
        if(dataBinding.Diagnostics.Count>0)return new(false,null,dataBinding.Diagnostics.Select(d=>d.Code+": "+d.Message).ToArray());
        drawingSourceText=dataBinding.Source;
        var parse = ParseDrawingLanguage(drawingSourceText,fullSourcePath,frontend);
        for(var i=0;i<frontend.Count;i++)if(frontend[i].SourceSpan is {} span)frontend[i]=frontend[i] with {SourceSpan=dataBinding.OriginalSpan(span)};
        if (parse.Diagnostics.Count > 0 || parse.Drawing is null) return new(false, null, parse.Diagnostics);
        var drawingSource = parse.Drawing;
        var metadataResult = ResolveMetadata(drawingSourceText, drawingSource);
        if (metadataResult.Diagnostics.Count > 0) return new(false, null, metadataResult.Diagnostics);
        var metadata = metadataResult.Metadata!;
        var geometrySource = EraseDrawingDeclarations(drawingSourceText, parse.Ranges);

        var sourceWatch = Stopwatch.StartNew();
        var bodies = new List<DrawingProjectionBody>();
        var pmi = new Dictionary<string, PmiPresentation>(StringComparer.Ordinal);
        IReadOnlyList<DrawingTableIr> designTables = [];
        DrawingBomIr? bom = null;
        var bomMilliseconds = 0d;
        string sourceKind;
        var isAssembly = FirmamentSyntaxParser.Parse(geometrySource).Document.Statements.OfType<DeclarationSyntax>().Any(d=>d.Type.Name.Text=="Assembly" && d.Name?.Text==drawingSource.Source);
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
        var reviewTargets = pmi.ToDictionary(pair => pair.Key, pair => ((string?)pair.Value.Display, (IReadOnlyList<string>)["PMI", "Dimensional"]), StringComparer.Ordinal);
        var reviewResult = FirmamentReviewCompiler.Compile(source, fullSourcePath, reviewTargets);
        frontend.AddRange(reviewResult.SourceDiagnostics.Select(d=>new FirmamentV2ValidationDiagnostic(d.Code,"error",d.Message){SourceFile=fullSourcePath,SourceSpan=new(d.Span.Start,d.Span.Length),Stage="Review binding"}));
        if (!reviewResult.IsSuccess) return new(false, null, reviewResult.Diagnostics);
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
            "Exact B-rep occurrence edges; bounded segment-split occlusion against deterministic face tessellation. Unsupported patches are conservative and explicit.", typography, reviewResult.Review);

        var stem = Sanitize(drawingSource.Name); var irPath = Path.Combine(output, $"{stem}.drawing.json");
        var svgPath = Path.Combine(output, $"{stem}.svg"); var pdfPath = Path.Combine(output, $"{stem}.pdf");
        var validationPath = Path.Combine(output, $"{stem}.validation.json");
        var pptxPath = Path.Combine(output, $"{stem}.pptx");
        var reviewPptxPath = reviewResult.Review!.Threads.Count == 0 ? null : Path.Combine(output, $"{stem.Replace("Production", "Review", StringComparison.Ordinal)}.pptx");
        var dfmPptxPath = reviewResult.Review.Threads.Count == 0 ? null : Path.Combine(output, $"{stem.Replace("Production", "DfmReview", StringComparison.Ordinal)}.pptx");
        var reviewIrPath = reviewResult.Review.Threads.Count == 0 ? null : Path.Combine(output, $"{stem}.review.json");
        var renderWatch = Stopwatch.StartNew(); File.WriteAllText(svgPath, DrawingSvgRenderer.Render(preliminary), new UTF8Encoding(false)); renderWatch.Stop();
        var pdfWatch = Stopwatch.StartNew(); DrawingVectorPdfWriter.Write(preliminary, pdfPath); pdfWatch.Stop();
        var measured = preliminary.Performance with { RenderMilliseconds = renderWatch.Elapsed.TotalMilliseconds, PdfMilliseconds = pdfWatch.Elapsed.TotalMilliseconds };
        var drawing = preliminary with { Performance = new(0, 0, 0, 0, 0, 0) };
        var json = JsonSerializer.Serialize(drawing, JsonOptions); File.WriteAllText(irPath, json, new UTF8Encoding(false));
        var pptxWatch = Stopwatch.StartNew(); DrawingPptxWriter.WriteDrawing(drawing, pptxPath, includeReviews: false); pptxWatch.Stop();
        var reviewPptxMilliseconds = 0d; var dfmDeckMilliseconds = 0d;
        if (reviewPptxPath is not null) { var watch = Stopwatch.StartNew(); DrawingPptxWriter.WriteDrawing(drawing, reviewPptxPath, includeReviews: true); watch.Stop(); reviewPptxMilliseconds = watch.Elapsed.TotalMilliseconds; }
        if (dfmPptxPath is not null) { var watch = Stopwatch.StartNew(); DrawingPptxWriter.WriteDfmDeck(drawing, dfmPptxPath); watch.Stop(); dfmDeckMilliseconds = watch.Elapsed.TotalMilliseconds; }
        if (reviewIrPath is not null) File.WriteAllText(reviewIrPath, JsonSerializer.Serialize(reviewResult.Review, JsonOptions), new UTF8Encoding(false));
        var irHash = HashBytes(Encoding.UTF8.GetBytes(json)); var pdfHash = HashBytes(File.ReadAllBytes(pdfPath));
        var pptxHash = HashBytes(File.ReadAllBytes(pptxPath));
        var validation = new
        {
            success = true, schema = drawing.SchemaVersion, authoritativeSource = drawing.Provenance.SourceProductIdentity, sourceKind,
            pageCount = drawing.Pages.Count, a4Only = drawing.Pages.All(page => (page.WidthMillimetres, page.HeightMillimetres) is (210, 297) or (297, 210)),
            vectorPdf = true, rasterImages = 0, embeddedFont = "Inter", searchableText = true,
            pptx = DrawingPptxWriter.Inspect(pptxPath), reviewPptx = reviewPptxPath is null ? null : DrawingPptxWriter.Inspect(reviewPptxPath),
            occurrenceCount = bodies.Count, bomRows = bom?.Items.Count ?? 0,
            visibleSegments = drawing.Pages.SelectMany(page => page.Views).Sum(view => view.VisibilityEvidence?.VisibleSegments ?? 0),
            hiddenSegments = drawing.Pages.SelectMany(page => page.Views).Sum(view => view.VisibilityEvidence?.HiddenSegments ?? 0),
            splitPoints = drawing.Pages.SelectMany(page => page.Views).Sum(view => view.VisibilityEvidence?.SplitPointCount ?? 0),
            drawing.LayoutEvidence.TextModelCollisionsAfter, drawing.LayoutEvidence.TextTextCollisionsAfter, drawing.LayoutEvidence.FailedAnnotationCount,
            drawingIrSha256 = irHash, pdfSha256 = pdfHash, pptxSha256 = pptxHash, measuredPerformance = measured,
            pptxPerformance = new { drawingMilliseconds = pptxWatch.Elapsed.TotalMilliseconds, reviewProjectionMilliseconds = reviewPptxMilliseconds, dfmDeckMilliseconds }
        };
        File.WriteAllText(validationPath, JsonSerializer.Serialize(validation, JsonOptions), new UTF8Encoding(false));
        return new(true, new(drawing, irPath, svgPath, pdfPath, validationPath, pptxPath, reviewPptxPath, dfmPptxPath, reviewIrPath, pdfHash, irHash, pptxHash), []);
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
            var records = ParseStaticRecords(source,diagnostics); if(diagnostics.Count>0)return new(null,diagnostics); if (!records.TryGetValue(drawing.Metadata, out var record)) return new(null, [$"{DrawingMetadataUnknown}: {drawing.Metadata}"]);
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

    private static string EraseDrawingDeclarations(string source, IReadOnlyList<(int Start, int Length)> ranges) { var chars = source.ToCharArray(); foreach (var range in ranges) for (var index = range.Start; index < range.Start + range.Length; index++) if (chars[index] != '\n') chars[index] = ' '; return new(chars); }
    private static string TrimValue(string value) => value.Trim().Trim('"');
    private static string StableHash(string value) => HashBytes(Encoding.UTF8.GetBytes(value));
    private static string HashBytes(byte[] bytes) => Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
    private static string Sanitize(string value) => Regex.Replace(value, "[^A-Za-z0-9._-]", "-");
    private static DrawingCompileResult Failure(string diagnostic) => new(false, null, [diagnostic]);

    private sealed record StaticRecord(IReadOnlyDictionary<string, string> Fields, string Provenance);
    private sealed record MetadataResult(DrawingMetadataIr? Metadata, IReadOnlyList<string> Diagnostics);
    private sealed record ParsedConcept(string Name, IReadOnlyList<string> Requirements);
    private sealed record ParsedView(string Name, IReadOnlyList<double> Direction, DrawingProjectionKind Projection, DrawingHiddenLinePolicy HiddenLines, IReadOnlyList<string> Pmi);
    private sealed record ParsedDrawing(string Name, string Template, string Concept, string Source, DrawingPageOrientation Orientation, IReadOnlyList<ParsedView> Views,
        string? Title, string? PartNumber, string? Revision, string? Material, string? Table, IReadOnlyList<string> Notes, string? Metadata, bool Bom);
    private sealed record ParseResult(ParsedDrawing? Drawing, ParsedConcept? Concept, IReadOnlyList<(int Start, int Length)> Ranges, IReadOnlyList<string> Diagnostics);
    internal sealed record PmiPresentation(string Identity, DrawingAnnotationKind Kind, string Display, double? Nominal, string Target, string Provenance);
}
