using System.Globalization;
using System.IO.Compression;
using System.Security;
using System.Text;
using System.Text.RegularExpressions;
using Aetheris.Collaboration;

namespace Aetheris.Kernel.Firmament.Drawing;

/// <summary>Deterministic, offline OPC/Open XML lowering from authoritative DrawingIR to native PowerPoint objects.</summary>
public static class DrawingPptxWriter
{
    private const long EmuPerMillimetre = 36_000;
    private static readonly DateTimeOffset PackageTime = new(2000, 1, 1, 0, 0, 0, TimeSpan.Zero);

    public static void WriteDrawing(DrawingIr drawing, string path, bool includeReviews)
    {
        var pages = drawing.Pages.Select((page, index) => DrawingSlide(drawing, page, index, includeReviews)).ToArray();
        WritePackage(path, pages, drawing.Pages[0].WidthMillimetres, drawing.Pages[0].HeightMillimetres, drawing.Metadata.Title);
    }

    public static void WriteDfmDeck(DrawingIr drawing, string path)
    {
        const double width = 338.6667, height = 190.5;
        var entries = (drawing.Reviews?.Threads ?? []).SelectMany(thread => thread.Entries
            .Where(entry => entry.Kind is ReviewEntryKind.Issue or ReviewEntryKind.Proposal)
            .Select(entry => (Thread: thread, Entry: entry))).ToArray();
        var slides = entries.Length == 0
            ? [DfmSlide(drawing, null, null, 0, width, height)]
            : entries.Select((item, index) => DfmSlide(drawing, item.Thread, item.Entry, index, width, height)).ToArray();
        WritePackage(path, slides, width, height, $"{drawing.Metadata.Title} — DFM Review");
    }

    public static object Inspect(string path)
    {
        using var archive = ZipFile.OpenRead(path);
        var slides = archive.Entries.Where(entry => Regex.IsMatch(entry.FullName, @"^ppt/slides/slide\d+\.xml$", RegexOptions.CultureInvariant)).OrderBy(entry => entry.FullName, StringComparer.Ordinal).ToArray();
        var xml = string.Join("\n", slides.Select(Read));
        var presentation = Read(archive.GetEntry("ppt/presentation.xml")!);
        var size = Regex.Match(presentation, "<p:sldSz cx=\"(\\d+)\" cy=\"(\\d+)\"");
        return new
        {
            slideCount = slides.Length,
            widthEmu = long.Parse(size.Groups[1].Value, CultureInfo.InvariantCulture),
            heightEmu = long.Parse(size.Groups[2].Value, CultureInfo.InvariantCulture),
            nativeShapes = Regex.Matches(xml, "<p:sp>").Count,
            nativeGroups = Regex.Matches(xml, "<p:grpSp>").Count,
            nativeTables = Regex.Matches(xml, "<a:tbl>").Count,
            editableTextRuns = Regex.Matches(xml, "<a:t>").Count,
            rasterImages = Regex.Matches(xml, "<p:pic>").Count,
            semanticNames = Regex.Matches(xml, "name=\"(?:View|PMI|Table|Metadata|Zone|Review)\\.").Count
        };
    }

    private static string DrawingSlide(DrawingIr drawing, DrawingPageIr page, int pageIndex, bool includeReviews)
    {
        var slide = new SlideBuilder(page.WidthMillimetres, page.HeightMillimetres);
        slide.Group($"Page.{page.PageNumber}.Zones", group =>
        {
            group.Line("Zone.Border.Top", 5, 5, page.WidthMillimetres - 5, 5);
            group.Line("Zone.Border.Right", page.WidthMillimetres - 5, 5, page.WidthMillimetres - 5, page.HeightMillimetres - 5);
            group.Line("Zone.Border.Bottom", page.WidthMillimetres - 5, page.HeightMillimetres - 5, 5, page.HeightMillimetres - 5);
            group.Line("Zone.Border.Left", 5, page.HeightMillimetres - 5, 5, 5);
            if (page.ZoneScheme is not null)
            {
                foreach (var label in page.ZoneScheme.ColumnLabels.Select((value, index) => (value, index)))
                {
                    var x = 5 + (page.WidthMillimetres - 10) * (label.index + .5) / page.ZoneScheme.Columns;
                    group.Text($"Zone.Column.{label.value}.Top", label.value, x - 2, 1, 4, 3, 2.2, centered: true);
                    group.Text($"Zone.Column.{label.value}.Bottom", label.value, x - 2, page.HeightMillimetres - 4, 4, 3, 2.2, centered: true);
                }
                foreach (var label in page.ZoneScheme.RowLabels.Select((value, index) => (value, index)))
                {
                    var y = 5 + (page.HeightMillimetres - 10) * (label.index + .5) / page.ZoneScheme.Rows;
                    group.Text($"Zone.Row.{label.value}.Left", label.value, 1, y - 1.5, 4, 3, 2.2, centered: true);
                    group.Text($"Zone.Row.{label.value}.Right", label.value, page.WidthMillimetres - 5, y - 1.5, 4, 3, 2.2, centered: true);
                }
            }
        });
        foreach (var view in page.Views)
            slide.Group($"View.{view.Identity}.Geometry", group =>
            {
                foreach (var primitive in view.Primitives)
                    for (var index = 1; index < primitive.Points.Count; index++)
                        group.Line($"View.{view.Identity}.Edge.{Safe(primitive.StableId)}.{index}", primitive.Points[index - 1], primitive.Points[index],
                            dashed: primitive.Kind == DrawingPrimitiveKind.Hidden);
            });
        foreach (var annotation in page.Annotations)
            slide.Group($"PMI.{Safe(annotation.Identity)}", group =>
            {
                var leader = annotation.SelectedCandidate.Leader;
                for (var index = 1; index < leader.Count; index++) group.Line($"PMI.{Safe(annotation.Identity)}.Leader.{index}", leader[index - 1], leader[index], arrowEnd: index == leader.Count - 1);
                var body = annotation.SelectedCandidate.Body;
                group.Text($"PMI.{Safe(annotation.Identity)}.Text", annotation.EngineeringDisplay, body.X, body.Y, body.Width, body.Height, 2.8, centered: true, border: annotation.Kind is DrawingAnnotationKind.Datum or DrawingAnnotationKind.FeatureControlFrame);
            });
        foreach (var table in page.Tables) slide.Table($"Table.{(table.Kind == DrawingTableKind.BillOfMaterials ? "BOM" : "Design")}.{Safe(table.Identity)}", table);
        if (page.InformationBlock is not null)
            slide.Group("Metadata.DrawingInfo", group =>
            {
                var block = page.InformationBlock; group.Rect("Metadata.DrawingInfo.Border", block.Bounds.X, block.Bounds.Y, block.Bounds.Width, block.Bounds.Height);
                var fields = block.Fields.ToArray(); var rowHeight = block.Bounds.Height / Math.Max(1, fields.Length);
                for (var index = 0; index < fields.Length; index++)
                    group.Text($"Metadata.DrawingInfo.{Safe(fields[index].Key)}", $"{fields[index].Key}: {fields[index].Value}", block.Bounds.X + 1, block.Bounds.Y + index * rowHeight, block.Bounds.Width - 2, rowHeight, 2.2);
            });
        foreach (var note in page.LocatedNotes ?? []) slide.Text($"Note.{Safe(note.Identity)}", note.Text, note.Bounds.X, note.Bounds.Y, note.Bounds.Width, note.Bounds.Height, 2.4);
        if (includeReviews) AddReviewOverlays(slide, drawing, page, pageIndex);
        return slide.Build();
    }

    private static void AddReviewOverlays(SlideBuilder slide, DrawingIr drawing, DrawingPageIr page, int pageIndex)
    {
        var reviews = drawing.Reviews?.Threads.Where(thread => thread.Status == ReviewStatus.Open || thread.Entries.Any(entry => entry.Kind is ReviewEntryKind.Issue or ReviewEntryKind.Proposal)).ToArray() ?? [];
        for (var index = 0; index < reviews.Length; index++)
        {
            var thread = reviews[index];
            var annotation = page.Annotations.FirstOrDefault(item => string.Equals(item.SemanticReference, thread.Target.SemanticReference, StringComparison.Ordinal));
            if (annotation is null) continue;
            var anchor = annotation.ProjectedAnchor; var width = Math.Min(62, page.WidthMillimetres * .28); var height = 23d;
            var x = Math.Clamp(anchor.X + 12, page.ContentRect.X, page.ContentRect.Right - width);
            var y = Math.Clamp(anchor.Y - height / 2 + index * 8, page.ContentRect.Y, page.ContentRect.Bottom - height);
            var summary = thread.Entries.FirstOrDefault(entry => entry.Kind is ReviewEntryKind.Issue or ReviewEntryKind.Proposal) ?? thread.Entries.FirstOrDefault();
            slide.Group($"Review.{Safe(thread.Id)}.Callout", group =>
            {
                group.Ellipse($"Review.{Safe(thread.Id)}.Highlight", anchor.X - 4, anchor.Y - 4, 8, 8, "FF8A00", 2);
                group.Line($"Review.{Safe(thread.Id)}.Leader", anchor.X + 4, anchor.Y, x, y + height / 2, "FF8A00", arrowStart: true);
                var proposed = summary?.Proposal is null ? "" : $"\nCurrent: {summary.Proposal.CurrentValue}\nProposed: {summary.Proposal.ProposedValue}";
                group.Text($"Review.{Safe(thread.Id)}.Text", $"{thread.Id} · {thread.Status}\n{summary?.Kind}: {summary?.Text}{proposed}\n{summary?.Author.Name} · {summary?.AuthoredDate:yyyy-MM-dd}", x, y, width, height, 2.4, fill: "FFF3E0", border: true, borderColor: "FF8A00");
            });
        }
    }

    private static string DfmSlide(DrawingIr drawing, ReviewThreadIr? thread, ReviewEntryIr? entry, int index, double width, double height)
    {
        var slide = new SlideBuilder(width, height);
        slide.Rect("DFM.Background", 0, 0, width, height, fill: "F7F8FA", line: false);
        slide.Text("DFM.Title", thread is null ? "DFM REVIEW" : $"{thread.Id}  |  {entry!.Kind}", 12, 8, width - 24, 12, 7, bold: true);
        var page = drawing.Pages[0]; const double gx = 12, gy = 28, gw = 205, gh = 145;
        slide.Rect("DFM.View.Frame", gx, gy, gw, gh, fill: "FFFFFF");
        var scale = Math.Min((gw - 10) / page.WidthMillimetres, (gh - 10) / page.HeightMillimetres);
        var ox = gx + (gw - page.WidthMillimetres * scale) / 2; var oy = gy + (gh - page.HeightMillimetres * scale) / 2;
        foreach (var view in page.Views)
            slide.Group($"View.{view.Identity}.Geometry", group =>
            {
                foreach (var primitive in view.Primitives)
                    for (var p = 1; p < primitive.Points.Count; p++)
                        group.Line($"View.{view.Identity}.Edge.{Safe(primitive.StableId)}.{p}", Map(primitive.Points[p - 1]), Map(primitive.Points[p]), dashed: primitive.Kind == DrawingPrimitiveKind.Hidden);
            });
        DrawingPoint2 Map(DrawingPoint2 point) => new(ox + point.X * scale, oy + point.Y * scale);
        if (thread is not null)
        {
            var annotation = page.Annotations.FirstOrDefault(item => item.SemanticReference == thread.Target.SemanticReference);
            if (annotation is not null)
            {
                var anchor = Map(annotation.ProjectedAnchor);
                slide.Ellipse($"Review.{Safe(thread.Id)}.Highlight", anchor.X - 8, anchor.Y - 8, 16, 16, "FF5A36", 3);
                slide.Line($"Review.{Safe(thread.Id)}.BigArrow", 238, 80, anchor.X + 8, anchor.Y, "FF5A36", 3, arrowEnd: true);
            }
            var proposal = entry!.Proposal;
            var details = $"STATUS  {thread.Status}\n\n{entry.Text}\n\nTARGET\n{thread.Target.SemanticReference}\n\nCURRENT\n{proposal?.CurrentValue ?? thread.Target.CurrentEngineeringValue ?? "—"}\n\nPROPOSED\n{proposal?.ProposedValue ?? "—"}\n\n{entry.Author.Name}{(entry.Author.Organization is null ? "" : " · " + entry.Author.Organization)}\n{entry.AuthoredDate:yyyy-MM-dd}";
            slide.Text($"Review.{Safe(thread.Id)}.Summary", details, 228, 30, 98, 140, 4.0, fill: "FFFFFF", border: true, borderColor: "D9DDE3");
        }
        else slide.Text("DFM.Empty", "No Issue or Proposal entries were supplied.", 228, 40, 96, 30, 4);
        slide.Text("DFM.Authority", "DOWNSTREAM REVIEW ARTIFACT — FIRMAMENT REMAINS ENGINEERING AUTHORITY", 12, 178, width - 24, 5, 2.6, bold: true);
        return slide.Build();
    }

    private static void WritePackage(string path, IReadOnlyList<string> slides, double widthMm, double heightMm, string title)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);
        if (File.Exists(path)) File.Delete(path);
        using var archive = ZipFile.Open(path, ZipArchiveMode.Create);
        Add(archive, "[Content_Types].xml", ContentTypes(slides.Count));
        Add(archive, "_rels/.rels", RootRelationships());
        Add(archive, "docProps/app.xml", AppProperties(slides.Count));
        Add(archive, "docProps/core.xml", CoreProperties(title));
        Add(archive, "ppt/presentation.xml", Presentation(slides.Count, Mm(widthMm), Mm(heightMm)));
        Add(archive, "ppt/_rels/presentation.xml.rels", PresentationRelationships(slides.Count));
        Add(archive, "ppt/slideMasters/slideMaster1.xml", SlideMaster());
        Add(archive, "ppt/slideMasters/_rels/slideMaster1.xml.rels", MasterRelationships());
        Add(archive, "ppt/slideLayouts/slideLayout1.xml", SlideLayout());
        Add(archive, "ppt/slideLayouts/_rels/slideLayout1.xml.rels", LayoutRelationships());
        Add(archive, "ppt/theme/theme1.xml", Theme());
        for (var index = 0; index < slides.Count; index++)
        {
            Add(archive, $"ppt/slides/slide{index + 1}.xml", slides[index]);
            Add(archive, $"ppt/slides/_rels/slide{index + 1}.xml.rels", SlideRelationships());
        }
    }

    private static void Add(ZipArchive archive, string name, string content)
    {
        var entry = archive.CreateEntry(name, CompressionLevel.Optimal); entry.LastWriteTime = PackageTime;
        using var stream = entry.Open(); var bytes = new UTF8Encoding(false).GetBytes(content); stream.Write(bytes);
    }
    private static string Read(ZipArchiveEntry entry) { using var reader = new StreamReader(entry.Open(), Encoding.UTF8); return reader.ReadToEnd(); }
    private static long Mm(double value) => checked((long)Math.Round(value * EmuPerMillimetre, MidpointRounding.AwayFromZero));
    private static string X(string value) => SecurityElement.Escape(value) ?? string.Empty;
    private static string Safe(string value) => Regex.Replace(value, @"[^A-Za-z0-9_.-]", "_");

    private sealed class SlideBuilder
    {
        private readonly double width, height; private readonly StringBuilder shapes = new(); private uint id = 2;
        public SlideBuilder(double width, double height) { this.width = width; this.height = height; }
        public void Group(string name, Action<SlideBuilder> content)
        {
            var child = new SlideBuilder(width, height) { id = id };
            content(child); id = child.id;
            shapes.Append($"<p:grpSp><p:nvGrpSpPr><p:cNvPr id=\"{id++}\" name=\"{X(name)}\"/><p:cNvGrpSpPr/><p:nvPr/></p:nvGrpSpPr><p:grpSpPr><a:xfrm><a:off x=\"0\" y=\"0\"/><a:ext cx=\"{Mm(width)}\" cy=\"{Mm(height)}\"/><a:chOff x=\"0\" y=\"0\"/><a:chExt cx=\"{Mm(width)}\" cy=\"{Mm(height)}\"/></a:xfrm></p:grpSpPr>{child.shapes}</p:grpSp>");
        }
        public void Line(string name, DrawingPoint2 a, DrawingPoint2 b, bool dashed = false, bool arrowEnd = false) => Line(name, a.X, a.Y, b.X, b.Y, "111111", 0.35, dashed, arrowEnd: arrowEnd);
        public void Line(string name, double x1, double y1, double x2, double y2, string color = "111111", double lineMm = .35, bool dashed = false, bool arrowStart = false, bool arrowEnd = false)
        {
            var x = Math.Min(x1, x2); var y = Math.Min(y1, y2); var cx = Math.Max(.001, Math.Abs(x2 - x1)); var cy = Math.Max(.001, Math.Abs(y2 - y1)); var flipH = x2 < x1 ? " flipH=\"1\"" : ""; var flipV = y2 < y1 ? " flipV=\"1\"" : "";
            shapes.Append($"<p:sp><p:nvSpPr><p:cNvPr id=\"{id++}\" name=\"{X(name)}\"/><p:cNvSpPr/><p:nvPr/></p:nvSpPr><p:spPr><a:xfrm{flipH}{flipV}><a:off x=\"{Mm(x)}\" y=\"{Mm(y)}\"/><a:ext cx=\"{Mm(cx)}\" cy=\"{Mm(cy)}\"/></a:xfrm><a:prstGeom prst=\"line\"><a:avLst/></a:prstGeom><a:ln w=\"{Mm(lineMm)}\"><a:solidFill><a:srgbClr val=\"{color}\"/></a:solidFill>{(dashed ? "<a:prstDash val=\"dash\"/>" : "")}{(arrowStart ? "<a:headEnd type=\"triangle\"/>" : "")}{(arrowEnd ? "<a:tailEnd type=\"triangle\"/>" : "")}</a:ln></p:spPr></p:sp>");
        }
        public void Rect(string name, double x, double y, double w, double h, string? fill = null, bool line = true) => Shape(name, "rect", x, y, w, h, fill, line ? "111111" : null, .3);
        public void Ellipse(string name, double x, double y, double w, double h, string color = "FF8A00", double lineMm = 1) => Shape(name, "ellipse", x, y, w, h, null, color, lineMm);
        private void Shape(string name, string geometry, double x, double y, double w, double h, string? fill, string? line, double lineMm)
        {
            var fillXml = fill is null ? "<a:noFill/>" : $"<a:solidFill><a:srgbClr val=\"{fill}\"/></a:solidFill>";
            var lineXml = line is null ? "<a:ln><a:noFill/></a:ln>" : $"<a:ln w=\"{Mm(lineMm)}\"><a:solidFill><a:srgbClr val=\"{line}\"/></a:solidFill></a:ln>";
            shapes.Append($"<p:sp><p:nvSpPr><p:cNvPr id=\"{id++}\" name=\"{X(name)}\"/><p:cNvSpPr/><p:nvPr/></p:nvSpPr><p:spPr><a:xfrm><a:off x=\"{Mm(x)}\" y=\"{Mm(y)}\"/><a:ext cx=\"{Mm(w)}\" cy=\"{Mm(h)}\"/></a:xfrm><a:prstGeom prst=\"{geometry}\"><a:avLst/></a:prstGeom>{fillXml}{lineXml}</p:spPr></p:sp>");
        }
        public void Text(string name, string text, double x, double y, double w, double h, double sizeMm, bool centered = false, bool bold = false, string? fill = null, bool border = false, string borderColor = "111111")
        {
            var fillXml = fill is null ? "<a:noFill/>" : $"<a:solidFill><a:srgbClr val=\"{fill}\"/></a:solidFill>";
            var lineXml = border ? $"<a:ln w=\"{Mm(.3)}\"><a:solidFill><a:srgbClr val=\"{borderColor}\"/></a:solidFill></a:ln>" : "<a:ln><a:noFill/></a:ln>";
            var paragraphs = text.Replace("\r", "", StringComparison.Ordinal).Split('\n').Select(line => $"<a:p><a:pPr algn=\"{(centered ? "ctr" : "l")}\"/><a:r><a:rPr lang=\"en-US\" sz=\"{Math.Max(100, (int)Math.Round(sizeMm / 0.352777778 * 100))}\"{(bold ? " b=\"1\"" : "")}><a:latin typeface=\"Inter\"/></a:rPr><a:t>{X(line)}</a:t></a:r><a:endParaRPr lang=\"en-US\"/></a:p>");
            shapes.Append($"<p:sp><p:nvSpPr><p:cNvPr id=\"{id++}\" name=\"{X(name)}\"/><p:cNvSpPr txBox=\"1\"/><p:nvPr/></p:nvSpPr><p:spPr><a:xfrm><a:off x=\"{Mm(x)}\" y=\"{Mm(y)}\"/><a:ext cx=\"{Mm(w)}\" cy=\"{Mm(h)}\"/></a:xfrm><a:prstGeom prst=\"rect\"><a:avLst/></a:prstGeom>{fillXml}{lineXml}</p:spPr><p:txBody><a:bodyPr wrap=\"square\" lIns=\"{Mm(.8)}\" tIns=\"{Mm(.4)}\" rIns=\"{Mm(.8)}\" bIns=\"{Mm(.4)}\"/><a:lstStyle/>{string.Concat(paragraphs)}</p:txBody></p:sp>");
        }
        public void Table(string name, DrawingTableIr table)
        {
            var columns = Math.Max(1, table.Columns.Count); var rows = Math.Max(1, table.Rows.Count + 1);
            string Cell(string text, bool header) => $"<a:tc><a:txBody><a:bodyPr/><a:lstStyle/><a:p><a:r><a:rPr lang=\"en-US\" sz=\"700\"{(header ? " b=\"1\"" : "")}><a:latin typeface=\"Inter\"/></a:rPr><a:t>{X(text)}</a:t></a:r><a:endParaRPr/></a:p></a:txBody><a:tcPr><a:solidFill><a:srgbClr val=\"{(header ? "E4E8ED" : "FFFFFF")}\"/></a:solidFill></a:tcPr></a:tc>";
            var grid = string.Concat(Enumerable.Range(0, columns).Select(_ => $"<a:gridCol w=\"{Mm(table.Bounds.Width / columns)}\"/>"));
            var rowXml = $"<a:tr h=\"{Mm(table.Bounds.Height / rows)}\">{string.Concat(table.Columns.Select(value => Cell(value, true)))}</a:tr>" + string.Concat(table.Rows.Select(row => $"<a:tr h=\"{Mm(table.Bounds.Height / rows)}\">{string.Concat(row.Select(value => Cell(value, false)))}</a:tr>"));
            shapes.Append($"<p:graphicFrame><p:nvGraphicFramePr><p:cNvPr id=\"{id++}\" name=\"{X(name)}\"/><p:cNvGraphicFramePr/><p:nvPr/></p:nvGraphicFramePr><p:xfrm><a:off x=\"{Mm(table.Bounds.X)}\" y=\"{Mm(table.Bounds.Y)}\"/><a:ext cx=\"{Mm(table.Bounds.Width)}\" cy=\"{Mm(table.Bounds.Height)}\"/></p:xfrm><a:graphic><a:graphicData uri=\"http://schemas.openxmlformats.org/drawingml/2006/table\"><a:tbl><a:tblPr firstRow=\"1\" bandRow=\"1\"/><a:tblGrid>{grid}</a:tblGrid>{rowXml}</a:tbl></a:graphicData></a:graphic></p:graphicFrame>");
        }
        public string Build() => $"<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?><p:sld xmlns:a=\"http://schemas.openxmlformats.org/drawingml/2006/main\" xmlns:r=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships\" xmlns:p=\"http://schemas.openxmlformats.org/presentationml/2006/main\"><p:cSld><p:spTree><p:nvGrpSpPr><p:cNvPr id=\"1\" name=\"Slide\"/><p:cNvGrpSpPr/><p:nvPr/></p:nvGrpSpPr><p:grpSpPr/>{shapes}</p:spTree></p:cSld><p:clrMapOvr><a:masterClrMapping/></p:clrMapOvr></p:sld>";
    }

    private static string ContentTypes(int count) => $"<?xml version=\"1.0\" encoding=\"UTF-8\"?><Types xmlns=\"http://schemas.openxmlformats.org/package/2006/content-types\"><Default Extension=\"rels\" ContentType=\"application/vnd.openxmlformats-package.relationships+xml\"/><Default Extension=\"xml\" ContentType=\"application/xml\"/><Override PartName=\"/ppt/presentation.xml\" ContentType=\"application/vnd.openxmlformats-officedocument.presentationml.presentation.main+xml\"/><Override PartName=\"/ppt/slideMasters/slideMaster1.xml\" ContentType=\"application/vnd.openxmlformats-officedocument.presentationml.slideMaster+xml\"/><Override PartName=\"/ppt/slideLayouts/slideLayout1.xml\" ContentType=\"application/vnd.openxmlformats-officedocument.presentationml.slideLayout+xml\"/><Override PartName=\"/ppt/theme/theme1.xml\" ContentType=\"application/vnd.openxmlformats-officedocument.theme+xml\"/><Override PartName=\"/docProps/core.xml\" ContentType=\"application/vnd.openxmlformats-package.core-properties+xml\"/><Override PartName=\"/docProps/app.xml\" ContentType=\"application/vnd.openxmlformats-officedocument.extended-properties+xml\"/>{string.Concat(Enumerable.Range(1, count).Select(i => $"<Override PartName=\"/ppt/slides/slide{i}.xml\" ContentType=\"application/vnd.openxmlformats-officedocument.presentationml.slide+xml\"/>"))}</Types>";
    private static string RootRelationships() => "<?xml version=\"1.0\" encoding=\"UTF-8\"?><Relationships xmlns=\"http://schemas.openxmlformats.org/package/2006/relationships\"><Relationship Id=\"rId1\" Type=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument\" Target=\"ppt/presentation.xml\"/><Relationship Id=\"rId2\" Type=\"http://schemas.openxmlformats.org/package/2006/relationships/metadata/core-properties\" Target=\"docProps/core.xml\"/><Relationship Id=\"rId3\" Type=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships/extended-properties\" Target=\"docProps/app.xml\"/></Relationships>";
    private static string Presentation(int count, long cx, long cy) => $"<?xml version=\"1.0\" encoding=\"UTF-8\"?><p:presentation xmlns:a=\"http://schemas.openxmlformats.org/drawingml/2006/main\" xmlns:r=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships\" xmlns:p=\"http://schemas.openxmlformats.org/presentationml/2006/main\"><p:sldMasterIdLst><p:sldMasterId id=\"2147483648\" r:id=\"rId1\"/></p:sldMasterIdLst><p:sldIdLst>{string.Concat(Enumerable.Range(1, count).Select(i => $"<p:sldId id=\"{255 + i}\" r:id=\"rId{i + 1}\"/>"))}</p:sldIdLst><p:sldSz cx=\"{cx}\" cy=\"{cy}\"/><p:notesSz cx=\"6858000\" cy=\"9144000\"/></p:presentation>";
    private static string PresentationRelationships(int count) => $"<?xml version=\"1.0\" encoding=\"UTF-8\"?><Relationships xmlns=\"http://schemas.openxmlformats.org/package/2006/relationships\"><Relationship Id=\"rId1\" Type=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships/slideMaster\" Target=\"slideMasters/slideMaster1.xml\"/>{string.Concat(Enumerable.Range(1, count).Select(i => $"<Relationship Id=\"rId{i + 1}\" Type=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships/slide\" Target=\"slides/slide{i}.xml\"/>"))}</Relationships>";
    private static string SlideMaster() => "<?xml version=\"1.0\" encoding=\"UTF-8\"?><p:sldMaster xmlns:a=\"http://schemas.openxmlformats.org/drawingml/2006/main\" xmlns:r=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships\" xmlns:p=\"http://schemas.openxmlformats.org/presentationml/2006/main\"><p:cSld><p:spTree><p:nvGrpSpPr><p:cNvPr id=\"1\" name=\"Master\"/><p:cNvGrpSpPr/><p:nvPr/></p:nvGrpSpPr><p:grpSpPr/></p:spTree></p:cSld><p:clrMap accent1=\"accent1\" accent2=\"accent2\" accent3=\"accent3\" accent4=\"accent4\" accent5=\"accent5\" accent6=\"accent6\" bg1=\"lt1\" bg2=\"lt2\" folHlink=\"folHlink\" hlink=\"hlink\" tx1=\"dk1\" tx2=\"dk2\"/><p:sldLayoutIdLst><p:sldLayoutId id=\"2147483649\" r:id=\"rId1\"/></p:sldLayoutIdLst><p:txStyles><p:titleStyle/><p:bodyStyle/><p:otherStyle/></p:txStyles></p:sldMaster>";
    private static string MasterRelationships() => "<?xml version=\"1.0\" encoding=\"UTF-8\"?><Relationships xmlns=\"http://schemas.openxmlformats.org/package/2006/relationships\"><Relationship Id=\"rId1\" Type=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships/slideLayout\" Target=\"../slideLayouts/slideLayout1.xml\"/><Relationship Id=\"rId2\" Type=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships/theme\" Target=\"../theme/theme1.xml\"/></Relationships>";
    private static string SlideLayout() => "<?xml version=\"1.0\" encoding=\"UTF-8\"?><p:sldLayout xmlns:a=\"http://schemas.openxmlformats.org/drawingml/2006/main\" xmlns:r=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships\" xmlns:p=\"http://schemas.openxmlformats.org/presentationml/2006/main\" type=\"blank\"><p:cSld name=\"Blank\"><p:spTree><p:nvGrpSpPr><p:cNvPr id=\"1\" name=\"Layout\"/><p:cNvGrpSpPr/><p:nvPr/></p:nvGrpSpPr><p:grpSpPr/></p:spTree></p:cSld><p:clrMapOvr><a:masterClrMapping/></p:clrMapOvr></p:sldLayout>";
    private static string LayoutRelationships() => "<?xml version=\"1.0\" encoding=\"UTF-8\"?><Relationships xmlns=\"http://schemas.openxmlformats.org/package/2006/relationships\"><Relationship Id=\"rId1\" Type=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships/slideMaster\" Target=\"../slideMasters/slideMaster1.xml\"/></Relationships>";
    private static string SlideRelationships() => "<?xml version=\"1.0\" encoding=\"UTF-8\"?><Relationships xmlns=\"http://schemas.openxmlformats.org/package/2006/relationships\"><Relationship Id=\"rId1\" Type=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships/slideLayout\" Target=\"../slideLayouts/slideLayout1.xml\"/></Relationships>";
    private static string CoreProperties(string title) => $"<?xml version=\"1.0\" encoding=\"UTF-8\"?><cp:coreProperties xmlns:cp=\"http://schemas.openxmlformats.org/package/2006/metadata/core-properties\" xmlns:dc=\"http://purl.org/dc/elements/1.1/\" xmlns:dcterms=\"http://purl.org/dc/terms/\" xmlns:xsi=\"http://www.w3.org/2001/XMLSchema-instance\"><dc:title>{X(title)}</dc:title><dc:creator>Aetheris</dc:creator><dcterms:created xsi:type=\"dcterms:W3CDTF\">2000-01-01T00:00:00Z</dcterms:created><dcterms:modified xsi:type=\"dcterms:W3CDTF\">2000-01-01T00:00:00Z</dcterms:modified></cp:coreProperties>";
    private static string AppProperties(int count) => $"<?xml version=\"1.0\" encoding=\"UTF-8\"?><Properties xmlns=\"http://schemas.openxmlformats.org/officeDocument/2006/extended-properties\" xmlns:vt=\"http://schemas.openxmlformats.org/officeDocument/2006/docPropsVTypes\"><Application>Aetheris</Application><PresentationFormat>Custom</PresentationFormat><Slides>{count}</Slides><Notes>0</Notes><HiddenSlides>0</HiddenSlides><MMClips>0</MMClips><ScaleCrop>false</ScaleCrop></Properties>";
    private static string Theme() => "<?xml version=\"1.0\" encoding=\"UTF-8\"?><a:theme xmlns:a=\"http://schemas.openxmlformats.org/drawingml/2006/main\" name=\"Aetheris Inter\"><a:themeElements><a:clrScheme name=\"Aetheris\"><a:dk1><a:srgbClr val=\"111111\"/></a:dk1><a:lt1><a:srgbClr val=\"FFFFFF\"/></a:lt1><a:dk2><a:srgbClr val=\"333333\"/></a:dk2><a:lt2><a:srgbClr val=\"F7F8FA\"/></a:lt2><a:accent1><a:srgbClr val=\"185FA5\"/></a:accent1><a:accent2><a:srgbClr val=\"FF8A00\"/></a:accent2><a:accent3><a:srgbClr val=\"2E7D32\"/></a:accent3><a:accent4><a:srgbClr val=\"6A1B9A\"/></a:accent4><a:accent5><a:srgbClr val=\"00838F\"/></a:accent5><a:accent6><a:srgbClr val=\"C62828\"/></a:accent6><a:hlink><a:srgbClr val=\"0563C1\"/></a:hlink><a:folHlink><a:srgbClr val=\"954F72\"/></a:folHlink></a:clrScheme><a:fontScheme name=\"Inter\"><a:majorFont><a:latin typeface=\"Inter\"/><a:ea typeface=\"\"/><a:cs typeface=\"\"/></a:majorFont><a:minorFont><a:latin typeface=\"Inter\"/><a:ea typeface=\"\"/><a:cs typeface=\"\"/></a:minorFont></a:fontScheme><a:fmtScheme name=\"Aetheris\"><a:fillStyleLst><a:solidFill><a:schemeClr val=\"phClr\"/></a:solidFill><a:solidFill><a:schemeClr val=\"phClr\"/></a:solidFill><a:solidFill><a:schemeClr val=\"phClr\"/></a:solidFill></a:fillStyleLst><a:lnStyleLst><a:ln w=\"12700\"><a:solidFill><a:schemeClr val=\"phClr\"/></a:solidFill></a:ln><a:ln w=\"25400\"><a:solidFill><a:schemeClr val=\"phClr\"/></a:solidFill></a:ln><a:ln w=\"38100\"><a:solidFill><a:schemeClr val=\"phClr\"/></a:solidFill></a:ln></a:lnStyleLst><a:effectStyleLst><a:effectStyle><a:effectLst/></a:effectStyle><a:effectStyle><a:effectLst/></a:effectStyle><a:effectStyle><a:effectLst/></a:effectStyle></a:effectStyleLst><a:bgFillStyleLst><a:solidFill><a:schemeClr val=\"phClr\"/></a:solidFill><a:solidFill><a:schemeClr val=\"phClr\"/></a:solidFill><a:solidFill><a:schemeClr val=\"phClr\"/></a:solidFill></a:bgFillStyleLst></a:fmtScheme></a:themeElements></a:theme>";
}
