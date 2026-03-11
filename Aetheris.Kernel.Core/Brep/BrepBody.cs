using Aetheris.Kernel.Core.Geometry;
using Aetheris.Kernel.Core.Math;
using Aetheris.Kernel.Core.Topology;

namespace Aetheris.Kernel.Core.Brep;

/// <summary>
/// Minimal combined topology + geometry + binding aggregate for B-rep bodies.
/// </summary>
public sealed class BrepBody
{
    private readonly IReadOnlyDictionary<VertexId, Point3D> _vertexPoints;

    public BrepBody(TopologyModel topology, BrepGeometryStore geometry, BrepBindingModel bindings)
        : this(topology, geometry, bindings, vertexPoints: null)
    {
    }

    public BrepBody(
        TopologyModel topology,
        BrepGeometryStore geometry,
        BrepBindingModel bindings,
        IReadOnlyDictionary<VertexId, Point3D>? vertexPoints)
    {
        Topology = topology;
        Geometry = geometry;
        Bindings = bindings;
        _vertexPoints = vertexPoints ?? new Dictionary<VertexId, Point3D>();
    }

    public TopologyModel Topology { get; }

    public BrepGeometryStore Geometry { get; }

    public BrepBindingModel Bindings { get; }

    public bool TryGetVertexPoint(VertexId vertexId, out Point3D point) => _vertexPoints.TryGetValue(vertexId, out point);

    public bool TryGetEdgeCurveGeometry(EdgeId edgeId, out CurveGeometry? curve)
    {
        curve = null;

        if (!Bindings.TryGetEdgeBinding(edgeId, out var binding))
        {
            return false;
        }

        return Geometry.TryGetCurve(binding.CurveGeometryId, out curve);
    }

    public bool TryGetFaceSurfaceGeometry(FaceId faceId, out SurfaceGeometry? surface)
    {
        surface = null;

        if (!Bindings.TryGetFaceBinding(faceId, out var binding))
        {
            return false;
        }

        return Geometry.TryGetSurface(binding.SurfaceGeometryId, out surface);
    }
}
