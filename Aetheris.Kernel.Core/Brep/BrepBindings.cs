using Aetheris.Kernel.Core.Geometry.Curves;
using Aetheris.Kernel.Core.Geometry;
using Aetheris.Kernel.Core.Topology;

namespace Aetheris.Kernel.Core.Brep;

public readonly record struct EdgeGeometryBinding(
    EdgeId EdgeId,
    CurveGeometryId CurveGeometryId,
    ParameterInterval? TrimInterval = null,
    bool OrientedEdgeSense = true);

/// <summary>
/// Canonical face orientation after topology/material-side resolution.  This is
/// intentionally not a STEP <c>same_sense</c> flag: interchange evidence is
/// retained separately on <see cref="BrepBody.FaceOrientationReport"/>.
/// </summary>
public readonly record struct ResolvedFaceOrientation(bool IsAlignedWithSurface)
{
    public static ResolvedFaceOrientation Aligned { get; } = new(true);
    public static ResolvedFaceOrientation Opposed { get; } = new(false);

    public ResolvedFaceOrientation Reversed() => new(!IsAlignedWithSurface);
}

/// <summary>A topology-to-surface binding whose face orientation is already resolved.</summary>
public readonly record struct FaceGeometryBinding
{
    // Keep the historical bool constructor shape for authored kernel geometry,
    // while exposing only the resolved typed value to consumers.
    public FaceGeometryBinding(
        FaceId FaceId,
        SurfaceGeometryId SurfaceGeometryId,
        bool IsAlignedWithSurface = true,
        int? SourceStepEntityId = null)
        : this(FaceId, SurfaceGeometryId, new ResolvedFaceOrientation(IsAlignedWithSurface), SourceStepEntityId)
    {
    }

    public FaceGeometryBinding(
        FaceId FaceId,
        SurfaceGeometryId SurfaceGeometryId,
        ResolvedFaceOrientation Orientation,
        int? SourceStepEntityId = null)
    {
        this.FaceId = FaceId;
        this.SurfaceGeometryId = SurfaceGeometryId;
        this.Orientation = Orientation;
        this.SourceStepEntityId = SourceStepEntityId;
    }

    public FaceId FaceId { get; init; }
    public SurfaceGeometryId SurfaceGeometryId { get; init; }
    public ResolvedFaceOrientation Orientation { get; init; }
    public int? SourceStepEntityId { get; init; }
}

public readonly record struct SurfaceParameterPoint(double U, double V);

public enum PcurveGeometryKind { Line, Circle, Ellipse, Polyline, Polynomial }

/// <summary>A face-local parameter-space curve with the same parameter as its bound 3D edge.</summary>
public sealed record PcurveGeometry
{
    private PcurveGeometry(PcurveGeometryKind kind, ParameterInterval domain, IReadOnlyList<SurfaceParameterPoint> points)
    {
        Kind = kind;
        Domain = domain;
        Points = points;
    }

    public BSpline3Curve? PolynomialCurve { get; private init; }
    public IReadOnlyList<double>? RationalWeights { get; private init; }
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

    /// <summary>Reuses the polynomial curve authority in the UV plane (Z must be zero).</summary>
    public static PcurveGeometry Polynomial(ParameterInterval domain, BSpline3Curve curve)
    {
        if (!double.IsFinite(domain.Start) || !double.IsFinite(domain.End) || domain.Start == domain.End ||
            System.Math.Min(domain.Start, domain.End) < curve.DomainStart || System.Math.Max(domain.Start, domain.End) > curve.DomainEnd ||
            curve.ControlPoints.Any(p => !double.IsFinite(p.X) || !double.IsFinite(p.Y) || p.Z != 0))
            throw new ArgumentException("Polynomial pcurve requires a finite in-domain trim and UV-plane controls.");
        return new(PcurveGeometryKind.Polynomial, domain, []) { PolynomialCurve = curve };
    }

    public static PcurveGeometry RationalPolynomial(ParameterInterval domain, BSpline3Curve curve, IReadOnlyList<double> weights)
    {
        if (weights.Count != curve.ControlPoints.Count || weights.Any(weight => !double.IsFinite(weight) || weight <= 0d))
            throw new ArgumentException("Rational pcurve weights must be finite, positive, and match the control points.");
        var polynomial = Polynomial(domain, curve);
        return polynomial with { RationalWeights = weights.ToArray() };
    }

    public SurfaceParameterPoint Evaluate(double parameter)
    {
        var span = Domain.End - Domain.Start;
        var fraction = double.Abs(span) <= 1e-15d ? 0d : System.Math.Clamp((parameter - Domain.Start) / span, 0d, 1d);
        if (PolynomialCurve is { } polynomial)
        {
            if (RationalWeights is { } weights)
                return EvaluateRational(polynomial, weights, Domain.Start + span * fraction);
            var point = polynomial.Evaluate(Domain.Start + span * fraction);
            return new(point.X, point.Y);
        }
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

    private static SurfaceParameterPoint EvaluateRational(BSpline3Curve curve, IReadOnlyList<double> weights, double parameter)
    {
        var u = System.Math.Clamp(parameter, curve.DomainStart, curve.DomainEnd);
        var basis = new double[curve.ControlPoints.Count];
        for (var i = 0; i < basis.Length; i++)
            basis[i] = (u >= curve.FullKnots[i] && u < curve.FullKnots[i + 1])
                || (u == curve.DomainEnd && i == basis.Length - 1) ? 1d : 0d;
        for (var degree = 1; degree <= curve.Degree; degree++)
        {
            var next = new double[basis.Length];
            for (var i = 0; i < basis.Length; i++)
            {
                var leftDenominator = curve.FullKnots[i + degree] - curve.FullKnots[i];
                var rightDenominator = i + degree + 1 < curve.FullKnots.Count
                    ? curve.FullKnots[i + degree + 1] - curve.FullKnots[i + 1] : 0d;
                var left = leftDenominator == 0d ? 0d : (u - curve.FullKnots[i]) / leftDenominator * basis[i];
                var right = i + 1 >= basis.Length || rightDenominator == 0d
                    ? 0d : (curve.FullKnots[i + degree + 1] - u) / rightDenominator * basis[i + 1];
                next[i] = left + right;
            }
            basis = next;
        }
        var sumU = 0d;
        var sumV = 0d;
        var denominator = 0d;
        for (var i = 0; i < basis.Length; i++)
        {
            var weighted = basis[i] * weights[i];
            sumU += curve.ControlPoints[i].X * weighted;
            sumV += curve.ControlPoints[i].Y * weighted;
            denominator += weighted;
        }
        if (double.Abs(denominator) <= 1e-15d) return default;
        return new(sumU / denominator, sumV / denominator);
    }
}

/// <summary>Associates one coedge use with its curve in the owning face's UV space.</summary>
public readonly record struct CoedgePcurveBinding(
    CoedgeId CoedgeId,
    FaceId FaceId,
    SurfaceGeometryId SurfaceGeometryId,
    PcurveGeometry Pcurve,
    bool SameSense = true,
    int? SourceStepPcurveEntityId = null,
    int? SourceStepCurveEntityId = null,
    string? SourceCurveType = null,
    int? SourceStepSurfaceEntityId = null);

public enum FaceBoundaryRole { Outer, Inner }

/// <summary>
/// Semantic role of one loop within one face. The role is use-context evidence:
/// it must not be reconstructed from LoopId ordering during STEP export.
/// </summary>
public readonly record struct FaceBoundaryRoleBinding(
    FaceId FaceId,
    LoopId LoopId,
    FaceBoundaryRole Role,
    int? SourceStepEntityId = null);

/// <summary>Face-local parameter location and provenance for an explicit STEP VERTEX_LOOP.</summary>
public readonly record struct VertexLoopParameterBinding(
    LoopId LoopId,
    FaceId FaceId,
    SurfaceGeometryId SurfaceGeometryId,
    VertexId VertexId,
    SurfaceParameterPoint? Parameter,
    int? SourceStepLoopEntityId = null,
    int? SourceStepVertexEntityId = null);

/// <summary>
/// Explicit topology-to-geometry binding container.
/// </summary>
public sealed class BrepBindingModel
{
    private readonly Dictionary<EdgeId, EdgeGeometryBinding> _edgeBindings = [];
    private readonly Dictionary<FaceId, FaceGeometryBinding> _faceBindings = [];
    private readonly Dictionary<CoedgeId, CoedgePcurveBinding> _pcurveBindings = [];
    private readonly Dictionary<LoopId, FaceBoundaryRoleBinding> _faceBoundaryRoleBindings = [];
    private readonly Dictionary<LoopId, VertexLoopParameterBinding> _vertexLoopParameterBindings = [];

    public IEnumerable<EdgeGeometryBinding> EdgeBindings => _edgeBindings.Values;

    public IEnumerable<FaceGeometryBinding> FaceBindings => _faceBindings.Values;
    public IEnumerable<CoedgePcurveBinding> PcurveBindings => _pcurveBindings.Values;
    public IEnumerable<FaceBoundaryRoleBinding> FaceBoundaryRoleBindings => _faceBoundaryRoleBindings.Values;
    public IEnumerable<VertexLoopParameterBinding> VertexLoopParameterBindings => _vertexLoopParameterBindings.Values;

    public void AddEdgeBinding(EdgeGeometryBinding binding) => _edgeBindings.Add(binding.EdgeId, binding);

    public void AddFaceBinding(FaceGeometryBinding binding) => _faceBindings.Add(binding.FaceId, binding);
    public void AddPcurveBinding(CoedgePcurveBinding binding) => _pcurveBindings.Add(binding.CoedgeId, binding);
    public void AddFaceBoundaryRoleBinding(FaceBoundaryRoleBinding binding) => _faceBoundaryRoleBindings.Add(binding.LoopId, binding);
    public void AddVertexLoopParameterBinding(VertexLoopParameterBinding binding) => _vertexLoopParameterBindings.Add(binding.LoopId, binding);

    public bool TryGetEdgeBinding(EdgeId edgeId, out EdgeGeometryBinding binding) => _edgeBindings.TryGetValue(edgeId, out binding);

    public bool TryGetFaceBinding(FaceId faceId, out FaceGeometryBinding binding) => _faceBindings.TryGetValue(faceId, out binding);
    public bool TryGetPcurveBinding(CoedgeId coedgeId, out CoedgePcurveBinding binding) => _pcurveBindings.TryGetValue(coedgeId, out binding);
    public bool TryGetFaceBoundaryRoleBinding(LoopId loopId, out FaceBoundaryRoleBinding binding) => _faceBoundaryRoleBindings.TryGetValue(loopId, out binding);
    public bool TryGetVertexLoopParameterBinding(LoopId loopId, out VertexLoopParameterBinding binding) => _vertexLoopParameterBindings.TryGetValue(loopId, out binding);

    public EdgeGeometryBinding GetEdgeBinding(EdgeId edgeId) => _edgeBindings[edgeId];

    public FaceGeometryBinding GetFaceBinding(FaceId faceId) => _faceBindings[faceId];
    public CoedgePcurveBinding GetPcurveBinding(CoedgeId coedgeId) => _pcurveBindings[coedgeId];
    public FaceBoundaryRoleBinding GetFaceBoundaryRoleBinding(LoopId loopId) => _faceBoundaryRoleBindings[loopId];
    public VertexLoopParameterBinding GetVertexLoopParameterBinding(LoopId loopId) => _vertexLoopParameterBindings[loopId];
}
