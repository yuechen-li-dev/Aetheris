namespace Aetheris.Drawing;

public sealed class DrawingNotebook
{
    public DrawingNotesProject Project { get; }

    public DrawingNotebook(DrawingNotesProject project)
    {
        Project = project ?? throw new ArgumentNullException(nameof(project));
        ValidateProjectShape(project);
    }

    public DrawingRegion AddRegion(DrawingRegion region)
    {
        ValidatePageAndBounds(region.Page, region.Bounds, region.Id);
        ValidateUniqueId(region.Id);
        if (region.ParentRegionId is { } parent)
        {
            var owner = Project.Regions.SingleOrDefault(candidate => candidate.Id == parent)
                ?? throw new DrawingNotesException("drawing-parent-region-missing", $"Parent region '{parent}' does not exist.");
            if (owner.Page != region.Page) throw new DrawingNotesException("drawing-parent-region-page-mismatch", "Nested regions must be on the same page as their parent.");
        }
        Project.Regions.Add(region with { Id = DrawingIds.Normalize(region.Id) });
        return region;
    }

    public DrawingAnnotation AddAnnotation(DrawingAnnotation annotation)
    {
        ValidatePageAndBounds(annotation.Page, annotation.Bounds, annotation.Id);
        ValidateUniqueId(annotation.Id);
        if (annotation.RegionId is { } region)
        {
            var owner = Project.Regions.SingleOrDefault(candidate => candidate.Id == region)
                ?? throw new DrawingNotesException("drawing-region-missing", $"Region '{region}' does not exist.");
            if (owner.Page != annotation.Page) throw new DrawingNotesException("drawing-annotation-region-page-mismatch", "Annotation and owning region must share a page.");
        }
        if (annotation.Category == DrawingAnnotationCategory.Dimension && annotation.Dimension is null)
            throw new DrawingNotesException("drawing-dimension-value-missing", "Dimension annotations require a dimension interpretation.");
        if (annotation.Category != DrawingAnnotationCategory.Dimension && annotation.Dimension is not null)
            throw new DrawingNotesException("drawing-dimension-category-mismatch", "Only Dimension annotations may carry dimension interpretation data.");
        foreach (var reference in new[] { annotation.Dimension?.DatumId, annotation.Dimension?.SourceFeatureId, annotation.Dimension?.TargetFeatureId }.Where(x => x is not null))
            if (!HasNode(reference!)) throw new DrawingNotesException("drawing-dimension-reference-missing", $"Dimension '{annotation.Id}' references unknown node '{reference}'.");
        Project.Annotations.Add(annotation with { Id = DrawingIds.Normalize(annotation.Id) });
        return annotation;
    }

    public DrawingRelation AddRelation(DrawingRelation relation)
    {
        ValidateUniqueId(relation.Id);
        if (!HasNode(relation.FromId) || !HasNode(relation.ToId))
            throw new DrawingNotesException("drawing-relation-node-missing", $"Relation '{relation.Id}' references an unknown node.");
        if (relation.FromId == relation.ToId) throw new DrawingNotesException("drawing-relation-self", "A relation cannot link a note to itself.");
        Project.Relations.Add(relation with { Id = DrawingIds.Normalize(relation.Id) });
        return relation;
    }

    public DrawingPass AddPass(DrawingPass pass)
    {
        var normalized = DrawingIds.Normalize(pass.Id);
        ValidateUniqueId(normalized);
        if (Project.Passes.Any(x => x.Id == normalized)) throw new DrawingNotesException("drawing-id-duplicate", $"Stable ID '{normalized}' already exists.");
        var added = pass with { Id = normalized };
        Project.Passes.Add(added);
        return added;
    }

    public IReadOnlyList<DrawingNotesIssue> InspectIssues()
    {
        var issues = new List<DrawingNotesIssue>();
        foreach (var group in Project.Annotations.GroupBy(a => (a.Page, a.Category)))
        {
            var items = group.OrderBy(a => a.Id, StringComparer.Ordinal).ToArray();
            for (var i = 0; i < items.Length; i++)
            for (var j = i + 1; j < items.Length; j++)
            {
                if (!items[i].Bounds.NearlyEquals(items[j].Bounds)) continue;
                var ids = new[] { items[i].Id, items[j].Id };
                var sameMeaning = string.Equals(items[i].Label, items[j].Label, StringComparison.Ordinal)
                                  && items[i].SemanticClass == items[j].SemanticClass
                                  && string.Equals(items[i].Dimension?.ValueText, items[j].Dimension?.ValueText, StringComparison.Ordinal);
                issues.Add(sameMeaning
                    ? new("drawing-likely-duplicate", $"Annotations '{ids[0]}' and '{ids[1]}' share the same source and meaning; they were not merged.", ids)
                    : new("drawing-interpretation-conflict", $"Annotations '{ids[0]}' and '{ids[1]}' assign different meanings to the same source area.", ids));
            }
        }
        return issues.OrderBy(issue => issue.Code, StringComparer.Ordinal).ThenBy(issue => issue.ItemIds[0], StringComparer.Ordinal).ToArray();
    }

    public bool SourceMatches(string pdfPath) =>
        string.Equals(Project.Document.Sha256, DrawingPdf.Hash(pdfPath), StringComparison.OrdinalIgnoreCase);

    public void RequireSourceMatch(string pdfPath)
    {
        if (!SourceMatches(pdfPath))
            throw new DrawingNotesException("drawing-source-hash-mismatch", $"Notes refer to SHA-256 {Project.Document.Sha256}, but '{Path.GetFileName(pdfPath)}' has {DrawingPdf.Hash(pdfPath)}.");
    }

    public void ReplaceProjectCollections(IEnumerable<DrawingRegion> regions, IEnumerable<DrawingAnnotation> annotations, IEnumerable<DrawingRelation> relations)
    {
        Project.Regions.Clear(); Project.Regions.AddRange(regions);
        Project.Annotations.Clear(); Project.Annotations.AddRange(annotations);
        Project.Relations.Clear(); Project.Relations.AddRange(relations);
        ValidateProjectShape(Project);
    }

    private bool HasNode(string id) => Project.Regions.Any(x => x.Id == id) || Project.Annotations.Any(x => x.Id == id);

    private void ValidateUniqueId(string id)
    {
        id = DrawingIds.Normalize(id);
        if (Project.Regions.Any(x => x.Id == id) || Project.Annotations.Any(x => x.Id == id) || Project.Relations.Any(x => x.Id == id) || Project.Passes.Any(x => x.Id == id))
            throw new DrawingNotesException("drawing-id-duplicate", $"Stable ID '{id}' already exists.");
    }

    private void ValidatePageAndBounds(int page, DrawingBounds bounds, string owner)
    {
        bounds.Validate(owner);
        var sourcePage = Project.Document.Pages.SingleOrDefault(candidate => candidate.Number == page)
            ?? throw new DrawingNotesException("drawing-page-invalid", $"Page {page} is not in the source document.");
        if (bounds.Right > sourcePage.WidthPoints + 0.01 || bounds.Bottom > sourcePage.HeightPoints + 0.01)
            throw new DrawingNotesException("drawing-bounds-outside-page", $"'{owner}' bounds {bounds} exceed page {page} ({sourcePage.WidthPoints:0.##} x {sourcePage.HeightPoints:0.##} pt).");
    }

    private static void ValidateProjectShape(DrawingNotesProject project)
    {
        if (project.SchemaVersion != "aetheris-drawing-notes-1") throw new DrawingNotesException("drawing-schema-unsupported", $"Unsupported Drawing Notes schema '{project.SchemaVersion}'.");
        if (project.Document.PageCount != project.Document.Pages.Count) throw new DrawingNotesException("drawing-page-metadata-mismatch", "Document page count does not match its page metadata.");
        if (project.Document.Pages.Select(x => x.Number).Distinct().Count() != project.Document.PageCount || project.Document.Pages.Any(x => x.Number < 1 || x.WidthPoints <= 0 || x.HeightPoints <= 0))
            throw new DrawingNotesException("drawing-page-metadata-invalid", "Document page metadata must contain unique positive page numbers and sizes.");
        var duplicate = project.Regions.Select(x => x.Id).Concat(project.Annotations.Select(x => x.Id)).Concat(project.Relations.Select(x => x.Id)).Concat(project.Passes.Select(x => x.Id)).GroupBy(x => x, StringComparer.Ordinal).FirstOrDefault(g => g.Count() > 1);
        if (duplicate is not null) throw new DrawingNotesException("drawing-id-duplicate", $"Stable ID '{duplicate.Key}' occurs more than once.");
        var pages = project.Document.Pages.ToDictionary(x => x.Number);
        foreach (var region in project.Regions)
        {
            region.Bounds.Validate(region.Id);
            if (!pages.TryGetValue(region.Page, out var page) || region.Bounds.Right > page.WidthPoints + 0.01 || region.Bounds.Bottom > page.HeightPoints + 0.01)
                throw new DrawingNotesException("drawing-bounds-outside-page", $"Region '{region.Id}' is outside page {region.Page}.");
            if (region.ParentRegionId is { } parent && !project.Regions.Any(x => x.Id == parent && x.Page == region.Page))
                throw new DrawingNotesException("drawing-parent-region-missing", $"Region '{region.Id}' has an invalid parent '{parent}'.");
        }
        foreach (var annotation in project.Annotations)
        {
            annotation.Bounds.Validate(annotation.Id);
            if (!pages.TryGetValue(annotation.Page, out var page) || annotation.Bounds.Right > page.WidthPoints + 0.01 || annotation.Bounds.Bottom > page.HeightPoints + 0.01)
                throw new DrawingNotesException("drawing-bounds-outside-page", $"Annotation '{annotation.Id}' is outside page {annotation.Page}.");
            if (annotation.RegionId is { } region && !project.Regions.Any(x => x.Id == region && x.Page == annotation.Page))
                throw new DrawingNotesException("drawing-region-missing", $"Annotation '{annotation.Id}' has an invalid region '{region}'.");
            if ((annotation.Category == DrawingAnnotationCategory.Dimension) != (annotation.Dimension is not null))
                throw new DrawingNotesException("drawing-dimension-category-mismatch", $"Annotation '{annotation.Id}' has inconsistent dimension data.");
        }
        var nodes = project.Regions.Select(x => x.Id).Concat(project.Annotations.Select(x => x.Id)).ToHashSet(StringComparer.Ordinal);
        foreach (var annotation in project.Annotations.Where(x => x.Dimension is not null))
        foreach (var reference in new[] { annotation.Dimension!.DatumId, annotation.Dimension.SourceFeatureId, annotation.Dimension.TargetFeatureId }.Where(x => x is not null))
            if (!nodes.Contains(reference!)) throw new DrawingNotesException("drawing-dimension-reference-missing", $"Dimension '{annotation.Id}' references unknown node '{reference}'.");
        foreach (var relation in project.Relations)
            if (relation.FromId == relation.ToId || !nodes.Contains(relation.FromId) || !nodes.Contains(relation.ToId))
                throw new DrawingNotesException("drawing-relation-node-missing", $"Relation '{relation.Id}' has invalid endpoints.");
    }
}
