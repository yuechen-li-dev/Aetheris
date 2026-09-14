using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Aetheris.Drawing;

public static class DrawingNotesPersistence
{
    public static JsonSerializerOptions JsonOptions { get; } = CreateOptions();

    private static JsonSerializerOptions CreateOptions()
    {
        var options = new JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            PropertyNameCaseInsensitive = true
        };
        options.Converters.Add(new JsonStringEnumConverter());
        return options;
    }

    public static DrawingNotesProject Load(string path)
    {
        var project = JsonSerializer.Deserialize<DrawingNotesProject>(File.ReadAllText(path), JsonOptions)
            ?? throw new DrawingNotesException("drawing-project-empty", $"Drawing Notes project '{path}' is empty.");
        _ = new DrawingNotebook(project);
        return project;
    }

    public static string Serialize(DrawingNotesProject project)
    {
        var canonical = Canonical(project);
        return JsonSerializer.Serialize(canonical, JsonOptions).Replace("\r\n", "\n", StringComparison.Ordinal) + "\n";
    }

    public static void Save(string path, DrawingNotesProject project)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);
        File.WriteAllText(path, Serialize(project), new UTF8Encoding(false));
    }

    public static DrawingNotesProject Canonical(DrawingNotesProject project) => new()
    {
        Name = project.Name,
        Document = project.Document with
        {
            Pages = project.Document.Pages.OrderBy(x => x.Number).ToArray(),
            SheetIdentifiers = project.Document.SheetIdentifiers?.Order(StringComparer.Ordinal).ToArray()
        },
        Regions = project.Regions.OrderBy(x => x.Page).ThenBy(x => x.Id, StringComparer.Ordinal).ToList(),
        Annotations = project.Annotations.Select(a => a with
        {
            Tags = a.Tags?.Order(StringComparer.Ordinal).ToArray(),
            Aliases = a.Aliases?.Order(StringComparer.Ordinal).ToArray()
        }).OrderBy(x => x.Page).ThenBy(x => x.Id, StringComparer.Ordinal).ToList(),
        Relations = project.Relations.OrderBy(x => x.Id, StringComparer.Ordinal).ToList(),
        Passes = project.Passes.OrderBy(x => x.Id, StringComparer.Ordinal).ToList()
    };
}

public sealed record DrawingExportFilter(int? Page = null, string? RegionId = null, DrawingSemanticClass? SemanticClass = null, bool UnresolvedOnly = false);

public static class DrawingNotesMarkdown
{
    public static string Export(DrawingNotesProject input, DrawingExportFilter? filter = null)
    {
        filter ??= new();
        var project = DrawingNotesPersistence.Canonical(input);
        var regions = project.Regions.Where(region => IncludeRegion(region, filter, project)).ToArray();
        var allowedRegions = regions.Select(x => x.Id).ToHashSet(StringComparer.Ordinal);
        var annotations = project.Annotations.Where(annotation => IncludeAnnotation(annotation, filter, allowedRegions)).ToArray();
        var allowedNodes = regions.Select(x => x.Id).Concat(annotations.Select(x => x.Id)).ToHashSet(StringComparer.Ordinal);
        var relations = project.Relations.Where(relation => allowedNodes.Contains(relation.FromId) && allowedNodes.Contains(relation.ToId)).ToArray();
        var pages = regions.Select(x => x.Page).Concat(annotations.Select(x => x.Page)).Distinct().Order().ToArray();
        var builder = new StringBuilder();
        builder.Append("# ").AppendLine(project.Name).AppendLine();
        builder.AppendLine("Source:");
        builder.Append("- Document: ").AppendLine(project.Document.FileName);
        builder.Append("- SHA-256: `").Append(project.Document.Sha256).AppendLine("`");
        if (!string.IsNullOrWhiteSpace(project.Document.Title)) builder.Append("- Title: ").AppendLine(project.Document.Title);
        if (!string.IsNullOrWhiteSpace(project.Document.VisibleDate)) builder.Append("- Visible date: ").AppendLine(project.Document.VisibleDate);
        if (project.Document.SheetIdentifiers is { Count: > 0 }) builder.Append("- Sheets: ").AppendLine(string.Join(", ", project.Document.SheetIdentifiers));
        builder.Append("- Pages inspected: ").AppendLine(pages.Length == 0 ? "none" : string.Join(", ", pages));
        if (project.Passes.Count > 0) builder.Append("- Passes: ").AppendLine(string.Join(", ", project.Passes.Select(x => $"{x.Id} ({x.Name})")));
        builder.Append("- Renderer: ").Append(project.Document.Renderer).Append(' ').AppendLine(project.Document.RendererVersion).AppendLine();
        builder.AppendLine("Coordinates: top-left PDF points `[x,y,width,height]` (1 point = 1/72 inch).").AppendLine();

        foreach (var page in pages)
        {
            builder.Append("## Page ").Append(page).AppendLine().AppendLine();
            foreach (var region in regions.Where(x => x.Page == page))
            {
                builder.Append("### ").Append(region.Name).Append(" (`").Append(region.Id).AppendLine("`)").AppendLine();
                builder.Append("Type: ").Append(region.Type).Append("  ").AppendLine();
                builder.Append("Source: p").Append(page).Append(" / ").Append(region.Name).Append(" / ").Append(region.Bounds).AppendLine().AppendLine();
                foreach (var annotation in annotations.Where(a => a.Page == page && a.RegionId == region.Id)) AppendAnnotation(builder, annotation, relations);
            }
            foreach (var annotation in annotations.Where(a => a.Page == page && (a.RegionId is null || !allowedRegions.Contains(a.RegionId)))) AppendAnnotation(builder, annotation, relations);
        }

        var issues = new DrawingNotebook(project).InspectIssues();
        if (issues.Count > 0)
        {
            builder.AppendLine("## Review findings").AppendLine();
            foreach (var issue in issues) builder.Append("- **").Append(issue.Code).Append(":** ").AppendLine(issue.Message);
            builder.AppendLine();
        }
        builder.AppendLine("## Unresolved").AppendLine();
        var unresolved = annotations.Where(a => a.Category == DrawingAnnotationCategory.Question || a.Confidence == DrawingConfidence.Unresolved).ToArray();
        if (unresolved.Length == 0) builder.AppendLine("- None recorded.");
        else foreach (var item in unresolved) builder.Append("- `").Append(item.Id).Append("`: ").AppendLine(item.Note ?? item.Label);
        return builder.ToString().Replace("\r\n", "\n", StringComparison.Ordinal);
    }

    private static void AppendAnnotation(StringBuilder builder, DrawingAnnotation annotation, IReadOnlyList<DrawingRelation> relations)
    {
        builder.Append("#### ").Append(annotation.Label).Append(" (`").Append(annotation.Id).AppendLine("`)").AppendLine();
        builder.Append("- Category: ").Append(annotation.Category).AppendLine();
        builder.Append("- Classification: ").Append(annotation.SemanticClass).AppendLine();
        builder.Append("- Confidence: ").Append(annotation.Confidence).AppendLine();
        if (!string.IsNullOrWhiteSpace(annotation.OriginalText)) builder.Append("- Original text: “").Append(annotation.OriginalText).AppendLine("”");
        if (annotation.Dimension is { } dimension)
        {
            builder.Append("- Value: ").AppendLine(dimension.ValueText);
            builder.Append("- Dimension type: ").AppendLine(dimension.Type.ToString());
            if (dimension.Axis is not null) builder.Append("- Axis: ").AppendLine(dimension.Axis.ToString());
            if (!string.IsNullOrWhiteSpace(dimension.DatumId)) builder.Append("- Datum: `").Append(dimension.DatumId).AppendLine("`");
            if (!string.IsNullOrWhiteSpace(dimension.SourceFeatureId)) builder.Append("- From: `").Append(dimension.SourceFeatureId).AppendLine("`");
            if (!string.IsNullOrWhiteSpace(dimension.TargetFeatureId)) builder.Append("- To: `").Append(dimension.TargetFeatureId).AppendLine("`");
            if (!string.IsNullOrWhiteSpace(dimension.Interpretation)) builder.Append("- Interpretation: ").AppendLine(dimension.Interpretation);
        }
        if (!string.IsNullOrWhiteSpace(annotation.Note)) builder.Append("- Note: ").AppendLine(annotation.Note);
        if (!string.IsNullOrWhiteSpace(annotation.Pass)) builder.Append("- Pass: `").Append(annotation.Pass).AppendLine("`");
        builder.Append("- Source: p").Append(annotation.Page).Append(" / ").Append(annotation.RegionId ?? "unassigned").Append(" / ").AppendLine(annotation.Bounds.ToString());
        foreach (var relation in relations.Where(r => r.FromId == annotation.Id))
            builder.Append("- Relation: ").Append(relation.Kind).Append(" -> `").Append(relation.ToId).Append("` (").Append(relation.Confidence).AppendLine(")");
        builder.AppendLine();
    }

    private static bool IncludeRegion(DrawingRegion region, DrawingExportFilter filter, DrawingNotesProject project)
    {
        if (filter.Page is { } page && region.Page != page) return false;
        if (filter.RegionId is { } id && region.Id != id && region.ParentRegionId != id) return false;
        if (filter.UnresolvedOnly && region.Confidence != DrawingConfidence.Unresolved && !project.Annotations.Any(a => a.RegionId == region.Id && (a.Category == DrawingAnnotationCategory.Question || a.Confidence == DrawingConfidence.Unresolved))) return false;
        return true;
    }

    private static bool IncludeAnnotation(DrawingAnnotation annotation, DrawingExportFilter filter, IReadOnlySet<string> allowedRegions)
    {
        if (filter.Page is { } page && annotation.Page != page) return false;
        if (filter.RegionId is not null && (annotation.RegionId is null || !allowedRegions.Contains(annotation.RegionId))) return false;
        if (filter.SemanticClass is { } semanticClass && annotation.SemanticClass != semanticClass) return false;
        if (filter.UnresolvedOnly && annotation.Category != DrawingAnnotationCategory.Question && annotation.Confidence != DrawingConfidence.Unresolved) return false;
        return true;
    }
}
