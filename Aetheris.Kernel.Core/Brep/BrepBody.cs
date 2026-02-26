using Aetheris.Kernel.Core.Geometry;
using Aetheris.Kernel.Core.Topology;

namespace Aetheris.Kernel.Core.Brep;

/// <summary>
/// Minimal combined topology + geometry + binding aggregate for B-rep bodies.
/// </summary>
public sealed class BrepBody
{
    public BrepBody(TopologyModel topology, BrepGeometryStore geometry, BrepBindingModel bindings)
    {
        Topology = topology;
        Geometry = geometry;
        Bindings = bindings;
    }

    public TopologyModel Topology { get; }

    public BrepGeometryStore Geometry { get; }

    public BrepBindingModel Bindings { get; }

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
