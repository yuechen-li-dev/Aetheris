using Aetheris.Collaboration;

namespace Aetheris.Kernel.Firmament.Drawing;

public enum DrawingPageOrientation { Portrait, Landscape }
public enum DrawingProjectionKind { Orthographic, Isometric }
public enum DrawingHiddenLinePolicy { VisibleOnly, VisibleAndHidden, Wireframe }
public enum DrawingPrimitiveKind { Visible, Silhouette, Hidden, SurfaceGrid, CenterMark }
public enum DrawingAnnotationKind { LinearDimension, DiameterDimension, RadiusDimension, Datum, FeatureControlFrame, Note }
public enum DrawingTableKind { Design, BillOfMaterials, HoleCoordinates }

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
    string? StaticProvenance = null)
{
    public DrawingSignoffIr? Signoff { get; init; }
}

public sealed record DrawingSignoffEntryIr(string? Name = null, string? Date = null);
public sealed record DrawingSignoffIr(DrawingSignoffEntryIr Drawn, DrawingSignoffEntryIr Checked, DrawingSignoffEntryIr Approved);

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
    IReadOnlyDictionary<string, string> Fields)
{
    public DrawingTitleBlockTableIr? Table { get; init; }
}

public sealed record DrawingTitleBlockTableIr(IReadOnlyList<double> ColumnWidths, IReadOnlyList<DrawingTitleBlockRowIr> Rows);
public sealed record DrawingTitleBlockRowIr(double Height, IReadOnlyList<DrawingTitleBlockCellIr> Cells);
public sealed record DrawingTitleBlockCellIr(string Key, string Label, string Value, int ColumnSpan,
    DrawingRect Bounds, IReadOnlyList<string> ValueLines, double ValueSizeMillimetres, bool Emphasized = false);

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
    DrawingVisibilityEvidenceIr? VisibilityEvidence = null)
{
    public DrawingCameraIr? Camera { get; init; }
}

public sealed record DrawingCameraIr(IReadOnlyList<double> Right, IReadOnlyList<double> Up, double OriginX, double OriginY);

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
    DrawingLocationIr? Location = null)
{
    public IReadOnlyList<DrawingDimensionLineIr> DimensionLines { get; init; } = [];
    public IReadOnlyList<DrawingDimensionLineIr> Strokes => DrawingAnnotationGeometry.Strokes(this).ToArray();
}

public sealed record DrawingDimensionLineIr(DrawingPoint2 Start, DrawingPoint2 End, bool ArrowStart = false, bool ArrowEnd = false);

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
    int FailedAnnotationCount)
{
    public int TextLeaderCollisionsAfter { get; init; }
}

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

/// <summary>Explicit authoring inputs, not inferred process capability or approval.
/// Linear tolerance applies to stock sizes and ordinate coordinates, never overrides PMI.</summary>
public sealed record DrawingGenerationOptions(bool ReferenceOnly = false, double? LinearToleranceMillimetres = null, string? Material = null,
    double? AngularToleranceDegrees = null);

public sealed record DrawingDefinedHoleIr(string Identity, double X, double Y, double Diameter, string Extent,
    string Group, IReadOnlyList<string> SourceReferences);
public sealed record DrawingDefinitionEvidenceIr(bool NominalGeometryComplete, bool DimensionalTolerancesComplete,
    bool MaterialSpecified, bool ProcessCapabilityVerified, string Family, IReadOnlyList<int> CoveredFaces,
    IReadOnlyList<string> MissingRequirements, IReadOnlyDictionary<string, double> StockDimensions,
    IReadOnlyList<DrawingDefinedHoleIr> Holes, double? LinearToleranceMillimetres = null)
{
    public double? AngularToleranceDegrees { get; init; }
    public double? MinimumHoleWebAtDeclaredLimitsMillimetres { get; init; }
    public string ToleranceScreenScope { get; init; } = "Stock size and Cartesian entry-center intervals with cylinder-axis lean on ideal orthogonal planar stock; stock form, general angular stacks and process capability are unqualified.";
}

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
    ReviewIr? Reviews = null,
    string SchemaVersion = "aetheris-drawing-m0b")
{
    public DrawingDefinitionEvidenceIr? DefinitionEvidence { get; init; }
}

public sealed record DrawingCompileArtifacts(
    DrawingIr Drawing,
    string DrawingIrPath,
    string SvgPath,
    string PdfPath,
    string ValidationPath,
    string PptxPath,
    string? ReviewPptxPath,
    string? DfmReviewPptxPath,
    string? ReviewIrPath,
    string PdfSha256,
    string DrawingIrSha256,
    string PptxSha256);

public sealed record DrawingCompileResult(
    bool IsSuccess,
    DrawingCompileArtifacts? Artifacts,
    IReadOnlyList<string> Diagnostics)
{
    public IReadOnlyList<FirmamentV2.FirmamentV2ValidationDiagnostic> FrontendDiagnostics { get; init; } = [];
    public IReadOnlyList<Aetheris.Kernel.Core.Diagnostics.KernelDiagnostic> KernelDiagnostics()=>FirmamentV2.FirmamentDomainDiagnostics.Present(Diagnostics,FrontendDiagnostics,"Firmament.Drawing");
}
