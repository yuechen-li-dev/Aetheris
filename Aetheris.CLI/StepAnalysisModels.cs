using Aetheris.Kernel.Core.Math;

namespace Aetheris.CLI;

public sealed record AnalyzeSummary(
    int BodyCount,
    int ShellCount,
    int FaceCount,
    int EdgeCount,
    int VertexCount,
    BoundingBox3D? BoundingBox,
    string StructuralAssessment,
    IReadOnlyDictionary<string, int> SurfaceFamilies,
    string StructuralAssessmentBasis);

public sealed record FaceDetail(
    int FaceId,
    string SurfaceType,
    BoundingBox3D? BoundingBox,
    Point3D? RepresentativePoint,
    Vector3D? PlanarNormal,
    Vector3D? Axis,
    double? Radius,
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
    double? Length);

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
