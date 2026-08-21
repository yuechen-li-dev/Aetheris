using Aetheris.Kernel.Core.Geometry;
using Aetheris.Kernel.Core.Topology;

namespace Aetheris.Kernel.Core.Brep;

public readonly record struct EdgeGeometryBinding(
    EdgeId EdgeId,
    CurveGeometryId CurveGeometryId,
    ParameterInterval? TrimInterval = null,
    bool OrientedEdgeSense = true);

public readonly record struct FaceGeometryBinding(
    FaceId FaceId,
    SurfaceGeometryId SurfaceGeometryId,
    bool SameSense = true,
    int? SourceStepEntityId = null);

public readonly record struct SurfaceParameterPoint(double U, double V);

public enum PcurveGeometryKind { Line, Circle, Ellipse, Polyline }

/// <summary>A face-local parameter-space curve with the same parameter as its bound 3D edge.</summary>
public sealed record PcurveGeometry
{
    private PcurveGeometry(PcurveGeometryKind kind, ParameterInterval domain, IReadOnlyList<SurfaceParameterPoint> points)
    {
        Kind = kind;
        Domain = domain;
        Points = points;
    }

    public PcurveGeometryKind Kind { get; }
    public ParameterInterval Domain { get; }
    public IReadOnlyList<SurfaceParameterPoint> Points { get; }

    public static PcurveGeometry Line(ParameterInterval domain, SurfaceParameterPoint start, SurfaceParameterPoint end)
        => new(PcurveGeometryKind.Line, domain, [start, end]);

    public static PcurveGeometry Circle(ParameterInterval domain, SurfaceParameterPoint center, double radiusU, double radiusV)
        => new(PcurveGeometryKind.Circle, domain, [center, new(radiusU, radiusV)]);

    public static PcurveGeometry Ellipse(ParameterInterval domain, SurfaceParameterPoint center,
        SurfaceParameterPoint cosineCoefficient, SurfaceParameterPoint sineCoefficient)
        => new(PcurveGeometryKind.Ellipse, domain, [center, cosineCoefficient, sineCoefficient]);

    public static PcurveGeometry Polyline(ParameterInterval domain, IReadOnlyList<SurfaceParameterPoint> points)
        => new(PcurveGeometryKind.Polyline, domain, points.ToArray());

    public SurfaceParameterPoint Evaluate(double parameter)
    {
        var span = Domain.End - Domain.Start;
        var fraction = double.Abs(span) <= 1e-15d ? 0d : System.Math.Clamp((parameter - Domain.Start) / span, 0d, 1d);
        if (Kind == PcurveGeometryKind.Circle)
        {
            var angle = Domain.Start + (span * fraction);
            return new(Points[0].U + (Points[1].U * double.Cos(angle)), Points[0].V + (Points[1].V * double.Sin(angle)));
        }
        if (Kind == PcurveGeometryKind.Ellipse)
        {
            var angle = Domain.Start + (span * fraction);
            return new(Points[0].U + (Points[1].U * double.Cos(angle)) + (Points[2].U * double.Sin(angle)),
                Points[0].V + (Points[1].V * double.Cos(angle)) + (Points[2].V * double.Sin(angle)));
        }
        if (Kind == PcurveGeometryKind.Line)
            return Lerp(Points[0], Points[1], fraction);
        if (Points.Count == 0) return default;
        if (Points.Count == 1) return Points[0];
        var scaled = fraction * (Points.Count - 1);
        var index = System.Math.Min((int)double.Floor(scaled), Points.Count - 2);
        return Lerp(Points[index], Points[index + 1], scaled - index);
    }

    private static SurfaceParameterPoint Lerp(SurfaceParameterPoint a, SurfaceParameterPoint b, double t)
        => new(a.U + ((b.U - a.U) * t), a.V + ((b.V - a.V) * t));
}

/// <summary>Associates one coedge use with its curve in the owning face's UV space.</summary>
public readonly record struct CoedgePcurveBinding(
    CoedgeId CoedgeId,
    FaceId FaceId,
    SurfaceGeometryId SurfaceGeometryId,
    PcurveGeometry Pcurve,
    bool SameSense = true);

/// <summary>
/// Explicit topology-to-geometry binding container.
/// </summary>
public sealed class BrepBindingModel
{
    private readonly Dictionary<EdgeId, EdgeGeometryBinding> _edgeBindings = [];
    private readonly Dictionary<FaceId, FaceGeometryBinding> _faceBindings = [];
    private readonly Dictionary<CoedgeId, CoedgePcurveBinding> _pcurveBindings = [];

    public IEnumerable<EdgeGeometryBinding> EdgeBindings => _edgeBindings.Values;

    public IEnumerable<FaceGeometryBinding> FaceBindings => _faceBindings.Values;
    public IEnumerable<CoedgePcurveBinding> PcurveBindings => _pcurveBindings.Values;

    public void AddEdgeBinding(EdgeGeometryBinding binding) => _edgeBindings.Add(binding.EdgeId, binding);

    public void AddFaceBinding(FaceGeometryBinding binding) => _faceBindings.Add(binding.FaceId, binding);
    public void AddPcurveBinding(CoedgePcurveBinding binding) => _pcurveBindings.Add(binding.CoedgeId, binding);

    public bool TryGetEdgeBinding(EdgeId edgeId, out EdgeGeometryBinding binding) => _edgeBindings.TryGetValue(edgeId, out binding);

    public bool TryGetFaceBinding(FaceId faceId, out FaceGeometryBinding binding) => _faceBindings.TryGetValue(faceId, out binding);
    public bool TryGetPcurveBinding(CoedgeId coedgeId, out CoedgePcurveBinding binding) => _pcurveBindings.TryGetValue(coedgeId, out binding);

    public EdgeGeometryBinding GetEdgeBinding(EdgeId edgeId) => _edgeBindings[edgeId];

    public FaceGeometryBinding GetFaceBinding(FaceId faceId) => _faceBindings[faceId];
    public CoedgePcurveBinding GetPcurveBinding(CoedgeId coedgeId) => _pcurveBindings[coedgeId];
}
