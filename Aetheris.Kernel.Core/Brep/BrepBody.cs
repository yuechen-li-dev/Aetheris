using Aetheris.Kernel.Core.Geometry;
using Aetheris.Kernel.Core.Math;
using Aetheris.Kernel.Core.Topology;
using Aetheris.Kernel.Core.Brep.Boolean;

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
        IReadOnlyDictionary<VertexId, Point3D>? vertexPoints,
        SafeBooleanComposition? safeBooleanComposition = null,
        BrepBodyShellRepresentation? shellRepresentation = null)
    {
        Topology = topology;
        Geometry = geometry;
        Bindings = bindings;
        _vertexPoints = vertexPoints ?? new Dictionary<VertexId, Point3D>();
        SafeBooleanComposition = safeBooleanComposition;
        ShellRepresentation = shellRepresentation ?? TryCreateLegacySingleShellRepresentation(topology);
    }

    public TopologyModel Topology { get; }

    public BrepGeometryStore Geometry { get; }

    public BrepBindingModel Bindings { get; }

    public SafeBooleanComposition? SafeBooleanComposition { get; }

    public BrepBodyShellRepresentation? ShellRepresentation { get; }

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


    private static BrepBodyShellRepresentation? TryCreateLegacySingleShellRepresentation(TopologyModel topology)
    {
        var bodies = topology.Bodies.OrderBy(body => body.Id.Value).ToArray();
        if (bodies.Length != 1)
        {
            return null;
        }

        var shellIds = bodies[0].ShellIds.OrderBy(shellId => shellId.Value).ToArray();
        if (shellIds.Length != 1)
        {
            return null;
        }

        return new BrepBodyShellRepresentation(shellIds[0], []);
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
