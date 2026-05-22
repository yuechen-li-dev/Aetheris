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
    IReadOnlyList<int> AdjacentEdgeIds);

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
    string? UnsupportedReason);

public sealed record SectionLoop(
    int LoopId,
    bool IsClosed,
    string? Winding,
    BoundingBox2D? BoundingBox2D,
    IReadOnlyList<SectionSegment> Segments);

public sealed record SectionAnalysisResult(
    SectionAnalysisMetadata Metadata,
    SectionAnalysisSummary Summary,
    IReadOnlyList<SectionLoop> Loops,
    IReadOnlyList<string> Notes);
