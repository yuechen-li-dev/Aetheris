using System.Text.Json.Serialization;
using Aetheris.Kernel.Core.Math;

namespace Aetheris.CLI;

public sealed record IdRangeSummary(
    int Min,
    int Max,
    int Count,
    bool Contiguous);

public sealed record AnalyzeSummary(
    int BodyCount,
    int ShellCount,
    int FaceCount,
    int EdgeCount,
    int VertexCount,
    BoundingBox3D? BoundingBox,
    string StructuralAssessment,
    IReadOnlyDictionary<string, int> SurfaceFamilies,
    IReadOnlyDictionary<string, int> CurveFamilies,
    string StructuralAssessmentBasis,
    string LengthUnit,
    string LengthUnitBasis,
    IdRangeSummary FaceIds,
    IdRangeSummary EdgeIds,
    IdRangeSummary VertexIds);

public sealed record FaceDetail(
    int FaceId,
    string? SurfaceType,
    string SurfaceStatus,
    BoundingBox3D? BoundingBox,
    Point3D? RepresentativePoint,
    Point3D? AnchorPoint,
    Point3D? Apex,
    Vector3D? PlanarNormal,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] Vector3D? Axis,
    double? Radius,
    double? PlacementRadius,
    double? MajorRadius,
    double? MinorRadius,
    double? SemiAngleRadians,
    IReadOnlyList<int> AdjacentEdgeIds,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? StepEntity = null);

public sealed record EdgeDetail(
    int EdgeId,
    string CurveType,
    int StartVertexId,
    Point3D? StartVertex,
    int EndVertexId,
    Point3D? EndVertex,
    IReadOnlyList<int> AdjacentFaceIds,
    double? ParameterRange,
    double? ArcLength,
    string ArcLengthStatus);

public sealed record VertexDetail(
    int VertexId,
    Point3D? Position,
    IReadOnlyList<int> IncidentEdgeIds);

public sealed record AnalyzeResult(
    string StepPath,
    AnalyzeSummary Summary,
    FaceDetail? Face,
    EdgeDetail? Edge,
    VertexDetail? Vertex,
    IReadOnlyList<string> Notes);

public enum OrthographicView
{
    Top,
    Bottom,
    Front,
    Back,
    Left,
    Right
}

public sealed record OrthographicMapMetadata(
    string SourcePath,
    BoundingBox3D BoundingBox,
    OrthographicView View,
    int Rows,
    int Cols,
    string PlaneAxisU,
    string PlaneAxisV,
    string RayDirectionAxis,
    string DepthReference);

public sealed record OrthographicMapSummary(
    int TotalSamples,
    int HitSamples,
    int EmptySamples,
    double? EntryDepthMin,
    double? EntryDepthMax,
    double? ThicknessMin,
    double? ThicknessMax,
    IReadOnlyList<int> VisibleFaceIds,
    IReadOnlyList<string> VisibleSurfaceTypes);

public sealed record OrthographicSample(
    bool Hit,
    double PlaneU,
    double PlaneV,
    double? EntryDepth,
    double? ExitDepth,
    double? Thickness,
    int? EntryFaceId,
    string? EntrySurfaceType,
    Point3D? EntryPoint,
    Vector3D? EntryNormal,
    Point3D? ExitPoint);

public sealed record OrthographicMapResult(
    OrthographicMapMetadata Metadata,
    OrthographicMapSummary Summary,
    IReadOnlyList<IReadOnlyList<OrthographicSample>> Grid,
    IReadOnlyList<string> Notes);


public sealed record RayMapBounds(double[] U, double[] V);
public sealed record RayMapHit(double T, Point3D Position, int? FaceIndex, string? SurfaceFamily, Vector3D? Normal, string IntersectionMode, string Confidence, IReadOnlyList<string> Diagnostics);
public sealed record RayMapSample(int I, int J, double U, double V, bool Hit, RayMapHit? FirstHit, RayMapHit? LastHit, int HitCount, IReadOnlyList<RayMapHit> Hits, IReadOnlyDictionary<string, int> IntersectionModes);
public sealed record RayMapSummary(double HitCoverage, double[]? HeightRange, IReadOnlyDictionary<string, int> SurfaceFamiliesHit, int AnalyticHitCount, int CirHitCount, int TessellatedFallbackHitCount, int UnsupportedSampleCount);
public sealed record RayMapResult(string Mode, string Plane, string Direction, int[]? Resolution, double[]? Point, RayMapBounds Bounds, IReadOnlyList<RayMapSample> Samples, int HitCount, IReadOnlyList<RayMapHit>? Hits, RayMapSummary Summary, string IntersectionMode, string BackendPolicy, IReadOnlyList<string> Diagnostics)
{
    public CompactPointProbeSummary? PointSummary { get; init; }
}
public sealed record CompactPointProbeSummary(int HitCount, CompactHitSummary? FirstHit, CompactHitSummary? LastHit, IReadOnlyList<string> FamilySequence, IReadOnlyDictionary<string, int> BackendModes, double[]? CoordinateRangeAlongRay, IReadOnlyList<string> Diagnostics);
public sealed record CompactHitSummary(string? Family, Point3D Position, int? FaceId, string IntersectionMode);
public sealed record SixViewMapResult(string Mode, string MapVersion, int[] Resolution, IReadOnlyList<SixViewMapView> Views, IReadOnlyList<SuggestedMapProbe> SuggestedProbes, IReadOnlyList<string> Diagnostics)
{
    public IReadOnlyList<RankedMapProbe> RankedProbes { get; init; } = Array.Empty<RankedMapProbe>();
    public EvidenceBundle? EvidenceBundle { get; init; }
}
public sealed record SixViewMapView(string Name, string Plane, string Direction, SixViewMapSummary Summary, CompactGrid? CompactGrid, SixViewMapComponents Components, IReadOnlyList<SuggestedMapProbe> SuggestedProbes, IReadOnlyList<string> MeasuredSummary);
public sealed record SixViewMapSummary(int SampleCount, int HitCount, double HitCoverage, double[]? HeightRange, IReadOnlyList<DominantBand> DominantBands, IReadOnlyDictionary<string, int> SurfaceFamiliesHit, IReadOnlyDictionary<string, int> BackendCounts, double FallbackRatio);
public sealed record DominantBand(double? Value, int SampleCount, double Coverage, string? Meaning, bool MostlyFallback);
public sealed record CompactGrid(string Encoding, int Width, int Height, IReadOnlyDictionary<string, string> Legend, IReadOnlyList<string> Rows);
public sealed record SixViewMapComponents(IReadOnlyList<MapComponent> NoHit, IReadOnlyList<MapComponent> HeightBands, IReadOnlyList<MapComponent> SurfaceFamilies, IReadOnlyList<MapComponent> Fallback, bool Truncated, int OmittedCount);
public sealed record MapComponent(string ComponentId, string Kind, string View, int CellCount, double Coverage, bool TouchesBorder, CellBoundingBox BboxCells, double[] CentroidCell, double[] CentroidUv, string? ClassificationHint, string Confidence, string? Band, double? RepresentativeValue, string? SurfaceFamily, string? BackendModeDominance);
public sealed record CellBoundingBox(int MinI, int MinJ, int MaxI, int MaxJ);
public sealed record SuggestedMapProbe(string ProbeId, string View, string Plane, string Direction, double[] Point, string Reason, string Command, string? SourceComponentId);
public sealed record EvidenceAction(string Kind, string? View, string Command, string Reason, object? Bounds = null, int[]? Resolution = null);
public sealed record RankedMapProbe(int Rank, double Score, double NormalizedScore, string Kind, string View, string ComponentId, string? ClassificationHint, IReadOnlyList<string> Reasons, IReadOnlyList<string> EvidenceTerms, double Uncertainty, string RecommendedNextAction, IReadOnlyList<EvidenceAction> RecommendedActions);
public sealed record EvidenceBundle(string Source, EvidenceBundleCoarseMap CoarseMap, IReadOnlyList<RankedMapProbe> RankedQuestions, IReadOnlyList<EvidenceAction> SuggestedActions, IReadOnlyList<object> ExecutedEvidence, EvidenceBundleLimits Limits, IReadOnlyList<string> Notes);
public sealed record EvidenceBundleCoarseMap(int[] Resolution, int Views, bool SummaryOnly);
public sealed record EvidenceBundleLimits(int MaxRankedItems, int MaxExecutedProbes);

public enum SectionPlaneFamily
{
    XY,
    XZ,
    YZ
}

public sealed record SectionAnalysisMetadata(
    string SourcePath,
    BoundingBox3D BoundingBox,
    SectionPlaneFamily PlaneFamily,
    double Offset,
    string OffsetAxis,
    string OffsetEquation,
    string SectionAxisU,
    string SectionAxisV,
    string WorldToSectionMapping);

public sealed record SectionAnalysisSummary(
    int LoopCount,
    int ClosedLoopCount,
    int LineSegmentCount,
    int ArcSegmentCount,
    int UnsupportedSegmentCount,
    BoundingBox2D? SectionBoundingBox2D);

public sealed record BoundingBox2D(
    Point2D Min,
    Point2D Max);

public sealed record Point2D(
    double U,
    double V);

public sealed record SectionSegment(
    string Kind,
    Point2D Start,
    Point2D End,
    Point2D? Center,
    double? Radius,
    string? Direction,
    double? SweepRadians,
    string? UnsupportedReason,
    int? SourceFace = null,
    string? SourceEntity = null,
    string? SurfaceFamily = null,
    string? ParameterKind = null,
    double? ParameterFrom = null,
    double? ParameterTo = null,
    string? MaterialSideEvidence = null);

public sealed record SectionFragmentEvidence(
    string FragmentId,
    int SourceFace,
    string SourceEntity,
    string SurfaceFamily,
    string CurveFamily,
    string ParameterKind,
    double ParameterFrom,
    double ParameterTo,
    Point2D Start,
    Point2D End,
    Point2D? Center,
    double? Radius,
    string MaterialSideEvidence);

public sealed record SectionLoop(
    int LoopId,
    bool IsClosed,
    string? Winding,
    BoundingBox2D? BoundingBox2D,
    IReadOnlyList<SectionSegment> Segments,
    string? Role = null);

/// <summary>Accounting for the deterministic analytic section-normalization route.</summary>
public sealed record SectionNormalizationDiagnostics(
    int RawFragmentCount,
    int CanonicalVertexCount,
    int AtomicFragmentCount,
    int CollapsedDuplicateCount,
    int NormalizedLoopCount,
    int OuterLoopCount,
    int InnerLoopCount,
    int UnaccountedFragmentCount,
    IReadOnlyList<SectionFragmentEvidence> RawFragments,
    IReadOnlyList<string> Diagnostics,
    double IntersectionMilliseconds,
    double SplittingMilliseconds,
    double GraphConstructionMilliseconds,
    double LoopWalkingMilliseconds);

public sealed record SectionAnalysisResult(
    SectionAnalysisMetadata Metadata,
    SectionAnalysisSummary Summary,
    IReadOnlyList<SectionLoop> Loops,
    IReadOnlyList<string> Notes,
    SectionNormalizationDiagnostics? Normalization = null);
