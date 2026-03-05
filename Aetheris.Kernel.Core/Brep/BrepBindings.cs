using Aetheris.Kernel.Core.Geometry;
using Aetheris.Kernel.Core.Topology;

namespace Aetheris.Kernel.Core.Brep;

public readonly record struct EdgeGeometryBinding(
    EdgeId EdgeId,
    CurveGeometryId CurveGeometryId,
    ParameterInterval? TrimInterval = null,
    bool OrientedEdgeSense = true);

public readonly record struct FaceGeometryBinding(FaceId FaceId, SurfaceGeometryId SurfaceGeometryId);

/// <summary>
/// Explicit topology-to-geometry binding container.
/// </summary>
public sealed class BrepBindingModel
{
    private readonly Dictionary<EdgeId, EdgeGeometryBinding> _edgeBindings = [];
    private readonly Dictionary<FaceId, FaceGeometryBinding> _faceBindings = [];

    public IEnumerable<EdgeGeometryBinding> EdgeBindings => _edgeBindings.Values;

    public IEnumerable<FaceGeometryBinding> FaceBindings => _faceBindings.Values;

    public void AddEdgeBinding(EdgeGeometryBinding binding) => _edgeBindings.Add(binding.EdgeId, binding);

    public void AddFaceBinding(FaceGeometryBinding binding) => _faceBindings.Add(binding.FaceId, binding);

    public bool TryGetEdgeBinding(EdgeId edgeId, out EdgeGeometryBinding binding) => _edgeBindings.TryGetValue(edgeId, out binding);

    public bool TryGetFaceBinding(FaceId faceId, out FaceGeometryBinding binding) => _faceBindings.TryGetValue(faceId, out binding);

    public EdgeGeometryBinding GetEdgeBinding(EdgeId edgeId) => _edgeBindings[edgeId];

    public FaceGeometryBinding GetFaceBinding(FaceId faceId) => _faceBindings[faceId];
}
