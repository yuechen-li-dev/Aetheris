using Aetheris.Kernel.Core.Math;
using Aetheris.Kernel.Core.Topology;

namespace Aetheris.Kernel.Core.Brep.Tessellation;

public sealed record DisplayTessellationResult(
    IReadOnlyList<DisplayFaceMeshPatch> FacePatches,
    IReadOnlyList<DisplayEdgePolyline> EdgePolylines,
    IReadOnlyList<DisplayFaceMaterializationDiagnostic>? FaceDiagnostics = null);

public enum DisplayFaceMeshSource
{
    Tessellator,
    BsplineUvScaffold,
}

public sealed record DisplayFaceMeshPatch(
    FaceId FaceId,
    IReadOnlyList<Point3D> Positions,
    IReadOnlyList<Vector3D> Normals,
    IReadOnlyList<int> TriangleIndices,
    DisplayFaceMeshSource Source = DisplayFaceMeshSource.Tessellator,
    string? ScaffoldRejectionReason = null);

public sealed record DisplayEdgePolyline(
    EdgeId EdgeId,
    IReadOnlyList<Point3D> Points,
    bool IsClosed);

public sealed record DisplayFaceMaterializationDiagnostic(
    FaceId? FaceId,
    string? SurfaceKind,
    string Phase,
    string Code,
    string Message);
