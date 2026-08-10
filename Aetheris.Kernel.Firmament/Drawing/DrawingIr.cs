namespace Aetheris.Kernel.Firmament.Drawing;

public enum DrawingPageOrientation { Portrait, Landscape }
public enum DrawingProjectionKind { Orthographic, Isometric }
public enum DrawingHiddenLinePolicy { VisibleOnly, VisibleAndHidden }
public enum DrawingPrimitiveKind { Visible, Silhouette, Hidden }
public enum DrawingAnnotationKind { LinearDimension, DiameterDimension, RadiusDimension, Datum, FeatureControlFrame, Note }

public readonly record struct DrawingPoint2(double X, double Y);
public readonly record struct DrawingRect(double X, double Y, double Width, double Height)
{
    public double Right => X + Width;
    public double Bottom => Y + Height;
    public bool Intersects(DrawingRect other) => X < other.Right && Right > other.X && Y < other.Bottom && Bottom > other.Y;
}

public sealed record DrawingMetadataIr(
    string Title,
    string ProductName,
    string? PartNumber,
    string? Revision,
    string? Material,
    string DrawingIdentity,
    string TemplateIdentity);

public sealed record DrawingProvenanceIr(
    string SourceProductIdentity,
    string? ConceptIdentity,
    string TemplateIdentity,
    IReadOnlyDictionary<string, string> TemplateArguments,
    string SpecializationIdentity,
    IReadOnlyList<string> StaticSources);

public sealed record DrawingProjectedPrimitiveIr(
    string StableId,
    DrawingPrimitiveKind Kind,
    IReadOnlyList<DrawingPoint2> Points,
    string? OccurrenceIdentity,
    double Depth);

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
    IReadOnlyList<string> AssignedPmi);

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
    string Provenance);

public sealed record DrawingTableIr(
    string Identity,
    IReadOnlyList<string> Columns,
    IReadOnlyList<IReadOnlyList<string>> Rows,
    string SourceIdentity,
    string Provenance);

public sealed record DrawingPageIr(
    int PageNumber,
    DrawingPageOrientation Orientation,
    double WidthMillimetres,
    double HeightMillimetres,
    DrawingRect ContentRect,
    IReadOnlyList<DrawingViewIr> Views,
    IReadOnlyList<DrawingAnnotationIr> Annotations,
    IReadOnlyList<DrawingTableIr> Tables,
    IReadOnlyList<string> Notes);

public sealed record DrawingLayoutEvidenceIr(
    int AnnotationCount,
    int TextModelCollisionsBefore,
    int TextModelCollisionsAfter,
    int TextTextCollisionsAfter,
    IReadOnlyDictionary<string, int> LaneOccupancy,
    int RejectedCandidateCount,
    int FailedAnnotationCount);

public sealed record DrawingPerformanceIr(
    double SourceCompileMilliseconds,
    double ProjectionMilliseconds,
    double LayoutMilliseconds,
    double RenderMilliseconds,
    double PdfMilliseconds);

public sealed record DrawingIr(
    string Identity,
    DrawingMetadataIr Metadata,
    DrawingProvenanceIr Provenance,
    IReadOnlyList<DrawingPageIr> Pages,
    DrawingLayoutEvidenceIr LayoutEvidence,
    DrawingPerformanceIr Performance,
    IReadOnlyList<string> Diagnostics,
    string ProjectionPolicy,
    string SchemaVersion = "aetheris-drawing-m0");

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
