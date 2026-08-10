namespace Aetheris.Kernel.Firmament.Drawing;

public enum DrawingPageOrientation { Portrait, Landscape }
public enum DrawingProjectionKind { Orthographic, Isometric }
public enum DrawingHiddenLinePolicy { VisibleOnly, VisibleAndHidden }
public enum DrawingPrimitiveKind { Visible, Silhouette, Hidden }
public enum DrawingAnnotationKind { LinearDimension, DiameterDimension, RadiusDimension, Datum, FeatureControlFrame, Note }
public enum DrawingTableKind { Design, BillOfMaterials }

public readonly record struct DrawingPoint2(double X, double Y);
public readonly record struct DrawingRect(double X, double Y, double Width, double Height)
{
    public double Right => X + Width;
    public double Bottom => Y + Height;
    public DrawingPoint2 Center => new(X + Width / 2d, Y + Height / 2d);
    public bool Intersects(DrawingRect other) => X < other.Right && Right > other.X && Y < other.Bottom && Bottom > other.Y;
}

public sealed record DrawingSemanticVersionIr(int Major, int Minor, int Patch)
{
    public override string ToString() => $"{Major}.{Minor}.{Patch}";
}

public sealed record DrawingMetadataIr(
    string Title,
    string ProductName,
    string? PartNumber,
    DrawingSemanticVersionIr? Revision,
    string? Material,
    string DrawingIdentity,
    string TemplateIdentity,
    string? Company = null,
    string? Author = null,
    string? Date = null,
    string? Description = null,
    string? StaticIdentity = null,
    string? StaticProvenance = null);

public sealed record DrawingLocationIr(int Page, string Zone);
public sealed record DrawingPageZoneIr(string Address, DrawingRect Bounds);
public sealed record DrawingPageZoneSchemeIr(
    int Rows,
    int Columns,
    IReadOnlyList<string> RowLabels,
    IReadOnlyList<string> ColumnLabels,
    IReadOnlyList<DrawingPageZoneIr> Zones);

public sealed record DrawingInformationBlockIr(
    DrawingRect Bounds,
    DrawingLocationIr Location,
    IReadOnlyDictionary<string, string> Fields);

public sealed record DrawingNoteIr(string Identity, string Text, DrawingRect Bounds, DrawingLocationIr Location);

public sealed record DrawingProvenanceIr(
    string SourceProductIdentity,
    string? ConceptIdentity,
    string TemplateIdentity,
    IReadOnlyDictionary<string, string> TemplateArguments,
    string SpecializationIdentity,
    IReadOnlyList<string> StaticSources,
    string SourceKind = "Part");

public sealed record DrawingProjectedPrimitiveIr(
    string StableId,
    DrawingPrimitiveKind Kind,
    IReadOnlyList<DrawingPoint2> Points,
    string? OccurrenceIdentity,
    double Depth,
    string? DefinitionIdentity = null,
    string? SourceEdgeIdentity = null);

public sealed record DrawingVisibilityEvidenceIr(
    int CandidateSegments,
    int VisibleSegments,
    int HiddenSegments,
    int SplitPointCount,
    int OcclusionTriangleCount,
    IReadOnlyList<string> UnsupportedFaceSupports,
    string Policy,
    string EvidenceHash);

public sealed record DrawingViewIr(
    string Identity,
    DrawingProjectionKind Projection,
    DrawingHiddenLinePolicy HiddenLinePolicy,
    DrawingPoint2 Direction,
    IReadOnlyList<double> Direction3,
    DrawingRect Viewport,
    DrawingRect GeometryBounds,
    double Scale,
    IReadOnlyList<DrawingProjectedPrimitiveIr> Primitives,
    IReadOnlyDictionary<string, DrawingPoint2> SemanticAnchors,
    IReadOnlyList<string> AssignedPmi,
    DrawingLocationIr? Location = null,
    DrawingVisibilityEvidenceIr? VisibilityEvidence = null);

public sealed record DrawingAnnotationCandidateIr(
    string Identity,
    DrawingRect Body,
    IReadOnlyList<DrawingPoint2> Leader,
    string Lane,
    int LaneIndex,
    double Cost,
    IReadOnlyList<string> Rejections);

public sealed record DrawingAnnotationIr(
    string Identity,
    string SemanticReference,
    string AssignedView,
    DrawingAnnotationKind Kind,
    string EngineeringDisplay,
    DrawingPoint2 ProjectedAnchor,
    DrawingAnnotationCandidateIr SelectedCandidate,
    IReadOnlyList<DrawingAnnotationCandidateIr> Candidates,
    string Provenance,
    DrawingLocationIr? Location = null);

public sealed record DrawingTableIr(
    string Identity,
    IReadOnlyList<string> Columns,
    IReadOnlyList<IReadOnlyList<string>> Rows,
    string SourceIdentity,
    string Provenance,
    DrawingTableKind Kind = DrawingTableKind.Design,
    DrawingRect Bounds = default,
    DrawingLocationIr? Location = null);

public sealed record DrawingBomItemIr(
    int Item,
    string DefinitionIdentity,
    string Description,
    int Quantity,
    string? PartNumber,
    string? Revision,
    IReadOnlyList<string> OccurrencePaths);

public sealed record DrawingBomIr(
    string Identity,
    string FlatteningPolicy,
    IReadOnlyList<DrawingBomItemIr> Items,
    DrawingTableIr Table);

public sealed record DrawingPageIr(
    int PageNumber,
    DrawingPageOrientation Orientation,
    double WidthMillimetres,
    double HeightMillimetres,
    DrawingRect ContentRect,
    IReadOnlyList<DrawingViewIr> Views,
    IReadOnlyList<DrawingAnnotationIr> Annotations,
    IReadOnlyList<DrawingTableIr> Tables,
    IReadOnlyList<string> Notes,
    DrawingPageZoneSchemeIr? ZoneScheme = null,
    DrawingInformationBlockIr? InformationBlock = null,
    IReadOnlyList<DrawingNoteIr>? LocatedNotes = null,
    DrawingBomIr? Bom = null);

public sealed record DrawingLayoutEvidenceIr(
    int AnnotationCount,
    int TextModelCollisionsBefore,
    int TextModelCollisionsAfter,
    int TextTextCollisionsAfter,
    IReadOnlyDictionary<string, int> LaneOccupancy,
    int RejectedCandidateCount,
    int FailedAnnotationCount);

public sealed record DrawingTypographyIr(
    string Family,
    string PdfEmbedding,
    string MetricsSource,
    IReadOnlyDictionary<string, double> TextSizesMillimetres);

public sealed record DrawingPerformanceIr(
    double SourceCompileMilliseconds,
    double ProjectionMilliseconds,
    double LayoutMilliseconds,
    double RenderMilliseconds,
    double PdfMilliseconds,
    double BomMilliseconds = 0);

public sealed record DrawingIr(
    string Identity,
    DrawingMetadataIr Metadata,
    DrawingProvenanceIr Provenance,
    IReadOnlyList<DrawingPageIr> Pages,
    DrawingLayoutEvidenceIr LayoutEvidence,
    DrawingPerformanceIr Performance,
    IReadOnlyList<string> Diagnostics,
    string ProjectionPolicy,
    DrawingTypographyIr? Typography = null,
    string SchemaVersion = "aetheris-drawing-m0b");

public sealed record DrawingCompileArtifacts(
    DrawingIr Drawing,
    string DrawingIrPath,
    string SvgPath,
    string PdfPath,
    string ValidationPath,
    string PdfSha256,
    string DrawingIrSha256);

public sealed record DrawingCompileResult(
    bool IsSuccess,
    DrawingCompileArtifacts? Artifacts,
    IReadOnlyList<string> Diagnostics);
